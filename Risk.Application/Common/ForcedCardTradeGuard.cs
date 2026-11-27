using Risk.Application.Interfaces.Repository;
using Risk.Domain.Entities;

namespace Risk.Application.Common;

/// <summary>
/// Fetches a player's hand and lets the game decide whether they must trade cards first.
/// The rule itself lives in <see cref="Game.EnsureNoForcedCardTrade"/>.
/// </summary>
public sealed class ForcedCardTradeGuard(IBonusCardRepository bonusCards)
{
    public async Task EnsureCompletedAsync(
        Game game, Guid playerId, CancellationToken cancellationToken = default)
    {
        var hand = await bonusCards.GetByPlayerIdAsync(game.Id, playerId, cancellationToken);
        game.EnsureNoForcedCardTrade(hand.Count);
    }

    /// <summary>
    /// How many cards the player whose turn it is holds, so responses can tell whether they must trade
    /// (<see cref="Game.MustTradeCards"/>). 0 when nobody has the turn.
    /// </summary>
    public async Task<int> CurrentPlayerHandSizeAsync(Game game, CancellationToken cancellationToken = default) =>
        game.CurrentPlayerId is { } playerId
            ? (await bonusCards.GetByPlayerIdAsync(game.Id, playerId, cancellationToken)).Count
            : 0;
}
