using Microsoft.AspNetCore.Mvc;

namespace RachelTower.Web.Localization;

public static class LocalizedUrlExtensions
{
    /// <summary>URL of a page in the current language (or the one given).</summary>
    public static string PageIn(this IUrlHelper url, string page, string? culture = null, object? values = null)
    {
        var routeValues = new RouteValueDictionary(values) { ["culture"] = culture ?? SiteCultures.Current };
        return url.Page(page, routeValues) ?? "/";
    }

    /// <summary>Absolute URL of a page, used for canonical and hreflang tags.</summary>
    public static string AbsolutePageIn(this IUrlHelper url, string page, string culture)
    {
        var request = url.ActionContext.HttpContext.Request;
        return url.Page(page, null, new { culture }, request.Scheme, request.Host.Value) ?? "/";
    }
}
