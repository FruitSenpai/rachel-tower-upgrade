using System.Globalization;

namespace RachelTower.Web.Localization;

/// <summary>The languages the site is published in.</summary>
public static class SiteCultures
{
    public const string English = "en";
    public const string French = "fr";
    public const string Default = English;

    public static readonly string[] All = [English, French];

    public static bool IsSupported(string? culture) =>
        culture is not null && All.Contains(culture, StringComparer.OrdinalIgnoreCase);

    public static string Current => CultureInfo.CurrentUICulture.TwoLetterISOLanguageName switch
    {
        French => French,
        _ => English,
    };

    public static string Other(string culture) => culture == French ? English : French;
}
