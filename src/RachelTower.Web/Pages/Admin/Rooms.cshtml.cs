using System.ComponentModel.DataAnnotations;
using System.Globalization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Localization;
using RachelTower.Web.Data;

namespace RachelTower.Web.Pages.Admin;

/// <summary>Edit room types (names, descriptions, rates, capacity) and rooms (number, open for booking).</summary>
public class RoomsModel(AppDbContext db, IStringLocalizer<SharedResource> L) : PageModel
{
    public List<RoomType> RoomTypes { get; private set; } = [];

    public async Task OnGetAsync(CancellationToken ct) => await LoadAsync(ct);

    public async Task<IActionResult> OnPostTypeAsync(TypeInput input, CancellationToken ct)
    {
        var rate = ParseRate(input.Rate);
        if (!ModelState.IsValid || rate is null)
        {
            TempData["AdminError"] = L["Val.Summary"].Value;
            return RedirectToPage();
        }

        var type = await db.RoomTypes.FindAsync([input.Id], ct);
        if (type is null) return NotFound();

        type.NameEn = input.NameEn.Trim();
        type.NameFr = input.NameFr.Trim();
        type.DescriptionEn = input.DescriptionEn?.Trim() ?? "";
        type.DescriptionFr = input.DescriptionFr?.Trim() ?? "";
        type.NightlyRateUsd = rate.Value;
        type.MaxGuests = input.MaxGuests;
        await db.SaveChangesAsync(ct);

        TempData["AdminMessage"] = L["Admin.Saved"].Value;
        return RedirectToPage();
    }

    public async Task<IActionResult> OnPostRoomsAsync(List<RoomInput> rooms, CancellationToken ct)
    {
        var numbers = rooms.Select(r => r.Number.Trim()).ToList();
        if (numbers.Any(string.IsNullOrWhiteSpace) || numbers.Distinct(StringComparer.OrdinalIgnoreCase).Count() != numbers.Count)
        {
            TempData["AdminError"] = L["Admin.Rooms.DuplicateNumber"].Value;
            return RedirectToPage();
        }

        var existing = await db.Rooms.ToDictionaryAsync(r => r.Id, ct);
        foreach (var input in rooms)
        {
            if (!existing.TryGetValue(input.Id, out var room)) continue;
            room.Number = input.Number.Trim();
            room.IsActive = input.IsActive;
        }
        try
        {
            await db.SaveChangesAsync(ct);
        }
        catch (DbUpdateException)
        {
            // e.g. two rooms swapped numbers in one save: the unique index refuses the intermediate state.
            TempData["AdminError"] = L["Admin.Rooms.DuplicateNumber"].Value;
            return RedirectToPage();
        }

        TempData["AdminMessage"] = L["Admin.Saved"].Value;
        return RedirectToPage();
    }

    /// <summary>Accepts "150", "150.50" or "150,50" whatever the admin language.</summary>
    private static decimal? ParseRate(string? text) =>
        decimal.TryParse(text?.Trim().Replace(',', '.'), NumberStyles.Number, CultureInfo.InvariantCulture, out var value) && value is >= 0 and <= 100000
            ? value
            : null;

    private async Task LoadAsync(CancellationToken ct) =>
        RoomTypes = await db.RoomTypes.AsNoTracking()
            .Include(t => t.Rooms.OrderBy(r => r.Number))
            .OrderBy(t => t.SortOrder).ToListAsync(ct);

    public sealed class TypeInput
    {
        public int Id { get; set; }
        [Required, StringLength(80)] public string NameEn { get; set; } = "";
        [Required, StringLength(80)] public string NameFr { get; set; } = "";
        [StringLength(1000)] public string? DescriptionEn { get; set; }
        [StringLength(1000)] public string? DescriptionFr { get; set; }
        [Required] public string Rate { get; set; } = "";
        [Range(1, 10)] public int MaxGuests { get; set; }
    }

    public sealed class RoomInput
    {
        public int Id { get; set; }
        public string Number { get; set; } = "";
        public bool IsActive { get; set; }
    }
}
