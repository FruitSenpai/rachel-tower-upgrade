using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using RachelTower.Web.Reservations;

namespace RachelTower.Web.Pages;

/// <summary>Step 1 of booking: pick dates and see which rooms are free, with prices.</summary>
public class BookModel(AvailabilityService availability, BookingService bookings, BookingText text, HotelClock clock) : PageModel
{
    [BindProperty(SupportsGet = true)]
    public DateOnly? CheckIn { get; set; }

    [BindProperty(SupportsGet = true)]
    public DateOnly? CheckOut { get; set; }

    [BindProperty(SupportsGet = true)]
    public int Guests { get; set; } = 2;

    /// <summary>Set when the details step sends the guest back (e.g. the room was just taken).</summary>
    [TempData]
    public string? Message { get; set; }

    public string? SearchError { get; private set; }
    public IReadOnlyList<RoomTypeAvailability>? Results { get; private set; }
    public DateOnly Today { get; private set; }

    public async Task OnGetAsync(CancellationToken ct)
    {
        Today = clock.Today;
        Guests = Math.Clamp(Guests, 1, 10);

        if (CheckIn is null || CheckOut is null)
        {
            CheckIn ??= Today;
            CheckOut ??= CheckIn.Value.AddDays(1);
            return; // first visit: show the form only
        }

        var error = bookings.ValidateStay(CheckIn.Value, CheckOut.Value);
        if (error != BookingError.None)
        {
            SearchError = text.Error(error);
            return;
        }

        Results = await availability.SearchAsync(CheckIn.Value, CheckOut.Value, ct);
    }
}
