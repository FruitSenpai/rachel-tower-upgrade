using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Npgsql;
using RachelTower.Web.Data;
using RachelTower.Web.Reservations;

namespace RachelTower.Tests;

[Collection(nameof(DatabaseCollection))]
public sealed class BookingTests(DatabaseFixture fx) : IAsyncLifetime
{
    // "Today" at the hotel is 10 November 2026.
    private static readonly DateOnly Today = new(2026, 11, 10);
    private static readonly DateOnly Dec20 = new(2026, 12, 20);
    private static readonly DateOnly Dec23 = new(2026, 12, 23);

    public async ValueTask InitializeAsync() => await fx.ResetAsync();
    public ValueTask DisposeAsync() => ValueTask.CompletedTask;

    private async Task<T> Use<T>(Func<IServiceProvider, Task<T>> action)
    {
        await using var scope = fx.Services.CreateAsyncScope();
        return await action(scope.ServiceProvider);
    }

    private Task<BookingResult> Book(string type, DateOnly checkIn, DateOnly checkOut, int guests = 2,
        PaymentMethod method = PaymentMethod.PayAtDesk, BookingSource source = BookingSource.Website, int? roomId = null) =>
        Use(sp => sp.GetRequiredService<BookingService>().CreateAsync(new CreateBookingRequest(
            type, checkIn, checkOut, guests, "Test Guest", "+243 81 000 0000", "guest@example.com", null, "en",
            method, source, roomId)));

    private Task<IReadOnlyList<RoomTypeAvailability>> Search(DateOnly checkIn, DateOnly checkOut) =>
        Use(sp => sp.GetRequiredService<AvailabilityService>().SearchAsync(checkIn, checkOut));

    private static int Free(IReadOnlyList<RoomTypeAvailability> results, string code) =>
        results.Single(r => r.RoomType.Code == code).AvailableRooms;

    [Fact]
    public async Task Empty_hotel_offers_five_VIP_and_three_Standard_rooms_at_the_right_price()
    {
        var results = await Search(Dec20, Dec23);

        Assert.Equal(["vip", "standard"], results.Select(r => r.RoomType.Code));
        Assert.Equal(5, Free(results, "vip"));
        Assert.Equal(3, Free(results, "standard"));
        Assert.Equal(450m, results[0].TotalUsd); // 3 nights x 150
        Assert.Equal(390m, results[1].TotalUsd); // 3 nights x 130
    }

    [Fact]
    public async Task Booking_stores_price_and_takes_one_room_for_overlapping_dates_only()
    {
        var result = await Book("standard", Dec20, Dec23);

        Assert.True(result.Ok);
        var b = result.Booking!;
        Assert.Matches("^RT-[A-Z2-9]{6}$", b.Reference);
        Assert.Equal(BookingStatus.Confirmed, b.Status);
        Assert.Equal(PaymentStatus.Unpaid, b.PaymentStatus);
        Assert.Equal(130m, b.NightlyRateUsd);
        Assert.Equal(390m, b.TotalUsd);

        Assert.Equal(2, Free(await Search(new(2026, 12, 22), new(2026, 12, 25)), "standard")); // overlaps
        Assert.Equal(3, Free(await Search(Dec23, new(2026, 12, 26)), "standard"));              // arrives the day they leave
        Assert.Equal(3, Free(await Search(new(2026, 12, 18), Dec20), "standard"));              // leaves the day they arrive
        Assert.Equal(5, Free(await Search(Dec20, Dec23), "vip"));                               // other room type
    }

    [Fact]
    public async Task Fourth_Standard_booking_for_the_same_night_is_refused()
    {
        for (var i = 0; i < 3; i++) Assert.True((await Book("standard", Dec20, Dec23)).Ok);

        var fourth = await Book("standard", Dec20, Dec23);

        Assert.Equal(BookingError.NoAvailability, fourth.Error);
        Assert.True((await Book("vip", Dec20, Dec23)).Ok);
    }

    [Fact]
    public async Task Simultaneous_requests_for_the_last_rooms_never_double_book()
    {
        // 12 guests click "Book" for a Standard room (3 exist) at the same instant.
        var attempts = await Task.WhenAll(Enumerable.Range(0, 12).Select(_ => Task.Run(() => Book("standard", Dec20, Dec23))));

        Assert.Equal(3, attempts.Count(a => a.Ok));
        Assert.All(attempts.Where(a => !a.Ok), a => Assert.Equal(BookingError.NoAvailability, a.Error));
        Assert.Equal(3, attempts.Where(a => a.Ok).Select(a => a.Booking!.RoomId).Distinct().Count());
    }

    [Fact]
    public async Task Database_rejects_an_overlapping_booking_even_when_the_app_is_bypassed()
    {
        var first = (await Book("vip", Dec20, Dec23)).Booking!;

        var ex = await Assert.ThrowsAsync<DbUpdateException>(() => Use(async sp =>
        {
            var db = sp.GetRequiredService<AppDbContext>();
            db.Bookings.Add(new Booking
            {
                Reference = "RT-MANUAL", ManageToken = "manual-token", RoomId = first.RoomId, RoomTypeId = first.RoomTypeId,
                CheckIn = new(2026, 12, 22), CheckOut = new(2026, 12, 24), Guests = 1,
                GuestName = "X", GuestPhone = "1", Status = BookingStatus.Confirmed,
                CreatedAt = DatabaseFixture.Start, UpdatedAt = DatabaseFixture.Start,
            });
            return await db.SaveChangesAsync();
        }));

        Assert.Equal(PostgresErrorCodes.ExclusionViolation, (ex.InnerException as PostgresException)?.SqlState);
    }

    [Theory]
    [InlineData(20, 20, BookingError.InvalidDates)]  // zero nights
    [InlineData(23, 20, BookingError.InvalidDates)]  // departure before arrival
    public async Task Invalid_dates_are_refused(int inDay, int outDay, BookingError expected)
    {
        var result = await Book("vip", new(2026, 12, inDay), new(2026, 12, outDay));
        Assert.Equal(expected, result.Error);
    }

    [Fact]
    public async Task Other_rules_are_enforced()
    {
        Assert.Equal(BookingError.ArrivalInPast, (await Book("vip", Today.AddDays(-1), Today.AddDays(1))).Error);
        Assert.Equal(BookingError.TooManyNights, (await Book("vip", Dec20, Dec20.AddDays(31))).Error);
        Assert.Equal(BookingError.TooFarAhead, (await Book("vip", Today.AddDays(400), Today.AddDays(401))).Error);
        Assert.Equal(BookingError.TooManyGuests, (await Book("vip", Dec20, Dec23, guests: 3)).Error);
        Assert.Equal(BookingError.UnknownRoomType, (await Book("penthouse", Dec20, Dec23)).Error);
        Assert.True((await Book("vip", Today, Today.AddDays(1))).Ok); // tonight is fine
    }

    [Fact]
    public async Task Inactive_rooms_are_never_offered()
    {
        await Use(sp => sp.GetRequiredService<AppDbContext>().Rooms
            .Where(r => r.Number == "STD 1").ExecuteUpdateAsync(s => s.SetProperty(r => r.IsActive, false)));

        Assert.Equal(2, Free(await Search(Dec20, Dec23), "standard"));
    }

    [Fact]
    public async Task Unpaid_pay_at_desk_booking_is_released_at_6pm_on_arrival_day()
    {
        var b = (await Book("standard", Dec20, Dec23)).Booking!;
        Assert.Equal(new DateTimeOffset(2026, 12, 20, 16, 0, 0, TimeSpan.Zero), b.HoldExpiresAt); // 18:00 Kolwezi

        fx.Time.SetUtcNow(new DateTimeOffset(2026, 12, 20, 15, 59, 0, TimeSpan.Zero));
        Assert.Equal(0, await Use(sp => sp.GetRequiredService<BookingService>().ReleaseUnpaidNoShowsAsync()));

        fx.Time.SetUtcNow(new DateTimeOffset(2026, 12, 20, 16, 0, 0, TimeSpan.Zero));
        Assert.Equal(1, await Use(sp => sp.GetRequiredService<BookingService>().ReleaseUnpaidNoShowsAsync()));
        Assert.Equal(3, Free(await Search(Dec20, Dec23), "standard"));
    }

    [Fact]
    public async Task Paid_or_checked_in_bookings_are_never_released()
    {
        var paid = (await Book("standard", Dec20, Dec23)).Booking!;
        var arrived = (await Book("standard", Dec20, Dec23)).Booking!;
        await Use(sp => sp.GetRequiredService<BookingService>().MarkPaidAsync(paid.Id));
        await Use(sp => sp.GetRequiredService<BookingService>().CheckInAsync(arrived.Id));

        fx.Time.SetUtcNow(new DateTimeOffset(2026, 12, 21, 0, 0, 0, TimeSpan.Zero));
        Assert.Equal(0, await Use(sp => sp.GetRequiredService<BookingService>().ReleaseUnpaidNoShowsAsync()));
    }

    [Fact]
    public async Task Same_day_booking_after_6pm_is_held_for_at_least_four_hours()
    {
        fx.Time.SetUtcNow(new DateTimeOffset(2026, 11, 10, 18, 0, 0, TimeSpan.Zero)); // 20:00 in Kolwezi
        var b = (await Book("vip", Today, Today.AddDays(1))).Booking!;

        Assert.Equal(new DateTimeOffset(2026, 11, 10, 22, 0, 0, TimeSpan.Zero), b.HoldExpiresAt);
    }

    [Fact]
    public async Task Online_payment_hold_frees_the_room_after_20_minutes()
    {
        for (var i = 0; i < 3; i++) await Book("standard", Dec20, Dec23, method: PaymentMethod.MobileMoney);
        Assert.Equal(0, Free(await Search(Dec20, Dec23), "standard"));

        fx.Time.Advance(TimeSpan.FromMinutes(21));

        Assert.Equal(3, Free(await Search(Dec20, Dec23), "standard")); // free at once, before the job runs
        Assert.True((await Book("standard", Dec20, Dec23)).Ok);        // and bookable (expired holds are cleared)
    }

    [Fact]
    public async Task Guest_can_cancel_until_24_hours_before_2pm_check_in()
    {
        var b = (await Book("vip", Dec20, Dec23)).Booking!;
        var bookings = () => fx.Services.CreateAsyncScope().ServiceProvider.GetRequiredService<BookingService>();

        fx.Time.SetUtcNow(new DateTimeOffset(2026, 12, 19, 12, 1, 0, TimeSpan.Zero)); // 19 Dec 14:01 Kolwezi
        Assert.Equal(BookingError.NotAllowed, (await bookings().CancelByGuestAsync(b.ManageToken)).Error);

        fx.Time.SetUtcNow(new DateTimeOffset(2026, 12, 19, 11, 59, 0, TimeSpan.Zero)); // 19 Dec 13:59 Kolwezi
        var cancelled = await bookings().CancelByGuestAsync(b.ManageToken);
        Assert.True(cancelled.Ok);
        Assert.Equal(BookingStatus.Cancelled, cancelled.Booking!.Status);
        Assert.Equal(5, Free(await Search(Dec20, Dec23), "vip"));
    }

    [Fact]
    public async Task Front_desk_status_changes_follow_the_stay()
    {
        var b = (await Book("vip", Dec20, Dec23)).Booking!;
        BookingService S(IServiceProvider sp) => sp.GetRequiredService<BookingService>();

        Assert.Equal(BookingError.NotAllowed, (await Use(sp => S(sp).CheckOutAsync(b.Id))).Error); // not in-house yet
        Assert.True((await Use(sp => S(sp).CheckInAsync(b.Id))).Ok);
        Assert.Equal(BookingError.NotAllowed, (await Use(sp => S(sp).CancelByStaffAsync(b.Id))).Error);
        Assert.True((await Use(sp => S(sp).MarkPaidAsync(b.Id))).Ok);
        var done = await Use(sp => S(sp).CheckOutAsync(b.Id));
        Assert.Equal(BookingStatus.CheckedOut, done.Booking!.Status);
        Assert.Equal(PaymentStatus.Paid, done.Booking.PaymentStatus);
        Assert.Equal(BookingError.NotFound, (await Use(sp => S(sp).CheckInAsync(999))).Error);
    }

    [Fact]
    public async Task No_show_can_be_checked_in_unless_the_room_was_re_let()
    {
        var b = (await Book("standard", Dec20, Dec23, roomId: null)).Booking!;
        await Use(sp => sp.GetRequiredService<BookingService>().MarkNoShowAsync(b.Id));

        // Room freed: staff give it to a walk-in for the same dates.
        var walkIn = await Book("standard", Dec20, Dec23, source: BookingSource.Staff, roomId: b.RoomId);
        Assert.True(walkIn.Ok);

        var late = await Use(sp => sp.GetRequiredService<BookingService>().CheckInAsync(b.Id));
        Assert.Equal(BookingError.NoAvailability, late.Error);
    }

    [Fact]
    public async Task Two_staff_editing_the_same_booking_cannot_overwrite_each_other()
    {
        var b = (await Book("vip", Dec20, Dec23)).Booking!;
        await using var scopeA = fx.Services.CreateAsyncScope();
        await using var scopeB = fx.Services.CreateAsyncScope();
        var dbA = scopeA.ServiceProvider.GetRequiredService<AppDbContext>();
        var dbB = scopeB.ServiceProvider.GetRequiredService<AppDbContext>();
        var a = await dbA.Bookings.SingleAsync(x => x.Id == b.Id);
        var c = await dbB.Bookings.SingleAsync(x => x.Id == b.Id);

        a.Notes = "Late arrival";
        await dbA.SaveChangesAsync();
        c.Notes = "Airport pickup";

        await Assert.ThrowsAsync<DbUpdateConcurrencyException>(() => dbB.SaveChangesAsync());
    }
}
