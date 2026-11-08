namespace RachelTower.Web.Reservations;

/// <summary>
/// Runs every minute: frees rooms whose online payment hold ran out, and releases unpaid
/// pay-at-desk bookings whose guest hasn't arrived by the release time.
/// </summary>
public sealed class BookingMaintenanceService(IServiceScopeFactory scopes, ILogger<BookingMaintenanceService> logger) : BackgroundService
{
    private static readonly TimeSpan Interval = TimeSpan.FromMinutes(1);

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        using var timer = new PeriodicTimer(Interval);
        do
        {
            try
            {
                await using var scope = scopes.CreateAsyncScope();
                var bookings = scope.ServiceProvider.GetRequiredService<BookingService>();
                var expired = await bookings.ExpireOnlinePaymentHoldsAsync(stoppingToken);
                var released = await bookings.ReleaseUnpaidNoShowsAsync(stoppingToken);
                if (expired + released > 0)
                {
                    logger.LogInformation("Booking maintenance: {Expired} payment holds expired, {Released} unpaid no-shows released", expired, released);
                }
            }
            catch (Exception ex) when (ex is not OperationCanceledException)
            {
                logger.LogError(ex, "Booking maintenance failed; will retry in {Interval}", Interval);
            }
        }
        while (await timer.WaitForNextTickAsync(stoppingToken));
    }
}
