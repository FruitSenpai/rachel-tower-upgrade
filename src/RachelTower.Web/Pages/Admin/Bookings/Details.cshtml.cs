using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Localization;
using RachelTower.Web.Data;
using RachelTower.Web.Localization;
using RachelTower.Web.Reservations;

namespace RachelTower.Web.Pages.Admin.Bookings;

/// <summary>One booking, with the front desk actions that make sense for its status.</summary>
public class DetailsModel(AppDbContext db, BookingService bookings, BookingText text, IStringLocalizer<SharedResource> L) : PageModel
{
    [BindProperty(SupportsGet = true)] public int Id { get; set; }

    public Booking Booking { get; private set; } = null!;
    public string GuestLink { get; private set; } = "";

    public async Task<IActionResult> OnGetAsync(CancellationToken ct)
    {
        var booking = await db.Bookings.AsNoTracking().Include(b => b.Room).Include(b => b.RoomType)
            .FirstOrDefaultAsync(b => b.Id == Id, ct);
        if (booking is null) return NotFound();

        Booking = booking;
        GuestLink = Url.PageLink("/ManageBooking", values: new { culture = booking.Culture, token = booking.ManageToken })!;
        return Page();
    }

    public Task<IActionResult> OnPostCheckInAsync(string? returnUrl, CancellationToken ct) => Act(bookings.CheckInAsync(Id, ct), returnUrl);
    public Task<IActionResult> OnPostCheckOutAsync(string? returnUrl, CancellationToken ct) => Act(bookings.CheckOutAsync(Id, ct), returnUrl);
    public Task<IActionResult> OnPostMarkPaidAsync(string? returnUrl, CancellationToken ct) => Act(bookings.MarkPaidAsync(Id, ct), returnUrl);
    public Task<IActionResult> OnPostNoShowAsync(string? returnUrl, CancellationToken ct) => Act(bookings.MarkNoShowAsync(Id, ct), returnUrl);
    public Task<IActionResult> OnPostCancelAsync(string? returnUrl, CancellationToken ct) => Act(bookings.CancelByStaffAsync(Id, ct), returnUrl);
    public Task<IActionResult> OnPostNotesAsync(string? notes, CancellationToken ct) => Act(bookings.UpdateNotesAsync(Id, notes, ct), null);

    private async Task<IActionResult> Act(Task<BookingResult> action, string? returnUrl)
    {
        var result = await action;
        TempData[result.Ok ? "AdminMessage" : "AdminError"] = result.Ok ? L["Admin.Saved"].Value : text.Error(result.Error);
        return LocalRedirect(AdminUi.SafeReturn(returnUrl, $"/admin/bookings/{Id}"));
    }
}
