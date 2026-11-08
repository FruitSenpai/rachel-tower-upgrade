using RachelTower.Web.Infrastructure;

namespace RachelTower.Web.Pages.Shared;

public sealed record PageHeaderModel(string Eyebrow, string Heading, SiteImage Image);

/// <summary>A photo plus its (already localized) description, for galleries.</summary>
public sealed record GalleryItem(SiteImage Image, string Alt);
