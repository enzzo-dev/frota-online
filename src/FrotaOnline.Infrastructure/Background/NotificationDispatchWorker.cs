using FrotaOnline.Application.Notifications;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;

namespace FrotaOnline.Infrastructure.Background;

/// <summary>
/// Sends the pushes of stored notifications every 5 seconds. The notification row already exists (it was
/// committed with the business change), so a restart or a provider outage never loses the inbox entry; the push
/// is simply picked up on the next pass. Only registered when <c>Jobs:RunInProcess</c> is true (development);
/// in production Cloud Scheduler calls the same dispatcher through an internal endpoint. The batch is leased in
/// the database, so several instances never send the same push.
/// </summary>
internal sealed class NotificationDispatchWorker : BackgroundService
{
    private static readonly TimeSpan Interval = TimeSpan.FromSeconds(5);

    private readonly IServiceScopeFactory _scopeFactory;
    private readonly ILogger<NotificationDispatchWorker> _logger;

    public NotificationDispatchWorker(IServiceScopeFactory scopeFactory, ILogger<NotificationDispatchWorker> logger)
    {
        _scopeFactory = scopeFactory;
        _logger = logger;
    }

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        using var timer = new PeriodicTimer(Interval);
        do
        {
            try
            {
                using var scope = _scopeFactory.CreateScope();
                var dispatcher = scope.ServiceProvider.GetRequiredService<INotificationDispatcher>();
                var settled = await dispatcher.DispatchPendingAsync(stoppingToken);
                if (settled > 0)
                {
                    _logger.LogInformation("Dispatched {Count} push notifications.", settled);
                }
            }
            catch (OperationCanceledException) when (stoppingToken.IsCancellationRequested)
            {
                break;
            }
            catch (Exception exception)
            {
                // A failed pass (for example, database not migrated yet) must not stop the worker.
                _logger.LogWarning(exception, "Notification dispatch pass failed. It will retry.");
            }
        }
        while (await WaitAsync(timer, stoppingToken));
    }

    private static async Task<bool> WaitAsync(PeriodicTimer timer, CancellationToken cancellationToken)
    {
        try
        {
            return await timer.WaitForNextTickAsync(cancellationToken);
        }
        catch (OperationCanceledException)
        {
            return false;
        }
    }
}
