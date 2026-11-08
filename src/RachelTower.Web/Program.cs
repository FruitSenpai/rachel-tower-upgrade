using System.Globalization;
using System.Text;
using System.Text.Encodings.Web;
using System.Text.Unicode;
using Microsoft.Extensions.WebEncoders;
using Microsoft.AspNetCore.Diagnostics;
using Microsoft.AspNetCore.Localization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Localization.Routing;
using Microsoft.Extensions.Options;
using RachelTower.Web.Infrastructure;
using RachelTower.Web.Localization;
using RachelTower.Web.Services;

var builder = WebApplication.CreateBuilder(args);

builder.Services.Configure<HotelOptions>(builder.Configuration.GetSection(HotelOptions.Section));
builder.Services.Configure<SmtpOptions>(builder.Configuration.GetSection(SmtpOptions.Section));
builder.Services.AddSingleton<ImageResolver>();
builder.Services.AddSingleton(TimeProvider.System);
// Output French accents as-is instead of HTML entities (é rather than &#xE9;).
builder.Services.Configure<WebEncoderOptions>(o => o.TextEncoderSettings = new TextEncoderSettings(UnicodeRanges.All));
builder.Services.Configure<MvcOptions>(o => o.SuppressImplicitRequiredAttributeForNonNullableReferenceTypes = true);

// Contact form: email the hotel when SMTP is configured, otherwise just log.
builder.Services.AddTransient<IContactMessageSender>(sp =>
    sp.GetRequiredService<IOptions<SmtpOptions>>().Value.IsConfigured
        ? ActivatorUtilities.CreateInstance<SmtpContactMessageSender>(sp)
        : ActivatorUtilities.CreateInstance<LoggingContactMessageSender>(sp));

// English / French: the language comes from the first URL segment (/en/..., /fr/...).
builder.Services.AddLocalization(o => o.ResourcesPath = "Resources");
builder.Services.Configure<RequestLocalizationOptions>(o =>
{
    var cultures = SiteCultures.All.Select(c => new CultureInfo(c)).ToList();
    o.DefaultRequestCulture = new RequestCulture(SiteCultures.Default);
    o.SupportedCultures = cultures;
    o.SupportedUICultures = cultures;
    o.RequestCultureProviders.Clear();
    o.RequestCultureProviders.Add(new RouteDataRequestCultureProvider
    {
        RouteDataStringKey = "culture",
        UIRouteDataStringKey = "culture",
    });
    // Error pages are re-executed at /Error/{code}: keep the language of the URL that failed.
    o.RequestCultureProviders.Add(new CustomRequestCultureProvider(ctx =>
    {
        var path = ctx.Features.Get<IStatusCodeReExecuteFeature>()?.OriginalPath
                   ?? ctx.Features.Get<IExceptionHandlerPathFeature>()?.Path;
        var segment = path?.Split('/', StringSplitOptions.RemoveEmptyEntries).FirstOrDefault();
        return Task.FromResult(SiteCultures.IsSupported(segment)
            ? new ProviderCultureResult(segment!.ToLowerInvariant())
            : null);
    }));
});

builder.Services
    .AddRazorPages(o => o.Conventions.Add(new LocalizedPageRouteConvention()))
    .AddViewLocalization()
    .AddDataAnnotationsLocalization(o =>
        o.DataAnnotationLocalizerProvider = (_, factory) => factory.Create(typeof(RachelTower.Web.SharedResource)));

var app = builder.Build();

if (!app.Environment.IsDevelopment())
{
    app.UseExceptionHandler("/Error");
    app.UseHsts();
}

app.UseStatusCodePagesWithReExecute("/Error/{0}");
app.UseRouting();
app.UseRequestLocalization();
app.UseAuthorization();

app.MapStaticAssets();
app.MapRazorPages().WithStaticAssets();

// "/" sends visitors to their browser's language, English by default.
app.MapGet("/", (HttpContext ctx) =>
{
    var preferred = ctx.Request.GetTypedHeaders().AcceptLanguage
        .OrderByDescending(l => l.Quality ?? 1)
        .Select(l => l.Value.Value?.Split('-')[0].ToLowerInvariant())
        .FirstOrDefault(SiteCultures.IsSupported);
    return Results.Redirect($"/{preferred ?? SiteCultures.Default}");
});

app.MapGet("/robots.txt", (HttpContext ctx) =>
    Results.Text($"User-agent: *\nAllow: /\nSitemap: {ctx.Request.Scheme}://{ctx.Request.Host}/sitemap.xml\n", "text/plain"));

app.MapGet("/sitemap.xml", (HttpContext ctx) =>
{
    var root = $"{ctx.Request.Scheme}://{ctx.Request.Host}";
    var xml = new StringBuilder("""<?xml version="1.0" encoding="UTF-8"?><urlset xmlns="http://www.sitemaps.org/schemas/sitemap/0.9" xmlns:xhtml="http://www.w3.org/1999/xhtml">""");
    foreach (var slugs in SiteRoutes.Pages.Values)
    {
        foreach (var culture in SiteCultures.All)
        {
            xml.Append($"<url><loc>{Url(culture)}</loc>");
            foreach (var alt in SiteCultures.All)
            {
                xml.Append($"""<xhtml:link rel="alternate" hreflang="{alt}" href="{Url(alt)}"/>""");
            }
            xml.Append("</url>");
        }

        string Url(string c) => slugs.For(c) is "" ? $"{root}/{c}" : $"{root}/{c}/{slugs.For(c)}";
    }
    xml.Append("</urlset>");
    return Results.Content(xml.ToString(), "application/xml");
});

app.Run();
