using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using RachelTower.Web.Data;
using RachelTower.Web.Reservations;

namespace RachelTower.Web.Pages;

/// <summary>
/// The guest's private booking page, reached through the secret link in the confirmation.
/// Shows the booking and lets the guest cancel before the deadline.
/// </summary>
public class ManageBookingModel(BookingService bookings, BookingNotifier notifier, BookingText text) : PageModel
{
    [BindProperty(SupportsGet = true)]
    public string Token { get; set; } = "";

    public Booking Booking { get; private set; } = null!;
    public bool CanCancel { get; private set; }
    public DateTimeOffset CancellationDeadline { get; private set; }
    public bool JustCreated { get; private set; }

    [TempData]
    public string? Message { get; set; }

    public async Task<IActionResult> OnGetAsync(CancellationToken ct)
    {
        if (!await LoadAsync(ct)) return NotFound();
        JustCreated = TempData["BookingCreated"] is true;
        return Page();
    }

    public async Task<IActionResult> OnPostCancelAsync(bool confirm, CancellationToken ct)
    {
        if (!await LoadAsync(ct)) return NotFound();
        if (!confirm) return RedirectToPage();

        var result = await bookings.CancelByGuestAsync(Token, ct);
        if (result.Ok)
        {
            await notifier.BookingCancelledAsync(result.Booking!, ct);
            Message = "cancelled";
        }
        else
        {
            Message = text.Error(result.Error);
        }
        return RedirectToPage();
    }

    private async Task<bool> LoadAsync(CancellationToken ct)
    {
        if (string.IsNullOrWhiteSpace(Token) || Token.Length > 64) return false;
        var booking = await bookings.FindByTokenAsync(Token, ct);
        if (booking is null) return false;

        Booking = booking;
        CanCancel = bookings.GuestCanCancel(booking);
        CancellationDeadline = bookings.CancellationDeadline(booking);
        return true;
    }
}
