using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using Microsoft.EntityFrameworkCore;
using RachelTower.Web.Data;
using RachelTower.Web.Reservations;

namespace RachelTower.Web.Pages.Admin.Bookings;

/// <summary>All bookings, with search and filters.</summary>
public class IndexModel(AppDbContext db, HotelClock clock) : PageModel
{
    private const int PageSize = 50;

    /// <summary>Name, phone, email or reference.</summary>
    [BindProperty(SupportsGet = true)] public string? Q { get; set; }

    /// <summary>"upcoming" (default), "all", or a BookingStatus name.</summary>
    [BindProperty(SupportsGet = true)] public string Status { get; set; } = "upcoming";

    [BindProperty(SupportsGet = true)] public DateOnly? From { get; set; }
    [BindProperty(SupportsGet = true)] public DateOnly? To { get; set; }
    [BindProperty(SupportsGet = true, Name = "p")] public int PageNumber { get; set; } = 1;

    public List<Booking> Bookings { get; private set; } = [];
    public int Total { get; private set; }
    public int Pages => Math.Max(1, (Total + PageSize - 1) / PageSize);

    public async Task OnGetAsync(CancellationToken ct)
    {
        var today = clock.Today;
        var query = db.Bookings.AsNoTracking().Include(b => b.Room).Include(b => b.RoomType).AsQueryable();

        if (!string.IsNullOrWhiteSpace(Q))
        {
            var q = Q.Trim();
            var pattern = $"%{q}%";
            query = query.Where(b =>
                EF.Functions.ILike(b.GuestName, pattern) ||
                EF.Functions.ILike(b.GuestPhone, pattern) ||
                (b.GuestEmail != null && EF.Functions.ILike(b.GuestEmail, pattern)) ||
                EF.Functions.ILike(b.Reference, pattern));
        }

        var upcoming = Status == "upcoming";
        if (upcoming)
        {
            query = query.Where(b =>
                (b.Status == BookingStatus.Confirmed || b.Status == BookingStatus.PendingPayment || b.Status == BookingStatus.CheckedIn)
                && b.CheckOut >= today);
        }
        else if (Enum.TryParse<BookingStatus>(Status, out var status))
        {
            query = query.Where(b => b.Status == status);
        }

        if (From is { } from) query = query.Where(b => b.CheckIn >= from);
        if (To is { } to) query = query.Where(b => b.CheckIn <= to);

        Total = await query.CountAsync(ct);
        PageNumber = Math.Clamp(PageNumber, 1, Pages);

        query = upcoming
            ? query.OrderBy(b => b.CheckIn).ThenBy(b => b.Room.Number)
            : query.OrderByDescending(b => b.CreatedAt);

        Bookings = await query.Skip((PageNumber - 1) * PageSize).Take(PageSize).ToListAsync(ct);
    }

    public string PageUrl(int page) =>
        Url.Page("/Admin/Bookings/Index", new { Q, Status, From = From?.ToString("yyyy-MM-dd"), To = To?.ToString("yyyy-MM-dd"), p = page })!;
}
