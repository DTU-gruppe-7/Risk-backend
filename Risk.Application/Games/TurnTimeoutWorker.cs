using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Risk.Application.Interfaces;
using Risk.Application.Interfaces.Repository;

namespace Risk.Application.Games;

/// <summary>Advances timed-out turns independently of incoming HTTP requests.</summary>
public sealed class TurnTimeoutWorker(
    IServiceScopeFactory scopeFactory,
    ILogger<TurnTimeoutWorker> logger) : BackgroundService
{
    private readonly ILogger<TurnTimeoutWorker> _logger = logger;

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        using var timer = new PeriodicTimer(TimeSpan.FromSeconds(1));
        while (await timer.WaitForNextTickAsync(stoppingToken))
        {
            try
            {
                await AdvanceTimedOutTurnsAsync(stoppingToken);
            }
            catch (OperationCanceledException) when (stoppingToken.IsCancellationRequested)
            {
                break;
            }
            catch (Exception exception)
            {
                _logger.LogError(exception, "Failed to advance timed-out game turns.");
            }
        }
    }

    private async Task AdvanceTimedOutTurnsAsync(CancellationToken cancellationToken)
    {
        await using var scope = scopeFactory.CreateAsyncScope();
        var games = scope.ServiceProvider.GetRequiredService<IGameRepository>();
        var unitOfWork = scope.ServiceProvider.GetRequiredService<IUnitOfWork>();
        var now = DateTimeOffset.UtcNow;
        var changed = false;

        foreach (var game in await games.GetInProgressGamesAsync(cancellationToken))
        {
            changed |= game.AdvanceIfTurnTimedOut(now);
        }

        if (changed)
            await unitOfWork.SaveChangesAsync(cancellationToken);
    }
}
