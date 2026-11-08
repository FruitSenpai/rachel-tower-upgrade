using RachelTower.Web.Localization;

namespace RachelTower.Web.Data;

/// <summary>A category of room with one price, e.g. "VIP" or "Standard".</summary>
public class RoomType
{
    public int Id { get; set; }

    /// <summary>Stable identifier used in URLs, e.g. "vip".</summary>
    public required string Code { get; set; }

    public required string NameEn { get; set; }
    public required string NameFr { get; set; }
    public string DescriptionEn { get; set; } = "";
    public string DescriptionFr { get; set; } = "";
    public decimal NightlyRateUsd { get; set; }
    public int MaxGuests { get; set; } = 2;
    public int SortOrder { get; set; }

    /// <summary>Key of the photo in SiteImages, e.g. "bedroom".</summary>
    public string ImageKey { get; set; } = "bedroom";

    public List<Room> Rooms { get; set; } = [];

    public string Name(string culture) => culture == SiteCultures.French ? NameFr : NameEn;
    public string Description(string culture) => culture == SiteCultures.French ? DescriptionFr : DescriptionEn;
}

/// <summary>A physical room. Bookings are always assigned to one room.</summary>
public class Room
{
    public int Id { get; set; }
    public required string Number { get; set; }
    public int RoomTypeId { get; set; }
    public RoomType RoomType { get; set; } = null!;

    /// <summary>Inactive rooms (e.g. under repair) are never offered.</summary>
    public bool IsActive { get; set; } = true;
}

public enum BookingStatus
{
    /// <summary>Waiting for an online payment; the room is held until HoldExpiresAt (step 3, mobile money).</summary>
    PendingPayment = 0,
    Confirmed = 1,
    CheckedIn = 2,
    CheckedOut = 3,
    Cancelled = 4,
    NoShow = 5,
    /// <summary>An online payment hold ran out before the guest paid.</summary>
    Expired = 6,
}

public enum PaymentMethod
{
    PayAtDesk = 0,
    MobileMoney = 1,
}

public enum PaymentStatus
{
    Unpaid = 0,
    Paid = 1,
    Refunded = 2,
}

public enum BookingSource
{
    Website = 0,
    Staff = 1,
    BookingCom = 2,
    Airbnb = 3,
}

public class Booking
{
    public int Id { get; set; }

    /// <summary>Short code given to the guest, e.g. "RT-7K3M9Q".</summary>
    public required string Reference { get; set; }

    /// <summary>Secret token in the guest's "manage my booking" link.</summary>
    public required string ManageToken { get; set; }

    public int RoomId { get; set; }
    public Room Room { get; set; } = null!;
    public int RoomTypeId { get; set; }
    public RoomType RoomType { get; set; } = null!;

    public DateOnly CheckIn { get; set; }
    public DateOnly CheckOut { get; set; }
    public int Guests { get; set; }

    public required string GuestName { get; set; }
    public required string GuestPhone { get; set; }
    public string? GuestEmail { get; set; }
    public string? Notes { get; set; }
    public string Culture { get; set; } = SiteCultures.Default;

    public BookingStatus Status { get; set; }
    public PaymentMethod PaymentMethod { get; set; }
    public PaymentStatus PaymentStatus { get; set; }
    public BookingSource Source { get; set; }

    /// <summary>Rate at the time of booking, so later price changes don't affect it.</summary>
    public decimal NightlyRateUsd { get; set; }
    public decimal TotalUsd { get; set; }

    /// <summary>
    /// PendingPayment: when the online payment hold ends.
    /// Confirmed + pay at desk: when an unpaid booking is released if the guest hasn't arrived.
    /// </summary>
    public DateTimeOffset? HoldExpiresAt { get; set; }

    public DateTimeOffset CreatedAt { get; set; }
    public DateTimeOffset UpdatedAt { get; set; }
    public DateTimeOffset? CancelledAt { get; set; }

    /// <summary>PostgreSQL row version, so two staff members can't overwrite each other's changes.</summary>
    public uint Version { get; set; }

    public int Nights => CheckOut.DayNumber - CheckIn.DayNumber;

    /// <summary>Statuses that occupy the room. Must match the exclusion constraint in the migration.</summary>
    public static readonly BookingStatus[] OccupyingStatuses =
        [BookingStatus.PendingPayment, BookingStatus.Confirmed, BookingStatus.CheckedIn];

    public bool IsActive => OccupyingStatuses.Contains(Status);
}
