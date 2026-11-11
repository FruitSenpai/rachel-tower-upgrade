using System.Security.Cryptography;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;
using Npgsql;
using RachelTower.Web.Data;

namespace RachelTower.Web.Reservations;

public enum BookingError
{
    None,
    InvalidDates,
    ArrivalInPast,
    TooManyNights,
    TooFarAhead,
    TooManyGuests,
    UnknownRoomType,
    NoAvailability,
    NotAllowed,
    NotFound,
    ConcurrencyConflict,
}

public sealed record BookingResult(Booking? Booking, BookingError Error)
{
    public bool Ok => Error == BookingError.None;
    public static BookingResult Success(Booking b) => new(b, BookingError.None);
    public static BookingResult Fail(BookingError e) => new(null, e);
}

public sealed record CreateBookingRequest(
    string RoomTypeCode,
    DateOnly CheckIn,
    DateOnly CheckOut,
    int Guests,
    string GuestName,
    string GuestPhone,
    string? GuestEmail,
    string? Notes,
    string Culture,
    PaymentMethod PaymentMethod = PaymentMethod.PayAtDesk,
    BookingSource Source = BookingSource.Website,
    int? RoomId = null,
    bool AlreadyPaid = false);

/// <summary>
/// Creates and changes bookings. Double bookings are prevented twice over: rooms are only offered
/// when free, and the database's exclusion constraint rejects any overlap that slips through
/// (e.g. two guests clicking "Book" for the last room at the same moment).
/// </summary>
public sealed class BookingService(
    AppDbContext db,
    AvailabilityService availability,
    HotelClock clock,
    IOptions<BookingOptions> options,
    ILogger<BookingService> logger)
{
    private const string ReferenceAlphabet = "ABCDEFGHJKMNPQRSTUVWXYZ23456789"; // no 0/O, 1/I/L

    private BookingOptions Rules => options.Value;

    /// <summary>Checks the stay dates against the booking rules.</summary>
    public BookingError ValidateStay(DateOnly checkIn, DateOnly checkOut)
    {
        var nights = checkOut.DayNumber - checkIn.DayNumber;
        if (nights < 1) return BookingError.InvalidDates;
        if (checkIn < clock.Today) return BookingError.ArrivalInPast;
        if (nights > Rules.MaxNights) return BookingError.TooManyNights;
        if (checkIn.DayNumber - clock.Today.DayNumber > Rules.MaxDaysAhead) return BookingError.TooFarAhead;
        return BookingError.None;
    }

    public async Task<BookingResult> CreateAsync(CreateBookingRequest request, CancellationToken ct = default)
    {
        var error = ValidateStay(request.CheckIn, request.CheckOut);
        if (error != BookingError.None) return BookingResult.Fail(error);

        var type = await db.RoomTypes.AsNoTracking().FirstOrDefaultAsync(t => t.Code == request.RoomTypeCode, ct);
        if (type is null) return BookingResult.Fail(BookingError.UnknownRoomType);
        if (request.Guests < 1 || request.Guests > type.MaxGuests) return BookingResult.Fail(BookingError.TooManyGuests);

        await ExpireOnlinePaymentHoldsAsync(ct);

        var candidates = await availability.FreeRooms(request.CheckIn, request.CheckOut)
            .Where(r => r.RoomTypeId == type.Id && (request.RoomId == null || r.Id == request.RoomId))
            .OrderBy(r => r.Number)
            .Select(r => r.Id)
            .ToListAsync(ct);

        foreach (var roomId in candidates)
        {
            var booking = NewBooking(request, type, roomId);
            db.Bookings.Add(booking);
            try
            {
                await db.SaveChangesAsync(ct);
                await db.Entry(booking).Reference(x => x.RoomType).LoadAsync(ct);
                await db.Entry(booking).Reference(x => x.Room).LoadAsync(ct);
                logger.LogInformation("Booking {Reference} created for room {RoomId}, {CheckIn} to {CheckOut}",
                    booking.Reference, roomId, booking.CheckIn, booking.CheckOut);
                return BookingResult.Success(booking);
            }
            catch (DbUpdateException ex) when (IsOverlap(ex) || IsUniqueViolation(ex))
            {
                // Someone took this room a moment ago (or, very rarely, the reference already exists):
                // try the next free room.
                db.Entry(booking).State = EntityState.Detached;
            }
        }

        return BookingResult.Fail(BookingError.NoAvailability);
    }

    /// <summary>Latest moment a guest can cancel online.</summary>
    public DateTimeOffset CancellationDeadline(Booking b) =>
        clock.At(b.CheckIn, Rules.CheckInHour).AddHours(-Rules.FreeCancellationHours);

    public bool GuestCanCancel(Booking b) =>
        b.Status is BookingStatus.Confirmed or BookingStatus.PendingPayment && clock.UtcNow < CancellationDeadline(b);

    public Task<Booking?> FindByTokenAsync(string token, CancellationToken ct = default) =>
        db.Bookings.Include(b => b.RoomType).Include(b => b.Room).FirstOrDefaultAsync(b => b.ManageToken == token, ct);

    public async Task<BookingResult> CancelByGuestAsync(string token, CancellationToken ct = default)
    {
        var booking = await FindByTokenAsync(token, ct);
        if (booking is null) return BookingResult.Fail(BookingError.NotFound);
        if (!GuestCanCancel(booking)) return BookingResult.Fail(BookingError.NotAllowed);
        return await SaveAsync(booking, b => { b.Status = BookingStatus.Cancelled; b.CancelledAt = clock.UtcNow; }, ct);
    }

    // ----- Front desk actions -----

    public Task<BookingResult> CheckInAsync(int id, CancellationToken ct = default) =>
        TransitionAsync(id, [BookingStatus.Confirmed, BookingStatus.NoShow], b => b.Status = BookingStatus.CheckedIn, ct);

    public Task<BookingResult> CheckOutAsync(int id, CancellationToken ct = default) =>
        TransitionAsync(id, [BookingStatus.CheckedIn], b => b.Status = BookingStatus.CheckedOut, ct);

    public Task<BookingResult> MarkNoShowAsync(int id, CancellationToken ct = default) =>
        TransitionAsync(id, [BookingStatus.Confirmed], b => b.Status = BookingStatus.NoShow, ct);

    public Task<BookingResult> CancelByStaffAsync(int id, CancellationToken ct = default) =>
        TransitionAsync(id, [BookingStatus.Confirmed, BookingStatus.PendingPayment],
            b => { b.Status = BookingStatus.Cancelled; b.CancelledAt = clock.UtcNow; }, ct);

    public Task<BookingResult> MarkPaidAsync(int id, CancellationToken ct = default) =>
        TransitionAsync(id, [BookingStatus.Confirmed, BookingStatus.CheckedIn, BookingStatus.CheckedOut],
            b => { b.PaymentStatus = PaymentStatus.Paid; b.HoldExpiresAt = null; }, ct);

    public async Task<BookingResult> UpdateNotesAsync(int id, string? notes, CancellationToken ct = default)
    {
        var booking = await db.Bookings.FindAsync([id], ct);
        return booking is null
            ? BookingResult.Fail(BookingError.NotFound)
            : await SaveAsync(booking, b => b.Notes = string.IsNullOrWhiteSpace(notes) ? null : notes.Trim(), ct);
    }

    // ----- Housekeeping (run by BookingMaintenanceService every minute) -----

    /// <summary>Frees rooms whose online payment hold ran out.</summary>
    public Task<int> ExpireOnlinePaymentHoldsAsync(CancellationToken ct = default)
    {
        var now = clock.UtcNow;
        return db.Bookings
            .Where(b => b.Status == BookingStatus.PendingPayment && b.HoldExpiresAt <= now)
            .ExecuteUpdateAsync(s => s
                .SetProperty(b => b.Status, BookingStatus.Expired)
                .SetProperty(b => b.UpdatedAt, now), ct);
    }

    /// <summary>Releases unpaid pay-at-desk bookings whose guest hasn't arrived by the release time.</summary>
    public Task<int> ReleaseUnpaidNoShowsAsync(CancellationToken ct = default)
    {
        var now = clock.UtcNow;
        return db.Bookings
            .Where(b => b.Status == BookingStatus.Confirmed
                        && b.PaymentStatus == PaymentStatus.Unpaid
                        && b.HoldExpiresAt != null && b.HoldExpiresAt <= now)
            .ExecuteUpdateAsync(s => s
                .SetProperty(b => b.Status, BookingStatus.NoShow)
                .SetProperty(b => b.UpdatedAt, now), ct);
    }

    // ----- Helpers -----

    private Booking NewBooking(CreateBookingRequest r, RoomType type, int roomId)
    {
        var now = clock.UtcNow;
        var online = r.PaymentMethod == PaymentMethod.MobileMoney && !r.AlreadyPaid;
        return new Booking
        {
            Reference = NewReference(),
            ManageToken = NewToken(),
            RoomId = roomId,
            RoomTypeId = type.Id,
            CheckIn = r.CheckIn,
            CheckOut = r.CheckOut,
            Guests = r.Guests,
            GuestName = r.GuestName.Trim(),
            GuestPhone = r.GuestPhone.Trim(),
            GuestEmail = string.IsNullOrWhiteSpace(r.GuestEmail) ? null : r.GuestEmail.Trim(),
            Notes = string.IsNullOrWhiteSpace(r.Notes) ? null : r.Notes.Trim(),
            Culture = r.Culture,
            Status = online ? BookingStatus.PendingPayment : BookingStatus.Confirmed,
            PaymentMethod = r.PaymentMethod,
            PaymentStatus = r.AlreadyPaid ? PaymentStatus.Paid : PaymentStatus.Unpaid,
            Source = r.Source,
            NightlyRateUsd = type.NightlyRateUsd,
            TotalUsd = type.NightlyRateUsd * (r.CheckOut.DayNumber - r.CheckIn.DayNumber),
            HoldExpiresAt = online ? now.AddMinutes(Rules.OnlinePaymentHoldMinutes)
                          : r.AlreadyPaid ? null
                          : ReleaseTime(r.CheckIn, now),
            CreatedAt = now,
            UpdatedAt = now,
        };
    }

    /// <summary>
    /// When an unpaid pay-at-desk booking is released. A same-day booking made after the
    /// release hour gets at least 4 hours, so it isn't released the moment it's made.
    /// </summary>
    private DateTimeOffset? ReleaseTime(DateOnly checkIn, DateTimeOffset now)
    {
        if (Rules.ReleaseUnpaidAtHour is not { } hour) return null;
        var release = clock.At(checkIn, hour);
        var minimum = now.AddHours(4);
        return release > minimum ? release : minimum;
    }

    private async Task<BookingResult> TransitionAsync(int id, BookingStatus[] from, Action<Booking> apply, CancellationToken ct)
    {
        var booking = await db.Bookings.FindAsync([id], ct);
        if (booking is null) return BookingResult.Fail(BookingError.NotFound);
        if (!from.Contains(booking.Status)) return BookingResult.Fail(BookingError.NotAllowed);
        return await SaveAsync(booking, apply, ct);
    }

    private async Task<BookingResult> SaveAsync(Booking booking, Action<Booking> apply, CancellationToken ct)
    {
        apply(booking);
        booking.UpdatedAt = clock.UtcNow;
        try
        {
            await db.SaveChangesAsync(ct);
            return BookingResult.Success(booking);
        }
        catch (DbUpdateConcurrencyException)
        {
            return BookingResult.Fail(BookingError.ConcurrencyConflict);
        }
        catch (DbUpdateException ex) when (IsOverlap(ex))
        {
            // e.g. checking in a no-show whose room has since been re-let.
            return BookingResult.Fail(BookingError.NoAvailability);
        }
    }

    private static bool IsOverlap(DbUpdateException ex) =>
        ex.InnerException is PostgresException { SqlState: PostgresErrorCodes.ExclusionViolation };

    private static bool IsUniqueViolation(DbUpdateException ex) =>
        ex.InnerException is PostgresException { SqlState: PostgresErrorCodes.UniqueViolation };

    private static string NewReference() =>
        "RT-" + RandomNumberGenerator.GetString(ReferenceAlphabet, 6);

    private static string NewToken() =>
        Convert.ToBase64String(RandomNumberGenerator.GetBytes(24)).Replace('+', '-').Replace('/', '_');
}
