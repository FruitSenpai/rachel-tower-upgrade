namespace RachelTower.Web.Infrastructure;

/// <summary>
/// Serves a photo from wwwroot/images when it has been downloaded,
/// otherwise from Wix's CDN resized to the requested width.
/// </summary>
public sealed class ImageResolver(IWebHostEnvironment env)
{
    private readonly string _imagesFolder = Path.Combine(env.WebRootPath ?? "wwwroot", "images");

    public string Src(SiteImage image, int width = 1600)
    {
        if (File.Exists(Path.Combine(_imagesFolder, image.FileName)))
        {
            return $"/images/{image.FileName}";
        }

        // Wix "fit" keeps the aspect ratio inside a width x (2*width) box.
        return $"{image.WixUrl}/v1/fit/w_{width},h_{width * 2},q_85/{image.FileName}";
    }
}
