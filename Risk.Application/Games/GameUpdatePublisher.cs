using Risk.Application.Cards;
using Risk.Application.Common;
using Risk.Application.Exceptions;
using Risk.Application.Interfaces.Repository;
using Risk.Application.Interfaces.Services;
using Risk.Application.Territories;
using Risk.Domain.Entities;
using Risk.Domain.Map;

namespace Risk.Application.Games;

/// <summary>
/// Decides what each watcher of a game receives after a change. Without fog of war everyone
/// sees the same board, so one message goes to the whole game. With fog of war every participant
/// gets their own message, filtered by the same rules as the REST endpoints.
/// Does not use the current user, because it also runs from background work (turn timeouts).
/// <para>
/// Cards that changed hands are announced only to the player who got them (<see cref="CardsReceivedMessage"/>),
/// after the game update for the same change.
/// </para>
/// </summary>
public sealed class GameUpdatePublisher(
    IGameRepository games,
    IMapRepository map,
    IGameRealtime realtime,
    ForcedCardTradeGuard forcedCardTrade)
{
    /// <param name="gameIds">The games that changed.</param>
    /// <param name="cardHandovers">Bonus cards that got a new owner in the same change.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    public async Task PublishAsync(
        IReadOnlyCollection<Guid> gameIds,
        IReadOnlyCollection<CardHandover>? cardHandovers = null,
        CancellationToken cancellationToken = default)
    {
        if (gameIds.Count == 0)
            return;

        var graph = await map.GetGraphAsync(cancellationToken);
        foreach (var gameId in gameIds)
        {
            // Deleted games have nothing left to show.
            if (await games.GetByIdWithDetailsAsync(gameId, cancellationToken) is not { } game)
                continue;

            await SendGameUpdateAsync(game, graph, cancellationToken);
            await SendCardsReceivedAsync(game, cardHandovers ?? []);
        }
    }

    private async Task SendGameUpdateAsync(Game game, MapGraph graph, CancellationToken cancellationToken)
    {
        var handSize = await forcedCardTrade.CurrentPlayerHandSizeAsync(game, cancellationToken);
        if (game.Settings?.HasFogOfWar != true)
        {
            await realtime.SendToGameAsync(game.Id, Build(game, graph, handSize, viewerUserId: null));
            return;
        }

        foreach (var userId in game.ParticipantUserIds)
            await realtime.SendToUserInGameAsync(game.Id, userId, Build(game, graph, handSize, userId));
    }

    /// <summary>One message per receiving player and reason, sent to that player's user only.</summary>
    private async Task SendCardsReceivedAsync(
        Game game, IReadOnlyCollection<CardHandover> cardHandovers)
    {
        var received = cardHandovers
            .Where(handover => handover.Card.GameId == game.Id && handover.Card.PlayerId is not null)
            .GroupBy(handover => (PlayerId: handover.Card.PlayerId!.Value, handover.Reason));

        foreach (var group in received)
        {
            if (game.FindPlayer(group.Key.PlayerId) is not { } player)
                continue;

            var cards = group.Select(handover => BonusCardResponse.From(handover.Card)).ToList();
            await realtime.SendCardsReceivedToUserInGameAsync(
                game.Id, player.UserId, new CardsReceivedMessage(game.Id, cards, group.Key.Reason));
        }
    }

    /// <summary>The current state as the given user may see it. Sent when a client starts watching.</summary>
    public async Task<GameUpdateMessage> SnapshotForAsync(
        Guid gameId, Guid userId, CancellationToken cancellationToken = default)
    {
        var game = await games.GetByIdWithDetailsAsync(gameId, cancellationToken)
                   ?? throw new NotFoundException("Game", gameId);
        var graph = await map.GetGraphAsync(cancellationToken);
        var handSize = await forcedCardTrade.CurrentPlayerHandSizeAsync(game, cancellationToken);
        return Build(game, graph, handSize, userId);
    }

    /// <param name="currentPlayerHandSize">How many cards the player whose turn it is holds.</param>
    private static GameUpdateMessage Build(Game game, MapGraph graph, int currentPlayerHandSize, Guid? viewerUserId)
    {
        var territories = game.Territories.OrderBy(territory => territory.Identifier);
        if (game.Settings?.HasFogOfWar != true)
            return new GameUpdateMessage(
                GameDetailResponse.From(game, visibleTerritories: null, currentPlayerHandSize),
                territories.Select(TerritoryResponse.From).ToList());

        var visible = viewerUserId is { } userId
            ? FogOfWar.VisibleTerritoriesForUser(game, graph, userId)!
            : new HashSet<string>();

        return new GameUpdateMessage(
            GameDetailResponse.From(game, visible, currentPlayerHandSize),
            territories
                .Where(territory => visible.Contains(territory.Identifier))
                .Select(TerritoryResponse.From)
                .ToList());
    }
}

/// <summary>
/// A bonus card that got a new owner in a saved change. Found by the unit of work, so no use case has to
/// remember to report it.
/// </summary>
/// <param name="PreviousPlayerId">Who held the card before, or null when it came from the draw pile.</param>
public sealed record CardHandover(BonusCard Card, Guid? PreviousPlayerId)
{
    /// <summary>A card from the draw pile is the card for conquering; a card from another player was taken from them.</summary>
    public CardsReceivedReason Reason =>
        PreviousPlayerId is null ? CardsReceivedReason.Conquest : CardsReceivedReason.Elimination;
}
