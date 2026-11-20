using Risk.Application.Common;
using Risk.Application.Exceptions;
using Risk.Application.Interfaces.Repository;
using Risk.Application.Interfaces.Services;
using Risk.Domain.Entities;

namespace Risk.Application.Games;

/// <summary>
/// Relays the current player's territory selection to the other participants, live. The selection is
/// only a visual hint: nothing is saved, the game's version does not change and game rules (adjacency,
/// ownership, phase) are not checked, since the real actions are validated by their own endpoints.
/// </summary>
public sealed class TerritorySelectionService(
    GameAccess access,
    IMapRepository map,
    IGameRealtime realtime)
{
    public async Task SelectAsync(
        Guid gameId, Guid userId, SelectTerritoriesRequest request, CancellationToken cancellationToken = default)
    {
        var (game, player) = await access.RequireCurrentTurnAsync(gameId, userId, cancellationToken);
        var source = FindTerritory(game, request.SourceTerritoryId, "sourceTerritoryId");
        var target = FindTerritory(game, request.TargetTerritoryId, "targetTerritoryId");

        // The map is only needed to work out what each receiver can see.
        var graph = game.Settings?.HasFogOfWar == true ? await map.GetGraphAsync(cancellationToken) : null;

        // Sent per user, so every one of the caller's own connections is skipped.
        foreach (var receiver in game.ParticipantUserIds.Where(id => id != userId))
        {
            var visible = graph is null ? null : FogOfWar.VisibleTerritoriesForUser(game, graph, receiver);
            await realtime.SendSelectionToUserInGameAsync(game.Id, receiver, new TerritorySelectionDto(
                game.Id, player.Id, IdIfVisible(source, visible), IdIfVisible(target, visible)));
        }
    }

    private static Territory? FindTerritory(Game game, Guid? territoryId, string field) =>
        territoryId is not { } id
            ? null
            : game.Territories.FirstOrDefault(territory => territory.Id == id)
              ?? throw new ValidationFailedException(field, "The territory is not part of this game.");

    /// <summary>The territory's id, or null when the receiver cannot see it. No fog of war: everything is visible.</summary>
    private static Guid? IdIfVisible(Territory? territory, IReadOnlySet<string>? visible) =>
        territory is not null && (visible is null || visible.Contains(territory.Identifier))
            ? territory.Id
            : null;
}
