namespace RachelTower.Web.Infrastructure;

/// <summary>A photo used on the site. Key is the local file name in wwwroot/images.</summary>
public sealed record SiteImage(string Key, string WixId, string Ext = "jpg", string AltKey = "")
{
    public string FileName => $"{Key}.{Ext}";
    public string WixUrl => $"https://static.wixstatic.com/media/f2e2c0_{WixId}~mv2.{Ext}";
}

/// <summary>
/// Every photo carried over from the Wix site.
/// Run scripts/download-images.(ps1|sh) once to copy them into wwwroot/images;
/// until then they are served straight from Wix's CDN.
/// </summary>
public static class SiteImages
{
    public static readonly SiteImage Logo = new("logo", "7eda3bdf61d3473ba5c1f110115d81c4", "png", "Img.Logo");
    public static readonly SiteImage Facade = new("facade", "8cc8705978e64ecb92c8510e18049010", AltKey: "Img.Facade");
    public static readonly SiteImage Aerial = new("aerial", "4391b7ecf1e042b2951281add4574699", AltKey: "Img.Aerial");
    public static readonly SiteImage Courtyard = new("courtyard", "047be59571654f87a7c728861ad38662", AltKey: "Img.Courtyard");
    public static readonly SiteImage Reception = new("reception", "e34618e0fc614bafbf2b45e0cacd7fc1", AltKey: "Img.Reception");
    public static readonly SiteImage Lounge = new("lounge", "c1a3b1ca9474485583964b9b436bea6c", AltKey: "Img.Lounge");
    public static readonly SiteImage Bedroom = new("bedroom", "35197812d4444a1aafbd10164da80e7a", AltKey: "Img.Bedroom");
    public static readonly SiteImage RoomDesk = new("room-desk", "6724475591c848aea0a6d9580bb33e23", AltKey: "Img.RoomDesk");
    public static readonly SiteImage RoomEntrance = new("room-entrance", "7cb61c156dbc4a6884c167ee88b9fe06", AltKey: "Img.RoomEntrance");
    public static readonly SiteImage Bathroom = new("bathroom", "e519c093e110487b9e939d6f32d29479", AltKey: "Img.Bathroom");
    public static readonly SiteImage Safe = new("in-room-safe", "5018a3fca9f84a5a869b9a339f3d796e", AltKey: "Img.Safe");
    public static readonly SiteImage Linen = new("linen", "ba241dac21d14499a290622d6984c912", AltKey: "Img.Linen");
    public static readonly SiteImage TeaService = new("tea-service", "802b0c2f73204a64b811e80f61a3d703", AltKey: "Img.TeaService");
    public static readonly SiteImage Restaurant = new("restaurant", "b581ca3dd5d44543a6c8305130ed9893", AltKey: "Img.Restaurant");
    public static readonly SiteImage DiningRoom = new("dining-room", "b6cda2d76ef24d6b801bd1a7325951f8", AltKey: "Img.DiningRoom");
    public static readonly SiteImage EventHall = new("event-hall", "d7f5a4f5814a4f56944dd8c5f885a346", AltKey: "Img.EventHall");
    public static readonly SiteImage Fitness = new("fitness", "a9d9c8555996486ea5c38663e9595d9d", AltKey: "Img.Fitness");
    public static readonly SiteImage Kolwezi = new("kolwezi", "f5bfd679184c4fe68d5410434530ced0", AltKey: "Img.Kolwezi");

    public static readonly SiteImage[] HeroSlides = [Facade, Reception, Bedroom, Restaurant, Aerial];

    public static readonly SiteImage[] Gallery =
        [Facade, Courtyard, Reception, Lounge, Bedroom, RoomDesk, RoomEntrance, Bathroom, Safe, Linen, TeaService, Restaurant, DiningRoom, EventHall, Fitness, Aerial];

    /// <summary>Planning drawings and construction photos, in the order they appeared on the History page.</summary>
    public static readonly SiteImage[] Construction =
    [
        new("history-01", "a3616b498cdb43e7a04d7523675a58c7", "jpeg"),
        new("history-02", "f8ef5b328bc74b3fbd438ce533b546df", "jpeg"),
        new("history-03", "12bf1714639e40c79438f95121c4bb89", "jpeg"),
        new("history-04", "2e6f46ea90a049af96d7e7305bf90ad8", "jpeg"),
        new("history-05", "7101e0e3f4c442c7a48cda6bf4162ae7", "jpeg"),
        new("history-06", "227740146cb840fdb61578632c1fefcb", "jpeg"),
        new("history-07", "a5c74a0456cd4af3af4c8c13732c1afa", "jpeg"),
        new("history-08", "667a7dccb8f84f75a71833ffbe416881"),
        new("history-09", "9842263771af4a5eb4a7e3bdde3ee67a"),
        new("history-10", "0bf4eece46054bf8baf2f2478e49dca3"),
        new("history-11", "ccc7ade0cc68447fae5e973c7049f35d"),
        new("history-12", "2e4cdce5e3724abf8c490fa597e50c79"),
        new("history-13", "d9a694fd257e4b2d8162208da14ee93f"),
        new("history-14", "9ecd9fe4fd164802a2ac76e2bc241774"),
        new("history-15", "13e5489f293a47b1803653fcadbfda96"),
        new("history-16", "50769436da5e4d68a5558abd5fadbb22"),
        new("history-17", "f658ff418ff7449cb2b726765fd39a2e"),
    ];

    /// <summary>Photo by key (e.g. a room type's ImageKey); the bedroom photo if the key is unknown.</summary>
    public static SiteImage ByKey(string? key) => All.FirstOrDefault(i => i.Key == key) ?? Bedroom;

    public static IEnumerable<SiteImage> All =>
        new[] { Logo, Kolwezi }.Concat(Gallery).Concat(Construction).DistinctBy(i => i.Key);
}
