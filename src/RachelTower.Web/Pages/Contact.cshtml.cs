using System.ComponentModel.DataAnnotations;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using RachelTower.Web.Localization;
using RachelTower.Web.Services;

namespace RachelTower.Web.Pages;

public class ContactModel(IContactMessageSender sender, ILogger<ContactModel> logger) : PageModel
{
    public static readonly string[] Topics = ["Reservation", "Event", "Wedding", "Catering", "Other"];

    [BindProperty]
    public ContactInput Input { get; set; } = new();

    /// <summary>Hidden "website" field: real visitors leave it empty, spam bots fill it in.</summary>
    [BindProperty]
    public string? Website { get; set; }

    [TempData]
    public string? Status { get; set; }

    public void OnGet(string? topic)
    {
        if (topic is not null && Topics.Contains(topic))
        {
            Input.Topic = topic;
        }
    }

    public async Task<IActionResult> OnPostAsync(CancellationToken ct)
    {
        if (!string.IsNullOrEmpty(Website))
        {
            Status = "sent"; // pretend success so bots don't retry
            return RedirectToPage();
        }

        if (!Topics.Contains(Input.Topic))
        {
            Input.Topic = "Other";
        }

        if (!ModelState.IsValid)
        {
            return Page();
        }

        try
        {
            await sender.SendAsync(new ContactMessage(
                Input.Name.Trim(), Input.Email.Trim(), Input.Phone?.Trim(), Input.Topic, Input.Message.Trim(), SiteCultures.Current), ct);
            Status = "sent";
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Failed to send contact message from {Email}", Input.Email);
            Status = "failed";
            return Page();
        }

        // Post/Redirect/Get so refreshing doesn't resend the message.
        return RedirectToPage();
    }

    public sealed class ContactInput
    {
        [Display(Name = "Contact.Name")]
        [Required(ErrorMessage = "Val.Required")]
        [StringLength(100, ErrorMessage = "Val.StringLength")]
        public string Name { get; set; } = "";

        [Display(Name = "Contact.Email")]
        [Required(ErrorMessage = "Val.Required")]
        [EmailAddress(ErrorMessage = "Val.Email")]
        [StringLength(200, ErrorMessage = "Val.StringLength")]
        public string Email { get; set; } = "";

        [Display(Name = "Contact.Phone")]
        [StringLength(30, ErrorMessage = "Val.StringLength")]
        public string? Phone { get; set; }

        [Display(Name = "Contact.Topic")]
        public string Topic { get; set; } = "Reservation";

        [Display(Name = "Contact.Message")]
        [Required(ErrorMessage = "Val.Required")]
        [StringLength(4000, ErrorMessage = "Val.StringLength")]
        public string Message { get; set; } = "";
    }
}
