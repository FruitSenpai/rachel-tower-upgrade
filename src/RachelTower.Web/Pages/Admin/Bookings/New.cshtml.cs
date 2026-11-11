using System.ComponentModel.DataAnnotations;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Localization;
using RachelTower.Web.Data;
using RachelTower.Web.Localization;
using RachelTower.Web.Reservations;

namespace RachelTower.Web.Pages.Admin.Bookings;

/// <summary>Booking made by staff: phone, WhatsApp, walk-in, or copied from Booking.com / Airbnb.</summary>
public class NewModel(
    AppDbContext db,
    BookingService bookings,
    BookingNotifier notifier,
    BookingText text,
    HotelClock clock,
    IStringLocalizer<SharedResource> L) : PageModel
{
    [BindProperty] public NewBookingInput Input { get; set; } = new();

    public List<RoomType> RoomTypes { get; private set; } = [];
    public List<Room> Rooms { get; private set; } = [];

    public async Task OnGetAsync(CancellationToken ct)
    {
        await LoadAsync(ct);
        Input.CheckIn = clock.Today;
        Input.CheckOut = clock.Today.AddDays(1);
        Input.RoomTypeCode = RoomTypes.FirstOrDefault()?.Code ?? "";
    }

    public async Task<IActionResult> OnPostAsync(CancellationToken ct)
    {
        await LoadAsync(ct);
        if (!ModelState.IsValid) return Page();

        var result = await bookings.CreateAsync(new CreateBookingRequest(
            Input.RoomTypeCode, Input.CheckIn, Input.CheckOut, Input.Guests,
            Input.Name, Input.Phone, Input.Email, Input.Notes, Input.Language,
            PaymentMethod.PayAtDesk, Input.Source, Input.RoomId, Input.AlreadyPaid), ct);

        if (!result.Ok)
        {
            ModelState.AddModelError(string.Empty, text.Error(result.Error));
            return Page();
        }

        var booking = result.Booking!;
        var manageUrl = Url.PageLink("/ManageBooking", values: new { culture = booking.Culture, token = booking.ManageToken })!;
        await notifier.BookingCreatedAsync(booking, manageUrl, ct, notifyHotel: false);

        TempData["AdminMessage"] = string.Format(L["Admin.New.Created"], booking.Reference);
        return LocalRedirect($"/admin/bookings/{booking.Id}");
    }

    private async Task LoadAsync(CancellationToken ct)
    {
        RoomTypes = await db.RoomTypes.AsNoTracking().OrderBy(t => t.SortOrder).ToListAsync(ct);
        Rooms = await db.Rooms.AsNoTracking().Include(r => r.RoomType).Where(r => r.IsActive)
            .OrderBy(r => r.RoomType.SortOrder).ThenBy(r => r.Number).ToListAsync(ct);
    }

    public sealed class NewBookingInput
    {
        [Display(Name = "Admin.New.RoomType")]
        [Required(ErrorMessage = "Val.Required")]
        public string RoomTypeCode { get; set; } = "";

        [Display(Name = "Admin.New.Room")]
        public int? RoomId { get; set; }

        [Display(Name = "Book.CheckIn")]
        [Required(ErrorMessage = "Val.Required")]
        public DateOnly CheckIn { get; set; }

        [Display(Name = "Book.CheckOut")]
        [Required(ErrorMessage = "Val.Required")]
        public DateOnly CheckOut { get; set; }

        [Display(Name = "Book.Guests")]
        [Range(1, 10, ErrorMessage = "Val.Range")]
        public int Guests { get; set; } = 1;

        [Display(Name = "Details.Name")]
        [Required(ErrorMessage = "Val.Required")]
        [StringLength(100, ErrorMessage = "Val.StringLength")]
        public string Name { get; set; } = "";

        [Display(Name = "Details.Phone")]
        [Required(ErrorMessage = "Val.Required")]
        [StringLength(30, ErrorMessage = "Val.StringLength")]
        public string Phone { get; set; } = "";

        [Display(Name = "Booking.Email")]
        [EmailAddress(ErrorMessage = "Val.Email")]
        public string? Email { get; set; }

        [Display(Name = "Booking.Notes")]
        [StringLength(2000, ErrorMessage = "Val.StringLength")]
        public string? Notes { get; set; }

        [Display(Name = "Admin.New.Source")]
        public BookingSource Source { get; set; } = BookingSource.Staff;

        [Display(Name = "Admin.Booking.Language")]
        public string Language { get; set; } = SiteCultures.French;

        [Display(Name = "Admin.New.Paid")]
        public bool AlreadyPaid { get; set; }
    }
}
