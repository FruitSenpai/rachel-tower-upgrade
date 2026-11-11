using Microsoft.EntityFrameworkCore;
using RachelTower.Web.Data;

namespace RachelTower.Web.Reservations;

public sealed record RoomTypeAvailability(RoomType RoomType, int AvailableRooms, int Nights, decimal TotalUsd)
{
    public bool IsAvailable => AvailableRooms > 0;
}

public sealed class AvailabilityService(AppDbContext db, HotelClock clock)
{
    /// <summary>Bookings that occupy a room: confirmed or in-house, or an online-payment hold that hasn't run out.</summary>
    public static IQueryable<Booking> Occupying(IQueryable<Booking> bookings, DateTimeOffset now) =>
        bookings.Where(b =>
            b.Status == BookingStatus.Confirmed ||
            b.Status == BookingStatus.CheckedIn ||
            (b.Status == BookingStatus.PendingPayment && b.HoldExpiresAt > now));

    /// <summary>Active rooms with no occupying booking that overlaps [checkIn, checkOut).</summary>
    public IQueryable<Room> FreeRooms(DateOnly checkIn, DateOnly checkOut)
    {
        var busy = Occupying(db.Bookings, clock.UtcNow)
            .Where(b => b.CheckIn < checkOut && b.CheckOut > checkIn)
            .Select(b => b.RoomId);

        return db.Rooms.Where(r => r.IsActive && !busy.Contains(r.Id));
    }

    /// <summary>Every room type with how many rooms are free for the dates and the total price.</summary>
    public async Task<IReadOnlyList<RoomTypeAvailability>> SearchAsync(DateOnly checkIn, DateOnly checkOut, CancellationToken ct = default)
    {
        var nights = checkOut.DayNumber - checkIn.DayNumber;
        var freeByType = await FreeRooms(checkIn, checkOut)
            .GroupBy(r => r.RoomTypeId)
            .Select(g => new { RoomTypeId = g.Key, Count = g.Count() })
            .ToDictionaryAsync(x => x.RoomTypeId, x => x.Count, ct);

        var types = await db.RoomTypes.AsNoTracking().OrderBy(t => t.SortOrder).ThenBy(t => t.Id).ToListAsync(ct);
        return types
            .Select(t => new RoomTypeAvailability(t, freeByType.GetValueOrDefault(t.Id), nights, t.NightlyRateUsd * nights))
            .ToList();
    }
}
