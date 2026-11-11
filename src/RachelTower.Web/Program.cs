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
using RachelTower.Web.Data;
using RachelTower.Web.Reservations;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;

var builder = WebApplication.CreateBuilder(args);

builder.Services.Configure<HotelOptions>(builder.Configuration.GetSection(HotelOptions.Section));
builder.Services.Configure<SmtpOptions>(builder.Configuration.GetSection(SmtpOptions.Section));
builder.Services.AddSingleton<ImageResolver>();
builder.Services.AddSingleton(TimeProvider.System);
// Output French accents as-is instead of HTML entities (é rather than &#xE9;).
builder.Services.Configure<WebEncoderOptions>(o => o.TextEncoderSettings = new TextEncoderSettings(UnicodeRanges.All));
builder.Services.Configure<MvcOptions>(o => o.SuppressImplicitRequiredAttributeForNonNullableReferenceTypes = true);

// Email: sent through SMTP when configured, otherwise written to the log.
builder.Services.AddTransient<IEmailSender>(sp =>
    sp.GetRequiredService<IOptions<SmtpOptions>>().Value.IsConfigured
        ? ActivatorUtilities.CreateInstance<SmtpEmailSender>(sp)
        : ActivatorUtilities.CreateInstance<LoggingEmailSender>(sp));
builder.Services.AddTransient<IContactMessageSender, ContactMessageSender>();

// Database (PostgreSQL) and bookings.
builder.Services.AddDbContext<AppDbContext>(o =>
    o.UseNpgsql(builder.Configuration.GetConnectionString("Default")
        ?? throw new InvalidOperationException("Connection string 'Default' is missing.")));
builder.Services.Configure<BookingOptions>(builder.Configuration.GetSection(BookingOptions.Section));
builder.Services.AddSingleton<HotelClock>();
builder.Services.AddScoped<AvailabilityService>();
builder.Services.AddScoped<BookingService>();
builder.Services.AddScoped<BookingNotifier>();
builder.Services.AddScoped<BookingText>();
builder.Services.AddHostedService<BookingMaintenanceService>();

// Staff accounts for /admin.
builder.Services
    .AddIdentity<IdentityUser, IdentityRole>(o =>
    {
        o.Password.RequiredLength = 10;
        o.Password.RequireNonAlphanumeric = false;
        o.Lockout.MaxFailedAccessAttempts = 5;
        o.Lockout.DefaultLockoutTimeSpan = TimeSpan.FromMinutes(15);
        o.User.RequireUniqueEmail = true;
    })
    .AddEntityFrameworkStores<AppDbContext>()
    .AddDefaultTokenProviders();
builder.Services.ConfigureApplicationCookie(o =>
{
    o.LoginPath = "/admin/login";
    o.AccessDeniedPath = "/admin/login";
    o.Cookie.Name = "rt.staff";
    o.ExpireTimeSpan = TimeSpan.FromHours(12);
    o.SlidingExpiration = true;
});

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
    // The staff admin (/admin) has no language in its URLs: it remembers the choice in a cookie.
    o.RequestCultureProviders.Add(new CookieRequestCultureProvider());
});

builder.Services
    .AddRazorPages(o =>
    {
        o.Conventions.Add(new LocalizedPageRouteConvention());
        o.Conventions.AuthorizeFolder("/Admin");
        o.Conventions.AllowAnonymousToPage("/Admin/Login");
    })
    .AddViewLocalization()
    .AddDataAnnotationsLocalization(o =>
        o.DataAnnotationLocalizerProvider = (_, factory) => factory.Create(typeof(RachelTower.Web.SharedResource)));

var app = builder.Build();

// Bring the database schema up to date and create the starting data (rooms, rates, first staff account).
if (app.Configuration.GetValue("Database:MigrateOnStartup", true))
{
    await using var scope = app.Services.CreateAsyncScope();
    await scope.ServiceProvider.GetRequiredService<AppDbContext>().Database.MigrateAsync();
    await DbSeeder.SeedAsync(scope.ServiceProvider);
}

if (!app.Environment.IsDevelopment())
{
    app.UseExceptionHandler("/Error");
    app.UseHsts();
}

app.UseStatusCodePagesWithReExecute("/Error/{0}");
app.UseRouting();
app.UseRequestLocalization();
app.UseAuthentication();
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

// Staff admin language switch: remembered in a cookie for a year.
app.MapGet("/admin/language", (HttpContext ctx, string c, string? returnUrl) =>
{
    if (SiteCultures.IsSupported(c))
    {
        ctx.Response.Cookies.Append(CookieRequestCultureProvider.DefaultCookieName,
            CookieRequestCultureProvider.MakeCookieValue(new RequestCulture(c)),
            new CookieOptions { Expires = DateTimeOffset.UtcNow.AddYears(1), IsEssential = true, HttpOnly = true, SameSite = SameSiteMode.Lax });
    }
    return Results.LocalRedirect(returnUrl is not null && returnUrl.StartsWith('/') && !returnUrl.StartsWith("//") ? returnUrl : "/admin");
});

app.MapGet("/robots.txt", (HttpContext ctx) =>
    Results.Text($"User-agent: *\nAllow: /\nDisallow: /admin\nSitemap: {ctx.Request.Scheme}://{ctx.Request.Host}/sitemap.xml\n", "text/plain"));

app.MapGet("/sitemap.xml", (HttpContext ctx) =>
{
    var root = $"{ctx.Request.Scheme}://{ctx.Request.Host}";
    var xml = new StringBuilder("""<?xml version="1.0" encoding="UTF-8"?><urlset xmlns="http://www.sitemaps.org/schemas/sitemap/0.9" xmlns:xhtml="http://www.w3.org/1999/xhtml">""");
    foreach (var (page, slugs) in SiteRoutes.Pages)
    {
        if (SiteRoutes.NotIndexed.Contains(page)) continue;
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

/// <summary>Exposed so integration tests can start the app.</summary>
public partial class Program;
