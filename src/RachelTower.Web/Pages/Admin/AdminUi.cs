using System.Globalization;
using RachelTower.Web.Data;

namespace RachelTower.Web.Pages.Admin;

/// <summary>Small display helpers shared by the admin pages.</summary>
public static class AdminUi
{
    public static string StatusClass(BookingStatus status) => status switch
    {
        BookingStatus.Confirmed => "bg-sky-50 text-sky-800 ring-sky-200",
        BookingStatus.CheckedIn => "bg-emerald-50 text-emerald-800 ring-emerald-200",
        BookingStatus.CheckedOut => "bg-stone-100 text-stone-700 ring-stone-200",
        BookingStatus.PendingPayment => "bg-amber-50 text-amber-800 ring-amber-200",
        _ => "bg-red-50 text-red-800 ring-red-200",
    };

    /// <summary>Colour of a booking bar in the calendar.</summary>
    public static string BarClass(BookingStatus status) => status switch
    {
        BookingStatus.Confirmed => "bg-sky-600 text-white",
        BookingStatus.CheckedIn => "bg-emerald-600 text-white",
        BookingStatus.CheckedOut => "bg-stone-300 text-stone-800",
        BookingStatus.PendingPayment => "bg-amber-400 text-ink",
        _ => "bg-red-200 text-red-900",
    };

    public static string Dates(Booking b, CultureInfo c) =>
        $"{b.CheckIn.ToString("d MMM", c)} → {b.CheckOut.ToString("d MMM yyyy", c)}";

    /// <summary>A return URL is only followed if it stays inside the admin.</summary>
    public static string SafeReturn(string? url, string fallback) =>
        url is not null && url.StartsWith("/admin", StringComparison.OrdinalIgnoreCase) && !url.StartsWith("//") ? url : fallback;
}
