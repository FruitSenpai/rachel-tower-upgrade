# The Rachel Tower: hotel website

Bilingual (English / French) website for The Rachel Tower, Kolwezi, DR Congo.
ASP.NET Core 10 Razor Pages, Tailwind CSS v4, no JavaScript framework.

## Run it

Requirements: [.NET 10 SDK](https://dotnet.microsoft.com/download/dotnet/10.0).

```bash
cd src/RachelTower.Web
dotnet run
```

Open http://localhost:5080 (or the port shown). `/` redirects to `/en` or `/fr` based on the browser language.

### First-time setup

1. **Copy the photos off Wix** (once). Until you do, photos load straight from Wix's CDN.
   ```powershell
   pwsh scripts/download-images.ps1     # Windows / PowerShell
   ```
   ```bash
   bash scripts/download-images.sh      # macOS / Linux
   ```
2. **Email for the contact form.** Fill in the `Smtp` section of `appsettings.json`
   (keep the password in user secrets locally: `dotnet user-secrets set "Smtp:Password" "..."`).
   Without it, messages are written to the log instead of emailed.

## Editing the site

| What | Where |
|---|---|
| All text, English | `Resources/SharedResource.resx` |
| All text, French | `Resources/SharedResource.fr.resx` |
| Phones, email, address, WhatsApp, Airbnb link | `appsettings.json` → `Hotel` |
| Photos used on each page | `Infrastructure/SiteImages.cs` |
| Page URLs per language (`/en/amenities` ↔ `/fr/services`) | `Localization/SiteRoutes.cs` |
| Colours, fonts, buttons | `Styles/app.css` |

Every text key must exist in both `.resx` files. Visual Studio and Rider have a side-by-side resource editor.

### Adding a page

1. Create `Pages/MyPage.cshtml` with `@page` at the top.
2. Add `["/MyPage"] = new("my-page", "ma-page")` to `SiteRoutes.Pages`.
3. Add its text to both `.resx` files and a nav entry in `Pages/Shared/_Layout.cshtml`.
4. Link to it with `Url.PageIn("/MyPage")` so the current language is kept.

### Styles (Tailwind)

The compiled `wwwroot/css/site.css` is committed, so you only need Tailwind when changing styles.
Download the standalone CLI (single file, no Node needed) from
https://github.com/tailwindlabs/tailwindcss/releases (v4.x, e.g. `tailwindcss-windows-x64.exe`), then:

```bash
tailwindcss -i Styles/app.css -o wwwroot/css/site.css --watch    # while editing
tailwindcss -i Styles/app.css -o wwwroot/css/site.css --minify   # before committing
```

## How it's built

- **Languages.** The first URL segment sets the culture (`RouteDataRequestCultureProvider`).
  `LocalizedPageRouteConvention` gives each page one route per language. Each page has
  `canonical` and `hreflang` tags, and `/sitemap.xml` lists both versions of every page.
- **Booking (step 1).** `/en/book` collects dates and opens WhatsApp with a pre-filled request
  to the front desk. Step 2 replaces this with the live booking engine.
- **Contact form.** Server-side validation in both languages, Post/Redirect/Get, and a hidden honeypot field against spam bots.
- **SEO.** Per-page titles and descriptions, Open Graph tags, and schema.org `Hotel` data on the home page.

## Roadmap

1. ~~Bilingual marketing pages~~ (this version)
2. Booking engine: PostgreSQL + EF Core, rooms, rates, availability holds, pay at the desk, staff admin
3. Mobile money via CinetPay (Orange Money, M-Pesa, Airtel Money; USD and CDF)
4. Booking.com and Airbnb sync via the Channex API
5. Deploy: Azure App Service (South Africa North) via GitHub Actions
