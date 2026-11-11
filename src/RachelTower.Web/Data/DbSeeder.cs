using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;

namespace RachelTower.Web.Data;

/// <summary>
/// Creates the starting data on an empty database: the hotel's rooms and rates, and the first
/// staff account (from the "Admin" settings). Never overwrites anything already there, so rates
/// and rooms edited in the admin are kept.
/// </summary>
public static class DbSeeder
{
    public static async Task SeedAsync(IServiceProvider services, CancellationToken ct = default)
    {
        var db = services.GetRequiredService<AppDbContext>();
        var config = services.GetRequiredService<IConfiguration>();
        var logger = services.GetRequiredService<ILoggerFactory>().CreateLogger(typeof(DbSeeder));

        if (!await db.RoomTypes.AnyAsync(ct))
        {
            var vip = new RoomType
            {
                Code = "vip",
                NameEn = "VIP Room",
                NameFr = "Chambre VIP",
                DescriptionEn = "Our premium rooms, with a comfortable double bed and quality linen, a work desk, an in-room safe, a tea and coffee tray, a television and an en-suite bathroom with walk-in shower.",
                DescriptionFr = "Nos chambres haut de gamme : lit double confortable et linge de qualité, bureau, coffre-fort, plateau de thé et café, télévision et salle de bain privative avec douche à l'italienne.",
                NightlyRateUsd = 150m,
                MaxGuests = 2,
                SortOrder = 1,
                ImageKey = "bedroom",
            };
            var standard = new RoomType
            {
                Code = "standard",
                NameEn = "Standard Room",
                NameFr = "Chambre Standard",
                DescriptionEn = "Comfortable, well-kept rooms with a double bed, a work desk, a television and an en-suite bathroom.",
                DescriptionFr = "Des chambres confortables et soignées, avec lit double, bureau, télévision et salle de bain privative.",
                NightlyRateUsd = 130m,
                MaxGuests = 2,
                SortOrder = 2,
                ImageKey = "room-desk",
            };
            for (var i = 1; i <= 5; i++) vip.Rooms.Add(new Room { Number = $"VIP {i}" });
            for (var i = 1; i <= 3; i++) standard.Rooms.Add(new Room { Number = $"STD {i}" });

            db.RoomTypes.AddRange(vip, standard);
            await db.SaveChangesAsync(ct);
            logger.LogInformation("Seeded room types: 5 VIP rooms at 150 USD and 3 Standard rooms at 130 USD");
        }

        var users = services.GetRequiredService<UserManager<IdentityUser>>();
        if (!await users.Users.AnyAsync(ct))
        {
            var email = config["Admin:Email"];
            var password = config["Admin:Password"];
            if (string.IsNullOrWhiteSpace(email) || string.IsNullOrWhiteSpace(password))
            {
                logger.LogWarning("No staff account exists. Set Admin:Email and Admin:Password to create the first one.");
                return;
            }

            var user = new IdentityUser { UserName = email, Email = email, EmailConfirmed = true };
            var result = await users.CreateAsync(user, password);
            if (result.Succeeded)
            {
                logger.LogInformation("Created staff account {Email}", email);
            }
            else
            {
                logger.LogError("Could not create staff account {Email}: {Errors}", email, string.Join("; ", result.Errors.Select(e => e.Description)));
            }
        }
    }
}
