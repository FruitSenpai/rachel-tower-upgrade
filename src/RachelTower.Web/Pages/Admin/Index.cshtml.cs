using Microsoft.AspNetCore.Mvc.RazorPages;
using Microsoft.EntityFrameworkCore;
using RachelTower.Web.Data;
using RachelTower.Web.Reservations;

namespace RachelTower.Web.Pages.Admin;

/// <summary>Front desk view of today: who's arriving, who's leaving, who's in the house.</summary>
public class IndexModel(AppDbContext db, HotelClock clock) : PageModel
{
    public DateOnly Today { get; private set; }
    public List<Booking> Arrivals { get; private set; } = [];
    public List<Booking> Departures { get; private set; } = [];
    public List<Booking> InHouse { get; private set; } = [];
    public int OccupiedTonight { get; private set; }
    public int ActiveRooms { get; private set; }

    public async Task OnGetAsync(CancellationToken ct)
    {
        Today = clock.Today;
        var today = Today;
        var bookings = db.Bookings.AsNoTracking().Include(b => b.Room).Include(b => b.RoomType);

        Arrivals = await bookings
            .Where(b => b.CheckIn == today && (b.Status == BookingStatus.Confirmed || b.Status == BookingStatus.PendingPayment || b.Status == BookingStatus.CheckedIn))
            .OrderBy(b => b.Status).ThenBy(b => b.Room.Number).ToListAsync(ct);

        Departures = await bookings
            .Where(b => b.CheckOut == today && (b.Status == BookingStatus.CheckedIn || b.Status == BookingStatus.CheckedOut))
            .OrderBy(b => b.Status).ThenBy(b => b.Room.Number).ToListAsync(ct);

        InHouse = await bookings
            .Where(b => b.Status == BookingStatus.CheckedIn)
            .OrderBy(b => b.Room.Number).ToListAsync(ct);

        OccupiedTonight = await AvailabilityService.Occupying(db.Bookings, clock.UtcNow)
            .CountAsync(b => b.CheckIn <= today && b.CheckOut > today, ct);
        ActiveRooms = await db.Rooms.CountAsync(r => r.IsActive, ct);
    }
}
