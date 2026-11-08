namespace RachelTower.Web.Infrastructure;

/// <summary>Hotel contact details, bound from the "Hotel" section of appsettings.json.</summary>
public sealed class HotelOptions
{
    public const string Section = "Hotel";

    public string Name { get; set; } = "The Rachel Tower";
    public string Email { get; set; } = "";
    public List<string> Phones { get; set; } = [];

    /// <summary>WhatsApp number in international format, digits only (e.g. 243815363323).</summary>
    public string WhatsApp { get; set; } = "";

    public string StreetAddress { get; set; } = "";
    public string Locality { get; set; } = "";
    public string Region { get; set; } = "";
    public string Country { get; set; } = "";
    public string AirbnbUrl { get; set; } = "";
    public string MapQuery { get; set; } = "";
    public string OpeningDate { get; set; } = "";

    public string WhatsAppUrl(string? message = null) =>
        $"https://wa.me/{WhatsApp}" + (string.IsNullOrWhiteSpace(message) ? "" : $"?text={Uri.EscapeDataString(message)}");

    public static string TelUrl(string phone) => "tel:" + new string(phone.Where(c => char.IsDigit(c) || c == '+').ToArray());
}
