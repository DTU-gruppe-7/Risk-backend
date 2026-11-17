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
/// </summary>
public sealed class GameUpdatePublisher(
    IGameRepository games,
    IMapRepository map,
    IGameRealtime realtime)
{
    public async Task PublishAsync(IReadOnlyCollection<Guid> gameIds, CancellationToken cancellationToken = default)
    {
        if (gameIds.Count == 0)
            return;

        var graph = await map.GetGraphAsync(cancellationToken);
        foreach (var gameId in gameIds)
        {
            // Deleted games have nothing left to show.
            if (await games.GetByIdWithDetailsAsync(gameId, cancellationToken) is not { } game)
                continue;

            if (game.Settings?.HasFogOfWar != true)
            {
                await realtime.SendToGameAsync(game.Id, Build(game, graph, viewerUserId: null));
                continue;
            }

            foreach (var userId in Participants(game))
                await realtime.SendToUserInGameAsync(game.Id, userId, Build(game, graph, userId));
        }
    }

    /// <summary>The current state as the given user may see it. Sent when a client starts watching.</summary>
    public async Task<GameUpdateMessage> SnapshotForAsync(
        Guid gameId, Guid userId, CancellationToken cancellationToken = default)
    {
        var game = await games.GetByIdWithDetailsAsync(gameId, cancellationToken)
                   ?? throw new NotFoundException("Game", gameId);
        var graph = await map.GetGraphAsync(cancellationToken);
        return Build(game, graph, userId);
    }

    private static IEnumerable<Guid> Participants(Game game) =>
        game.Players.Select(player => player.UserId).Append(game.HostId).Distinct();

    private static GameUpdateMessage Build(Game game, MapGraph graph, Guid? viewerUserId)
    {
        var territories = game.Territories.OrderBy(territory => territory.Identifier);
        if (game.Settings?.HasFogOfWar != true)
            return new GameUpdateMessage(
                GameDetailResponse.From(game),
                territories.Select(TerritoryResponse.From).ToList());

        var viewerPlayerId = game.Players.FirstOrDefault(player => player.UserId == viewerUserId)?.Id;

        // A host who is not playing has no position on the board, so under fog they see no territories
        // (the REST endpoint refuses them the same way).
        var visible = viewerPlayerId is { } playerId
            ? FogOfWar.VisibleTerritoriesFor(game, graph, playerId)
            : new HashSet<string>();

        return new GameUpdateMessage(
            GameDetailResponse.From(game, viewerPlayerId),
            territories
                .Where(territory => visible!.Contains(territory.Identifier))
                .Select(TerritoryResponse.From)
                .ToList());
    }
}
