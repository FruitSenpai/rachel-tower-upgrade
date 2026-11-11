using Microsoft.Extensions.Options;

namespace RachelTower.Web.Reservations;

/// <summary>Current date and time at the hotel, in Kolwezi time.</summary>
public sealed class HotelClock(TimeProvider time, IOptions<BookingOptions> options)
{
    private readonly TimeZoneInfo _zone = TimeZoneInfo.FindSystemTimeZoneById(options.Value.TimeZone);

    public DateTimeOffset UtcNow => time.GetUtcNow();

    public DateTimeOffset LocalNow => TimeZoneInfo.ConvertTime(UtcNow, _zone);

    public DateOnly Today => DateOnly.FromDateTime(LocalNow.DateTime);

    /// <summary>The instant a given local date and hour happens at the hotel.</summary>
    public DateTimeOffset At(DateOnly date, int hour)
    {
        var local = date.ToDateTime(new TimeOnly(hour, 0));
        return new DateTimeOffset(local, _zone.GetUtcOffset(local)).ToUniversalTime();
    }

    public DateTimeOffset ToLocal(DateTimeOffset instant) => TimeZoneInfo.ConvertTime(instant, _zone);
}
