# The Rachel Tower: hotel website

Bilingual (English / French) website and booking engine for The Rachel Tower, Kolwezi, DR Congo.
ASP.NET Core 10 Razor Pages, PostgreSQL with EF Core, Tailwind CSS v4, no JavaScript framework.

## Run it

Requirements: [.NET 10 SDK](https://dotnet.microsoft.com/download/dotnet/10.0) and PostgreSQL 14 or later.
The easiest way to get PostgreSQL is Docker:

```bash
docker compose up -d          
cd src/RachelTower.Web
dotnet run
```

Open http://localhost:5080. `/` redirects to `/en` or `/fr` based on the browser language.
On startup the app creates the database tables and the starting data (5 VIP rooms at 150 USD,
3 Standard rooms at 130 USD) if they don't exist yet.

**Staff admin:** http://localhost:5080/admin. 

### First-time setup

1. **Copy the photos off Wix** (once). Until you do, photos load straight from Wix's CDN.
   ```powershell
   pwsh scripts/download-images.ps1     # Windows / PowerShell
   ```
   ```bash
   bash scripts/download-images.sh      # macOS / Linux
   ```
2. **Email.** Fill in the `Smtp` section of `appsettings.json` (keep the password in user secrets:
   `dotnet user-secrets set "Smtp:Password" "..."`). Without it, contact messages and booking
   confirmations are written to the log instead of emailed.

### Production settings

Set these as environment variables or app settings on the server, never in the repo:

| Setting | Example |
|---|---|
| `ConnectionStrings__Default` | `Host=...;Database=racheltower;Username=...;Password=...;SSL Mode=Require` |
| `Admin__Email`, `Admin__Password` | First staff account, created only when there are no accounts yet |
| `Smtp__Host`, `Smtp__User`, `Smtp__Password`, `Smtp__From` | Outgoing email |

## Tests

```bash
dotnet test
```

The booking tests run against a real PostgreSQL database (`racheltower_test`, recreated on each run;
set `TEST_DATABASE` to use another), because the double-booking guarantee is a PostgreSQL constraint.
They cover pricing, availability, booking rules, 12 guests racing for the last 3 rooms, payment holds,
automatic release of unpaid no-shows, the cancellation deadline and front desk status changes.
GitHub Actions runs them on every push and pull request.

## Bookings

**Guest flow:** `/en/book` (dates → available rooms and prices) → `/en/book/details` (name, phone,
email, pay at the desk) → `/en/booking/{token}`, the guest's private page to view or cancel.
Guests get a confirmation email if they give an address; the hotel inbox gets a notice in French.

**Staff admin (`/admin`):** today's arrivals, departures and in-house guests; a two-week room
calendar; booking search; check in, check out, mark paid, no-show, cancel; bookings taken by phone,
WhatsApp, walk-in or copied from Booking.com / Airbnb; room names, rates, capacity and closing a room.
English or French, remembered per browser.

**Rules** (`appsettings.json` → `Booking`):

| Setting | Default | Meaning |
|---|---|---|
| `ReleaseUnpaidAtHour` | `18` | Unpaid pay-at-desk bookings become no-shows at 18:00 on the arrival day if the guest hasn't checked in (`null` = never) |
| `FreeCancellationHours` | `24` | Guests can cancel online until 24 h before check-in time |
| `CheckInHour` | `14` | Check-in time, used for the cancellation deadline |
| `OnlinePaymentHoldMinutes` | `20` | How long a room is held during an online payment (mobile money, step 3) |
| `MaxNights` / `MaxDaysAhead` | `30` / `365` | Limits for online bookings |
| `TimeZone` | `Africa/Lubumbashi` | Kolwezi time (UTC+2) |

**How double bookings are prevented:** rooms are only offered when free, and a PostgreSQL exclusion
constraint (`ex_bookings_no_overlap`) rejects any two active bookings of the same room whose dates
overlap, even if two guests click "Book" at the same instant. The app then tries the next free room.
A guest can leave the morning another arrives. Rates are copied onto each booking, so changing a
rate never changes existing bookings. Staff edits are protected by a row version, so two people
can't overwrite each other.

**Background job:** `BookingMaintenanceService` runs every minute to expire unpaid online-payment
holds and release unpaid no-shows.

## Editing the site

| What | Where |
|---|---|
| All text, English | `Resources/SharedResource.resx` |
| All text, French | `Resources/SharedResource.fr.resx` |
| Phones, email, address, WhatsApp, Airbnb link | `appsettings.json` → `Hotel` |
| Room names, descriptions, rates | Staff admin → Rooms & rates |
| Photos used on each page | `Infrastructure/SiteImages.cs` |
| Page URLs per language (`/en/amenities` ↔ `/fr/services`) | `Localization/SiteRoutes.cs` |
| Colours, fonts, buttons | `Styles/app.css` |

Every text key must exist in both `.resx` files. Visual Studio and Rider have a side-by-side resource editor.

### Adding a page

1. Create `Pages/MyPage.cshtml` with `@page` at the top.
2. Add `["/MyPage"] = new("my-page", "ma-page")` to `SiteRoutes.Pages`.
3. Add its text to both `.resx` files and a nav entry in `Pages/Shared/_Layout.cshtml`.
4. Link to it with `Url.PageIn("/MyPage")` so the current language is kept.

### Database changes

```bash
dotnet tool restore
dotnet ef migrations add <Name> --project src/RachelTower.Web -o Data/Migrations
```

Migrations are applied automatically on startup (`Database:MigrateOnStartup`).

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
  `LocalizedPageRouteConvention` gives each page one route per language. Public pages have
  `canonical` and `hreflang` tags, and `/sitemap.xml` lists both versions of every public page.
  Booking steps and private booking pages are `noindex`.
- **Contact form.** Server-side validation in both languages, Post/Redirect/Get, and a hidden honeypot field against spam bots.
- **SEO.** Per-page titles and descriptions, Open Graph tags, and schema.org `Hotel` data on the home page.

## Roadmap

1. ~~Bilingual marketing pages~~
2. ~~Booking engine: rooms, rates, availability, pay at the desk, staff admin~~ (this version)
3. Mobile money via CinetPay (Orange Money, M-Pesa, Airtel Money; USD and CDF)
4. Booking.com and Airbnb sync via the Channex API
5. Deploy: Azure App Service (South Africa North) via GitHub Actions
