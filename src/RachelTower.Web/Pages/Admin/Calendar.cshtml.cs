using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using Microsoft.EntityFrameworkCore;
using RachelTower.Web.Data;
using RachelTower.Web.Reservations;

namespace RachelTower.Web.Pages.Admin;

/// <summary>Two-week occupancy grid: one row per room, one column per night.</summary>
public class CalendarModel(AppDbContext db, HotelClock clock) : PageModel
{
    public const int Days = 14;

    private static readonly BookingStatus[] Shown =
        [BookingStatus.PendingPayment, BookingStatus.Confirmed, BookingStatus.CheckedIn, BookingStatus.CheckedOut];

    [BindProperty(SupportsGet = true)] public DateOnly? Start { get; set; }

    public DateOnly From { get; private set; }
    public DateOnly Today { get; private set; }
    public List<Room> Rooms { get; private set; } = [];

    /// <summary>Per room: the bookings visible in the window, in date order.</summary>
    public Dictionary<int, List<Booking>> ByRoom { get; private set; } = [];

    public IEnumerable<DateOnly> Nights => Enumerable.Range(0, Days).Select(From.AddDays);

    public async Task OnGetAsync(CancellationToken ct)
    {
        Today = clock.Today;
        From = Start ?? Today;
        var to = From.AddDays(Days);
        var from = From;

        Rooms = await db.Rooms.AsNoTracking().Include(r => r.RoomType)
            .OrderBy(r => r.RoomType.SortOrder).ThenBy(r => r.Number).ToListAsync(ct);

        var bookings = await db.Bookings.AsNoTracking()
            .Where(b => Shown.Contains(b.Status) && b.CheckIn < to && b.CheckOut > from)
            .OrderBy(b => b.CheckIn)
            .ToListAsync(ct);

        ByRoom = bookings.GroupBy(b => b.RoomId).ToDictionary(g => g.Key, g => g.ToList());
    }
}
