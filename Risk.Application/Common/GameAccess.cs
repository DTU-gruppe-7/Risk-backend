using Risk.Application.Exceptions;
using Risk.Application.Interfaces;
using Risk.Application.Interfaces.Repository;
using Risk.Domain.Entities;

namespace Risk.Application.Common;

/// <summary>
/// Loads a game and checks what the current user may do with it.
/// Every use case asks here, so the access rules exist in one place.
/// </summary>
public sealed class GameAccess(IGameRepository games, ICurrentUser currentUser)
{
    public async Task<Game> RequireGameAsync(Guid gameId, CancellationToken ct = default) =>
        await games.GetAsync(gameId, ct) ?? throw new NotFoundException("Game", gameId);

    public async Task EnsureGameExistsAsync(Guid gameId, CancellationToken ct = default)
    {
        if (!await games.ExistsAsync(gameId, ct))
            throw new NotFoundException("Game", gameId);
    }

    /// <summary>The game, if the current user is its host or one of its players.</summary>
    public async Task<Game> RequireParticipantAsync(Guid gameId, CancellationToken ct = default)
    {
        var game = await RequireGameAsync(gameId, ct);
        if (!game.HasParticipant(currentUser.UserId))
            throw new ForbiddenException("You are not part of this game.");
        return game;
    }

    /// <summary>The game, if the current user is its host.</summary>
    public async Task<Game> RequireHostAsync(Guid gameId, CancellationToken ct = default)
    {
        var game = await RequireGameAsync(gameId, ct);
        if (!game.IsHost(currentUser.UserId))
            throw new ForbiddenException("Only the host can do this.");
        return game;
    }

    /// <summary>Validates that a player id from the request belongs to the game.</summary>
    public static Player RequirePlayerInGame(Game game, Guid playerId) =>
        game.FindPlayer(playerId)
        ?? throw new ValidationFailedException("playerId", "The player is not part of this game.");
}
