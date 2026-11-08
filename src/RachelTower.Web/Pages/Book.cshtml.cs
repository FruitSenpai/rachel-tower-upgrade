using System.ComponentModel.DataAnnotations;
using System.Globalization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using Microsoft.Extensions.Localization;
using Microsoft.Extensions.Options;
using RachelTower.Web.Infrastructure;

namespace RachelTower.Web.Pages;

/// <summary>
/// Step 1 booking page: collects dates and sends the request to the front desk on WhatsApp.
/// Step 2 replaces this with the live booking engine (availability, mobile money, pay at the desk).
/// </summary>
public class BookModel(IOptions<HotelOptions> hotel, IStringLocalizer<SharedResource> L, TimeProvider clock) : PageModel
{
    [BindProperty]
    public BookingRequest Input { get; set; } = new();

    public void OnGet()
    {
        var today = Today;
        Input.CheckIn = today.AddDays(1);
        Input.CheckOut = today.AddDays(2);
    }

    public IActionResult OnPost()
    {
        if (Input.CheckIn < Today)
        {
            ModelState.AddModelError("Input.CheckIn", L["Val.ArrivalInPast"]);
        }
        if (Input.CheckOut <= Input.CheckIn)
        {
            ModelState.AddModelError("Input.CheckOut", L["Val.DepartureAfterArrival"]);
        }
        if (!ModelState.IsValid)
        {
            return Page();
        }

        var culture = CultureInfo.CurrentUICulture;
        var message = string.Format(
            L["Book.WhatsAppMessage"],
            Input.CheckIn.ToString("D", culture),
            Input.CheckOut.ToString("D", culture),
            Input.Guests,
            Input.Name.Trim());

        return Redirect(hotel.Value.WhatsAppUrl(message));
    }

    // Kolwezi is UTC+2 all year.
    private DateOnly Today => DateOnly.FromDateTime(clock.GetUtcNow().UtcDateTime.AddHours(2));

    public sealed class BookingRequest
    {
        [Display(Name = "Book.CheckIn")]
        [Required(ErrorMessage = "Val.Required")]
        public DateOnly CheckIn { get; set; }

        [Display(Name = "Book.CheckOut")]
        [Required(ErrorMessage = "Val.Required")]
        public DateOnly CheckOut { get; set; }

        [Display(Name = "Book.Guests")]
        [Range(1, 10, ErrorMessage = "Val.Range")]
        public int Guests { get; set; } = 2;

        [Display(Name = "Book.Name")]
        [Required(ErrorMessage = "Val.Required")]
        [StringLength(100, ErrorMessage = "Val.StringLength")]
        public string Name { get; set; } = "";
    }
}
