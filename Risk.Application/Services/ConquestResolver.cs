using Risk.Application.Interfaces.Repository;
using Risk.Domain.Entities;
using Risk.Domain.Exceptions;

namespace Risk.Application.Services;

/// <summary>What a resolved conquest changed, for the caller's response.</summary>
public sealed record ConquestOutcome(bool BonusCardAwarded, bool PlayerEliminated);

/// <summary>
/// Everything that follows once a territory has changed hands: eliminating the defeated player
/// and taking over their cards, awarding the conquest card and checking for a winner.
/// Attacks, blitz attacks and manually completed conquests all end here, so the rules exist once.
/// </summary>
public sealed class ConquestResolver(
    IBonusCardRepository bonusCards,
    IMissionCardRepository missionCards,
    IMapRepository mapRepository)
{
    public async Task<ConquestOutcome> ResolveAsync(
        Game game, Guid attackerId, Guid defeatedPlayerId, CancellationToken cancellationToken = default)
    {
        var attackerHand = (await bonusCards.GetByPlayerIdAsync(game.Id, attackerId, cancellationToken)).Count;

        var takenCards = await EliminateAsync(game, defeatedPlayerId, attackerId, cancellationToken);
        var eliminated = takenCards is not null;
        attackerHand += takenCards ?? 0;

        var awarded = await AwardConquestCardAsync(game, attackerId, cancellationToken);
        if (awarded)
            attackerHand++;

        await EvaluateVictoryAsync(game, attackerId, cancellationToken);

        // Taking over an eliminated player's cards can leave the attacker with a hand that has to be
        // traded down: back to Deployment, then they place the new troops freely and attack again.
        if (eliminated &&
            game.State == GameState.InProgress &&
            attackerHand >= Game.ForcedCardTradeThreshold)
        {
            game.ReturnToDeploymentForCardTrade();
        }

        return new ConquestOutcome(awarded, eliminated);
    }

    /// <summary>Returns the number of cards taken over, or null when nobody was eliminated.</summary>
    private async Task<int?> EliminateAsync(
        Game game, Guid defeatedPlayerId, Guid attackerId, CancellationToken cancellationToken)
    {
        if (!game.EliminateIfDefeated(defeatedPlayerId))
            return null;

        var defeatedCards = await bonusCards.GetByPlayerIdAsync(
            game.Id, defeatedPlayerId, cancellationToken);
        foreach (var card in defeatedCards)
            card.AssignToPlayer(attackerId);

        var mission = await missionCards.GetByPlayerIdAsync(
            game.Id, defeatedPlayerId, cancellationToken);
        mission?.ReleaseFromPlayer();

        return defeatedCards.Count;
    }

    private async Task<bool> AwardConquestCardAsync(
        Game game, Guid attackerId, CancellationToken cancellationToken)
    {
        if (!game.RecordConquest(attackerId))
            return false;

        var card = await bonusCards.GetNextUnassignedAsync(game.Id, cancellationToken);
        if (card is null)
            return false;

        var player = game.FindPlayer(attackerId)
                     ?? throw new BusinessRuleException("Attacking player is not part of the game.");
        card.AssignToPlayer(player.Id);
        return true;
    }

    private async Task EvaluateVictoryAsync(
        Game game, Guid playerId, CancellationToken cancellationToken)
    {
        if (game.Settings?.HasMissions != true)
        {
            if (game.ControlsAllTerritories(playerId))
                game.FinishWithWinner(playerId);
            return;
        }

        var mission = await missionCards.GetByPlayerIdAsync(game.Id, playerId, cancellationToken);
        if (mission is null || mission.IsCompleted)
            return;

        var map = await mapRepository.GetGraphAsync(cancellationToken);
        if (!mission.IsSatisfiedBy(game, map, playerId))
            return;

        mission.Complete(game, map);
        game.FinishWithWinner(playerId);
    }
}
