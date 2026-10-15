using Library.Core.Configuration;
using Library.Core.Contracts;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace Library.Core.Services;

/// <summary>Runs the daily notification checks. The same code runs from the "Run checks now" button. It never crashes the app.</summary>
public sealed class NotificationScheduler(
    IServiceProvider services,
    IOptions<NotificationOptions> options,
    ILogger<NotificationScheduler> log) : BackgroundService
{
    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        if (!options.Value.EnableScheduler) return;

        try { await Task.Delay(TimeSpan.FromSeconds(5), stoppingToken); }
        catch (OperationCanceledException) { return; }

        while (!stoppingToken.IsCancellationRequested)
        {
            try
            {
                var notifications = services.GetRequiredService<INotificationService>();
                var clock = services.GetRequiredService<IClock>();
                var created = await notifications.RunScheduledChecksAsync(clock.Today, stoppingToken);
                log.LogInformation("Daily notification checks finished. {Count} new notification(s).", created);
            }
            catch (Exception ex)
            {
                log.LogError(ex, "Daily notification checks failed. The next run will try again.");
            }

            try { await Task.Delay(TimeSpan.FromHours(24), stoppingToken); }
            catch (OperationCanceledException) { break; }
        }
    }
}

/// <summary>
/// Person B's seeder: a waitlist of 3 patrons on one borrowed item, plus "Borrowed" notifications
/// for the existing demo loans. (Person A's SeedRunner will call this logic once it exists.)
/// </summary>
public sealed class ReservationAndNotificationSeeder(
    DemoLibraryStore store,
    ReservationService reservations,
    NotificationService notifications) : IHostedService
{
    public async Task StartAsync(CancellationToken cancellationToken)
    {
        // Dune (item 101) is borrowed by Dan. Alice, Bob and Carol wait in that order.
        await reservations.PlaceAsync(101, 1, ReservationSource.Web, cancellationToken);
        await reservations.PlaceAsync(101, 2, ReservationSource.Kiosk, cancellationToken);
        await reservations.PlaceAsync(101, 3, ReservationSource.Desk, cancellationToken);

        foreach (var loan in store.LoansSnapshot())
        {
            await notifications.RecordAsync(
                new NotificationRequest(NotificationType.Borrowed, loan.BorrowerId, loan.ItemId, loan.LoanId),
                cancellationToken);
        }
    }

    public Task StopAsync(CancellationToken cancellationToken) => Task.CompletedTask;
}
