using Microsoft.AspNetCore.Mvc.ApplicationModels;

namespace RachelTower.Web.Localization;

/// <summary>
/// Gives each page listed in <see cref="SiteRoutes"/> one route per language:
/// "/Explore" becomes /en/explore and /fr/decouvrir. The {culture} route value
/// is what the request localization middleware reads to pick the language.
/// </summary>
public sealed class LocalizedPageRouteConvention : IPageRouteModelConvention
{
    public void Apply(PageRouteModel model)
    {
        if (!SiteRoutes.Pages.TryGetValue(model.ViewEnginePath, out var slugs))
        {
            return; // pages not in the map (e.g. /Error) keep their default route
        }

        model.Selectors.Clear();
        foreach (var culture in SiteCultures.All)
        {
            var slug = slugs.For(culture);
            var prefix = $"{{culture:regex(^{culture}$)}}";
            model.Selectors.Add(new SelectorModel
            {
                AttributeRouteModel = new AttributeRouteModel
                {
                    Template = slug.Length == 0 ? prefix : $"{prefix}/{slug}",
                },
            });
        }
    }
}
