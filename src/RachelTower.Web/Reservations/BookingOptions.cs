namespace RachelTower.Web.Reservations;

/// <summary>Booking rules, bound from the "Booking" section of appsettings.json.</summary>
public sealed class BookingOptions
{
    public const string Section = "Booking";

    /// <summary>IANA time zone of the hotel. Kolwezi is UTC+2 all year.</summary>
    public string TimeZone { get; set; } = "Africa/Lubumbashi";

    /// <summary>Local hour guests can check in from. Used for the cancellation cut-off.</summary>
    public int CheckInHour { get; set; } = 14;

    /// <summary>
    /// Unpaid pay-at-desk bookings are released (marked no-show) at this local hour on the arrival day
    /// if the guest hasn't checked in. Set to null to never release automatically.
    /// </summary>
    public int? ReleaseUnpaidAtHour { get; set; } = 18;

    /// <summary>Guests can cancel online until this many hours before the check-in time.</summary>
    public int FreeCancellationHours { get; set; } = 24;

    /// <summary>How long a room is held while a guest pays online (step 3, mobile money).</summary>
    public int OnlinePaymentHoldMinutes { get; set; } = 20;

    public int MaxNights { get; set; } = 30;
    public int MaxDaysAhead { get; set; } = 365;
}
