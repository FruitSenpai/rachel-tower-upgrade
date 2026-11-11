namespace RachelTower.Web.Localization;

/// <summary>
/// Localized URL slugs for every public page, e.g. /en/amenities and /fr/services.
/// Add a line here when you add a page; the route convention picks it up automatically.
/// </summary>
public static class SiteRoutes
{
    public sealed record Slugs(string En, string Fr)
    {
        public string For(string culture) => culture == SiteCultures.French ? Fr : En;
    }

    public static readonly IReadOnlyDictionary<string, Slugs> Pages = new Dictionary<string, Slugs>
    {
        ["/Index"] = new("", ""),
        ["/Explore"] = new("explore", "decouvrir"),
        ["/History"] = new("history", "notre-histoire"),
        ["/Amenities"] = new("amenities", "services"),
        ["/Events"] = new("meetings-and-events", "reunions-et-evenements"),
        ["/Book"] = new("book", "reserver"),
        ["/Contact"] = new("contact", "contact"),
        ["/BookDetails"] = new("book/details", "reserver/details"),
        ["/ManageBooking"] = new("booking/{token}", "reservation/{token}"),
    };

    /// <summary>Pages kept out of the sitemap and search results (booking steps, private booking pages).</summary>
    public static readonly ISet<string> NotIndexed = new HashSet<string> { "/BookDetails", "/ManageBooking" };
}
