using Risk.Application.Exceptions;
using Risk.Application.Interfaces;
using Risk.Application.Interfaces.Repository;
using Risk.Domain.Entities;
using Risk.Domain.Exceptions;
using Risk.Domain.Map;

namespace Risk.Application.Common;

/// <summary>
/// What the current user is allowed to see when a game uses fog of war.
/// Every use case asks here, so the visibility rules exist in one place.
/// </summary>
public sealed class FogOfWar(ICurrentUser currentUser, IMapRepository mapRepository)
{
    public bool IsActive(Game game) => game.Settings?.HasFogOfWar == true;

    /// <summary>The current user's player in this game, or null for a host who is not playing.</summary>
    public Guid? ViewerPlayerId(Game game) =>
        game.Players.FirstOrDefault(player => player.UserId == currentUser.UserId)?.Id;

    /// <summary>The territories the current user can see, or null when the game has no fog of war.</summary>
    public async Task<IReadOnlySet<string>?> VisibleTerritoriesAsync(
        Game game, CancellationToken cancellationToken = default)
    {
        if (!IsActive(game))
            return null;

        var viewer = ViewerPlayerId(game)
                     ?? throw new ForbiddenException("You are not a player in this game.");
        var graph = await mapRepository.GetGraphAsync(cancellationToken);
        return game.BoardOn(graph).VisibleTo(viewer);
    }

    /// <summary>Rejects a target the player cannot see, so fog cannot be probed by attacking.</summary>
    public void EnsureTargetVisible(Game game, MapGraph graph, Guid playerId, Territory target)
    {
        if (IsActive(game) && !game.BoardOn(graph).VisibleTo(playerId).Contains(target.Identifier))
            throw new BusinessRuleException("The target territory is hidden by fog of war.");
    }
}
