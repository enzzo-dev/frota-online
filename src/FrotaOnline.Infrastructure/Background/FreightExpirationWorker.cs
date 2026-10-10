using FrotaOnline.Application.Marketplace;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;

namespace FrotaOnline.Infrastructure.Background;

/// <summary>
/// Runs <see cref="IFreightExpirationService"/> every 30 seconds so freights and proposals close
/// right after their window ends. A durable job runner (Hangfire) can replace this later without
/// touching the service.
/// </summary>
internal sealed class FreightExpirationWorker : BackgroundService
{
    private static readonly TimeSpan Interval = TimeSpan.FromSeconds(30);

    private readonly IServiceScopeFactory _scopeFactory;
    private readonly ILogger<FreightExpirationWorker> _logger;

    public FreightExpirationWorker(IServiceScopeFactory scopeFactory, ILogger<FreightExpirationWorker> logger)
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
                var service = scope.ServiceProvider.GetRequiredService<IFreightExpirationService>();
                var (freights, proposals) = await service.ExpireDueAsync(stoppingToken);
                if (freights + proposals > 0)
                {
                    _logger.LogInformation("Expired {Freights} freights and {Proposals} proposals.", freights, proposals);
                }
            }
            catch (OperationCanceledException) when (stoppingToken.IsCancellationRequested)
            {
                break;
            }
            catch (Exception exception)
            {
                // A failed pass (for example, database not migrated yet) must not stop the worker.
                _logger.LogWarning(exception, "Freight expiration pass failed. It will retry.");
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
