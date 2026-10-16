using Library.Core.Configuration;
using Library.Core.Contracts;
using Microsoft.Extensions.Options;

namespace Library.Web.Infrastructure;

/// <summary>
/// The daily background job (decision 10): runs the notification checks once per calendar day, using the same code as the
/// admin "Run checks now" button. It looks at the clock every 15 minutes so a simulated date change is picked up too.
/// </summary>
public sealed class NotificationScheduler(IServiceProvider services, IOptions<NotificationOptions> options, ILogger<NotificationScheduler> log) : BackgroundService
{
    private DateOnly? _lastRun;

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        if (!options.Value.EnableScheduler) { log.LogInformation("Notification scheduler is switched off (Notifications:EnableScheduler)."); return; }
        try { await Task.Delay(TimeSpan.FromSeconds(5), stoppingToken); } catch (OperationCanceledException) { return; }

        while (!stoppingToken.IsCancellationRequested)
        {
            try
            {
                var clock = services.GetRequiredService<IClock>();
                if (_lastRun != clock.Today)
                {
                    var count = await services.GetRequiredService<INotificationService>().RunScheduledChecksAsync(clock.Today, stoppingToken);
                    _lastRun = clock.Today;
                    log.LogInformation("Daily notification checks for {Date}: {Count} new", clock.Today, count);
                }
            }
            catch (Exception ex) when (ex is not OperationCanceledException)
            {
                log.LogError(ex, "The daily notification checks failed; will try again.");
            }
            try { await Task.Delay(TimeSpan.FromMinutes(15), stoppingToken); } catch (OperationCanceledException) { break; }
        }
    }
}
