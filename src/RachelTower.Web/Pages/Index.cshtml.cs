using System.Text.Json;
using Microsoft.AspNetCore.Mvc.RazorPages;
using Microsoft.Extensions.Options;
using RachelTower.Web.Infrastructure;

namespace RachelTower.Web.Pages;

public class IndexModel(IOptions<HotelOptions> hotel) : PageModel
{
    /// <summary>schema.org Hotel markup so search engines can show address, phone and map details.</summary>
    public string HotelJsonLd(string imageUrl, string pageUrl)
    {
        var h = hotel.Value;
        var data = new Dictionary<string, object?>
        {
            ["@context"] = "https://schema.org",
            ["@type"] = "Hotel",
            ["name"] = h.Name,
            ["url"] = pageUrl,
            ["image"] = imageUrl,
            ["email"] = h.Email,
            ["telephone"] = h.Phones.FirstOrDefault(),
            ["address"] = new Dictionary<string, string>
            {
                ["@type"] = "PostalAddress",
                ["streetAddress"] = h.StreetAddress,
                ["addressLocality"] = h.Locality,
                ["addressRegion"] = h.Region,
                ["addressCountry"] = h.Country,
            },
            ["amenityFeature"] = new[] { "Restaurant", "Fitness center", "Spa", "24-hour front desk" }
                .Select(a => new Dictionary<string, object> { ["@type"] = "LocationFeatureSpecification", ["name"] = a, ["value"] = true }),
            ["sameAs"] = new[] { h.AirbnbUrl },
        };
        return JsonSerializer.Serialize(data);
    }
}
