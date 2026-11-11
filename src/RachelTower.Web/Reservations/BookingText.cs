using Microsoft.Extensions.Localization;
using Microsoft.Extensions.Options;

namespace RachelTower.Web.Reservations;

/// <summary>Turns booking errors into messages for guests and staff, in the current language.</summary>
public sealed class BookingText(IStringLocalizer<SharedResource> L, IOptions<BookingOptions> rules)
{
    public string Error(BookingError error) => error switch
    {
        BookingError.TooManyNights => string.Format(L["Err.TooManyNights"], rules.Value.MaxNights),
        BookingError.TooFarAhead => string.Format(L["Err.TooFarAhead"], rules.Value.MaxDaysAhead),
        _ => L[$"Err.{error}"],
    };
}
