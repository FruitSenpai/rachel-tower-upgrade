using System.ComponentModel.DataAnnotations;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using Microsoft.Extensions.Localization;

namespace RachelTower.Web.Pages.Admin;

public class LoginModel(SignInManager<IdentityUser> signIn, IStringLocalizer<SharedResource> L, ILogger<LoginModel> logger) : PageModel
{
    [BindProperty] public LoginInput Input { get; set; } = new();
    [BindProperty(SupportsGet = true)] public string? ReturnUrl { get; set; }

    public string? Error { get; private set; }

    public IActionResult OnGet() =>
        User.Identity?.IsAuthenticated == true ? LocalRedirect(AdminUi.SafeReturn(ReturnUrl, "/admin")) : Page();

    public async Task<IActionResult> OnPostAsync()
    {
        if (!ModelState.IsValid) return Page();

        var result = await signIn.PasswordSignInAsync(Input.Email.Trim(), Input.Password, Input.Remember, lockoutOnFailure: true);
        if (result.Succeeded)
        {
            logger.LogInformation("Staff sign-in: {Email}", Input.Email);
            return LocalRedirect(AdminUi.SafeReturn(ReturnUrl, "/admin"));
        }

        logger.LogWarning("Failed staff sign-in for {Email} (locked out: {LockedOut})", Input.Email, result.IsLockedOut);
        Error = result.IsLockedOut ? L["Admin.Login.LockedOut"] : L["Admin.Login.Failed"];
        return Page();
    }

    public async Task<IActionResult> OnPostLogoutAsync()
    {
        await signIn.SignOutAsync();
        return LocalRedirect("/admin/login");
    }

    public sealed class LoginInput
    {
        [Display(Name = "Admin.Login.Email")]
        [Required(ErrorMessage = "Val.Required")]
        [EmailAddress(ErrorMessage = "Val.Email")]
        public string Email { get; set; } = "";

        [Display(Name = "Admin.Login.Password")]
        [Required(ErrorMessage = "Val.Required")]
        [DataType(DataType.Password)]
        public string Password { get; set; } = "";

        [Display(Name = "Admin.Login.Remember")]
        public bool Remember { get; set; }
    }
}
