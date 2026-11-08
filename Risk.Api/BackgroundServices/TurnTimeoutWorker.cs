using Risk.Application.Games;

namespace Risk.Api.BackgroundServices;

/// <summary>Runs <see cref="TurnTimeoutService"/> on a timer, independently of incoming HTTP requests.</summary>
public sealed class TurnTimeoutWorker(
    IServiceScopeFactory scopeFactory,
    ILogger<TurnTimeoutWorker> logger) : BackgroundService
{
    private static readonly TimeSpan Interval = TimeSpan.FromSeconds(1);

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        using var timer = new PeriodicTimer(Interval);
        while (await timer.WaitForNextTickAsync(stoppingToken))
        {
            try
            {
                await using var scope = scopeFactory.CreateAsyncScope();
                var timeouts = scope.ServiceProvider.GetRequiredService<TurnTimeoutService>();
                await timeouts.AdvanceTimedOutTurnsAsync(DateTimeOffset.UtcNow, stoppingToken);
            }
            catch (OperationCanceledException) when (stoppingToken.IsCancellationRequested)
            {
                break;
            }
            catch (Exception exception)
            {
                logger.LogError(exception, "Failed to advance timed-out game turns.");
            }
        }
    }
}
