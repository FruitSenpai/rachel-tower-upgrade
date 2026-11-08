using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Identity.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore;

namespace RachelTower.Web.Data;

public class AppDbContext(DbContextOptions<AppDbContext> options) : IdentityDbContext<IdentityUser>(options)
{
    /// <summary>Name of the constraint that makes double bookings impossible at the database level.</summary>
    public const string NoOverlapConstraint = "ex_bookings_no_overlap";

    public DbSet<RoomType> RoomTypes => Set<RoomType>();
    public DbSet<Room> Rooms => Set<Room>();
    public DbSet<Booking> Bookings => Set<Booking>();

    protected override void OnModelCreating(ModelBuilder b)
    {
        base.OnModelCreating(b);

        // Needed for the no-overlap exclusion constraint (see the InitialBooking migration).
        b.HasPostgresExtension("btree_gist");

        b.Entity<RoomType>(e =>
        {
            e.HasIndex(x => x.Code).IsUnique();
            e.Property(x => x.Code).HasMaxLength(40);
            e.Property(x => x.NameEn).HasMaxLength(80);
            e.Property(x => x.NameFr).HasMaxLength(80);
            e.Property(x => x.DescriptionEn).HasMaxLength(1000);
            e.Property(x => x.DescriptionFr).HasMaxLength(1000);
            e.Property(x => x.ImageKey).HasMaxLength(60);
            e.Property(x => x.NightlyRateUsd).HasPrecision(10, 2);
            e.ToTable(t => t.HasCheckConstraint("ck_room_types_rate", "\"NightlyRateUsd\" >= 0"));
        });

        b.Entity<Room>(e =>
        {
            e.HasIndex(x => x.Number).IsUnique();
            e.Property(x => x.Number).HasMaxLength(20);
            e.HasOne(x => x.RoomType).WithMany(t => t.Rooms).OnDelete(DeleteBehavior.Restrict);
        });

        b.Entity<Booking>(e =>
        {
            e.HasIndex(x => x.Reference).IsUnique();
            e.HasIndex(x => x.ManageToken).IsUnique();
            e.HasIndex(x => new { x.RoomId, x.CheckIn, x.CheckOut });
            e.HasIndex(x => new { x.Status, x.HoldExpiresAt });
            e.Property(x => x.Reference).HasMaxLength(12);
            e.Property(x => x.ManageToken).HasMaxLength(64);
            e.Property(x => x.GuestName).HasMaxLength(100);
            e.Property(x => x.GuestPhone).HasMaxLength(30);
            e.Property(x => x.GuestEmail).HasMaxLength(200);
            e.Property(x => x.Notes).HasMaxLength(2000);
            e.Property(x => x.Culture).HasMaxLength(5);
            e.Property(x => x.NightlyRateUsd).HasPrecision(10, 2);
            e.Property(x => x.TotalUsd).HasPrecision(10, 2);
            e.Property(x => x.Version).IsRowVersion(); // maps to PostgreSQL xmin
            e.HasOne(x => x.Room).WithMany().OnDelete(DeleteBehavior.Restrict);
            e.HasOne(x => x.RoomType).WithMany().OnDelete(DeleteBehavior.Restrict);
            e.ToTable(t =>
            {
                t.HasCheckConstraint("ck_bookings_dates", "\"CheckOut\" > \"CheckIn\"");
                t.HasCheckConstraint("ck_bookings_guests", "\"Guests\" > 0");
            });
        });
    }
}
