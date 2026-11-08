using Risk.Application.Interfaces;
using Risk.Application.Interfaces.Repository;
using Risk.Application.Services;
using Risk.Domain.Entities;

namespace Risk.Application.Games;

/// <summary>
/// Advances every game whose turn has run out of time. Hosting this on a timer is the job of the
/// API; this class only holds the rules, so it can be used and tested without any host.
/// </summary>
public sealed class TurnTimeoutService(
    IGameRepository games,
    IMapRepository mapRepository,
    ConquestResolver conquestResolver,
    IUnitOfWork unitOfWork)
{
    /// <summary>Returns how many games moved on to the next turn.</summary>
    public async Task<int> AdvanceTimedOutTurnsAsync(
        DateTimeOffset now, CancellationToken cancellationToken = default)
    {
        var graph = await mapRepository.GetGraphAsync(cancellationToken);
        var advanced = 0;
        var changed = false;

        foreach (var game in await games.GetInProgressGamesAsync(cancellationToken))
        {
            if (!game.IsTurnTimedOut(now))
                continue;

            changed = true;
            await CompletePendingConquestAsync(game, cancellationToken);

            // The conquest can end the game (a winner), in which case there is no turn to move on.
            if (game.State != GameState.InProgress)
                continue;

            if (game.AdvanceIfTurnTimedOut(graph, now))
                advanced++;
        }

        if (changed)
            await unitOfWork.SaveChangesAsync(cancellationToken);

        return advanced;
    }

    /// <summary>A pending conquest is completed with one troop, with everything that follows from it.</summary>
    private async Task CompletePendingConquestAsync(Game game, CancellationToken cancellationToken)
    {
        if (game.CompletePendingConquestWithMinimumTroops() is not { } conquest)
            return;

        await conquestResolver.ResolveAsync(
            game, conquest.AttackerId, conquest.DefeatedPlayerId, cancellationToken);
    }
}
