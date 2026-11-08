using System.ComponentModel.DataAnnotations;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using RachelTower.Web.Data;
using RachelTower.Web.Localization;
using RachelTower.Web.Reservations;

namespace RachelTower.Web.Pages;

/// <summary>Step 2 of booking: guest details and payment choice, then the booking is created.</summary>
public class BookDetailsModel(
    AvailabilityService availability,
    BookingService bookings,
    BookingNotifier notifier,
    BookingText text) : PageModel
{
    [BindProperty(SupportsGet = true)] public string Type { get; set; } = "";
    [BindProperty(SupportsGet = true)] public DateOnly CheckIn { get; set; }
    [BindProperty(SupportsGet = true)] public DateOnly CheckOut { get; set; }
    [BindProperty(SupportsGet = true)] public int Guests { get; set; } = 2;

    [BindProperty] public GuestInput Input { get; set; } = new();

    public RoomTypeAvailability Offer { get; private set; } = null!;
    public DateTimeOffset CancellationDeadline { get; private set; }

    public async Task<IActionResult> OnGetAsync(CancellationToken ct) =>
        await LoadOfferAsync(ct) ?? Page();

    public async Task<IActionResult> OnPostAsync(CancellationToken ct)
    {
        if (await LoadOfferAsync(ct) is { } redirect) return redirect;
        if (!ModelState.IsValid) return Page();

        var result = await bookings.CreateAsync(new CreateBookingRequest(
            Type, CheckIn, CheckOut, Guests, Input.Name, Input.Phone, Input.Email, Input.Notes, SiteCultures.Current), ct);

        if (!result.Ok)
        {
            return BackToSearch(text.Error(result.Error));
        }

        var booking = result.Booking!;
        var manageUrl = Url.PageLink("/ManageBooking", values: new { culture = SiteCultures.Current, token = booking.ManageToken })!;
        await notifier.BookingCreatedAsync(booking, manageUrl, ct);

        TempData["BookingCreated"] = true;
        return Redirect(manageUrl);
    }

    /// <summary>Loads the chosen room type for the dates; sends the guest back to the search if it no longer fits.</summary>
    private async Task<IActionResult?> LoadOfferAsync(CancellationToken ct)
    {
        var error = bookings.ValidateStay(CheckIn, CheckOut);
        if (error != BookingError.None) return BackToSearch(text.Error(error));

        var offer = (await availability.SearchAsync(CheckIn, CheckOut, ct)).FirstOrDefault(r => r.RoomType.Code == Type);
        if (offer is null) return BackToSearch(text.Error(BookingError.UnknownRoomType));
        if (!offer.IsAvailable) return BackToSearch(text.Error(BookingError.NoAvailability));
        if (Guests < 1 || Guests > offer.RoomType.MaxGuests) return BackToSearch(text.Error(BookingError.TooManyGuests));

        Offer = offer;
        CancellationDeadline = bookings.CancellationDeadline(new Booking
        {
            Reference = "", ManageToken = "", GuestName = "", GuestPhone = "", CheckIn = CheckIn, CheckOut = CheckOut,
        });
        return null;
    }

    private RedirectResult BackToSearch(string message)
    {
        TempData["Message"] = message;
        return Redirect(Url.PageIn("/Book", values: new
        {
            checkIn = CheckIn.ToString("yyyy-MM-dd"),
            checkOut = CheckOut.ToString("yyyy-MM-dd"),
            guests = Guests,
        }));
    }

    public sealed class GuestInput
    {
        [Display(Name = "Details.Name")]
        [Required(ErrorMessage = "Val.Required")]
        [StringLength(100, ErrorMessage = "Val.StringLength")]
        public string Name { get; set; } = "";

        [Display(Name = "Details.Phone")]
        [Required(ErrorMessage = "Val.Required")]
        [StringLength(30, ErrorMessage = "Val.StringLength")]
        [RegularExpression(@"^\+?[0-9 ()\-.]{6,}$", ErrorMessage = "Val.Phone")]
        public string Phone { get; set; } = "";

        [Display(Name = "Details.Email")]
        [EmailAddress(ErrorMessage = "Val.Email")]
        [StringLength(200, ErrorMessage = "Val.StringLength")]
        public string? Email { get; set; }

        [Display(Name = "Details.Notes")]
        [StringLength(1000, ErrorMessage = "Val.StringLength")]
        public string? Notes { get; set; }
    }
}
