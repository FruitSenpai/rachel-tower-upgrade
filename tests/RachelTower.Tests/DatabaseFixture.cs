using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using RachelTower.Web.Data;
using RachelTower.Web.Reservations;

namespace RachelTower.Tests;

/// <summary>
/// Runs the tests against a real PostgreSQL database, because the double-booking guarantee lives
/// in a PostgreSQL constraint. Set TEST_DATABASE to point elsewhere; the database is recreated on each run.
/// </summary>
public sealed class DatabaseFixture : IAsyncLifetime
{
    public const string DefaultConnection =
        "Host=localhost;Port=5432;Database=racheltower_test;Username=rachel;Password=rachel";

    /// <summary>"Now" for every test: 10 November 2026, 10:00 in Kolwezi (08:00 UTC).</summary>
    public static readonly DateTimeOffset Start = new(2026, 11, 10, 8, 0, 0, TimeSpan.Zero);

    public TestClock Time { get; } = new(Start);
    public ServiceProvider Services { get; private set; } = null!;

    public async ValueTask InitializeAsync()
    {
        var connection = Environment.GetEnvironmentVariable("TEST_DATABASE") ?? DefaultConnection;
        var services = new ServiceCollection();
        services.AddLogging(b => b.SetMinimumLevel(LogLevel.Warning));
        services.AddSingleton<IConfiguration>(new ConfigurationBuilder().Build());
        services.AddDbContext<AppDbContext>(o => o.UseNpgsql(connection));
        services.AddIdentityCore<IdentityUser>().AddEntityFrameworkStores<AppDbContext>();
        services.AddSingleton<TimeProvider>(Time);
        services.AddSingleton(Options.Create(new BookingOptions()));
        services.AddSingleton<HotelClock>();
        services.AddScoped<AvailabilityService>();
        services.AddScoped<BookingService>();
        Services = services.BuildServiceProvider();

        await using var scope = Services.CreateAsyncScope();
        var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
        await db.Database.EnsureDeletedAsync();
        await db.Database.MigrateAsync();
        await DbSeeder.SeedAsync(scope.ServiceProvider);
    }

    /// <summary>Removes all bookings and resets the clock, so each test starts from an empty hotel.</summary>
    public async Task ResetAsync()
    {
        Time.SetUtcNow(Start);
        await using var scope = Services.CreateAsyncScope();
        var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
        await db.Database.ExecuteSqlRawAsync("TRUNCATE \"Bookings\" RESTART IDENTITY");
        await db.Rooms.ExecuteUpdateAsync(s => s.SetProperty(r => r.IsActive, true));
    }

    public async ValueTask DisposeAsync() => await Services.DisposeAsync();
}

[CollectionDefinition(nameof(DatabaseCollection))]
public sealed class DatabaseCollection : ICollectionFixture<DatabaseFixture>;
