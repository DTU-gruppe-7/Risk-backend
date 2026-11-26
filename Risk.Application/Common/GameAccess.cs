using Risk.Application.Exceptions;
using Risk.Application.Interfaces;
using Risk.Application.Interfaces.Repository;
using Risk.Domain.Entities;
using Risk.Domain.Exceptions;

namespace Risk.Application.Common;

/// <summary>
/// Loads a game and checks what the current user may do with it.
/// Every use case asks here, so the access rules exist in one place.
/// </summary>
public sealed class GameAccess(
    IGameRepository games,
    ICurrentUser currentUser)
{
    public async Task<Game> RequireGameAsync(Guid gameId, CancellationToken ct = default) =>
        await games.GetAsync(gameId, ct) ?? throw new NotFoundException("Game", gameId);

    public async Task EnsureGameExistsAsync(Guid gameId, CancellationToken ct = default)
    {
        if (!await games.ExistsAsync(gameId, ct))
            throw new NotFoundException("Game", gameId);
    }

    /// <summary>The game, if the current user is its host or one of its players.</summary>
    public Task<Game> RequireParticipantAsync(Guid gameId, CancellationToken ct = default) =>
        RequireParticipantAsync(gameId, currentUser.UserId, ct);

    /// <summary>
    /// The game, if the given user is its host or one of its players.
    /// For callers that know the user without an HTTP request, such as realtime hubs.
    /// </summary>
    public async Task<Game> RequireParticipantAsync(Guid gameId, Guid userId, CancellationToken ct = default)
    {
        var game = await RequireGameAsync(gameId, ct);
        if (!game.HasParticipant(userId))
            throw new ForbiddenException("You are not part of this game.");
        return game;
    }

    public async Task<Game> RequireParticipantWithDetailsAsync(Guid gameId, CancellationToken ct = default)
    {
        var game = await games.GetByIdWithDetailsAsync(gameId, ct)
                   ?? throw new NotFoundException("Game", gameId);
        if (!game.HasParticipant(currentUser.UserId))
            throw new ForbiddenException("You are not part of this game.");
        return game;
    }

    /// <summary>
    /// The game (with details) and the given user's player, if the game is in progress and it is
    /// that player's turn. For callers that know the user without an HTTP request, such as realtime hubs.
    /// </summary>
    public async Task<(Game Game, Player Player)> RequireCurrentTurnAsync(
        Guid gameId, Guid userId, CancellationToken ct = default)
    {
        var game = await games.GetByIdWithDetailsAsync(gameId, ct)
                   ?? throw new NotFoundException("Game", gameId);
        var player = game.FindPlayerByUser(userId)
                     ?? throw new ForbiddenException("You are not a player in this game.");
        if (player.IsEliminated)
            throw new ForbiddenException("You have been eliminated from this game.");
        if (game.State != GameState.InProgress)
            throw new BusinessRuleException("The game is not in progress.");
        game.EnsureCurrentTurn(player.Id);
        return (game, player);
    }

    public Player RequireCurrentPlayer(Game game) =>
        game.FindPlayerByUser(currentUser.UserId)
        ?? throw new ForbiddenException("You are not a player in this game.");

    /// <summary>The game, if the current user is its host.</summary>
    public async Task<Game> RequireHostAsync(Guid gameId, CancellationToken ct = default)
    {
        var game = await RequireGameAsync(gameId, ct);
        if (!game.IsHost(currentUser.UserId))
            throw new ForbiddenException("Only the host can do this.");
        return game;
    }

    public async Task<Game> RequireHostWithDetailsAsync(Guid gameId, CancellationToken ct = default)
    {
        var game = await games.GetByIdWithDetailsAsync(gameId, ct)
                   ?? throw new NotFoundException("Game", gameId);
        if (!game.IsHost(currentUser.UserId))
            throw new ForbiddenException("Only the host can do this.");
        return game;
    }

    /// <summary>Validates that a player id from the request belongs to the game.</summary>
    public static Player RequirePlayerInGame(Game game, Guid playerId) =>
        game.FindPlayer(playerId)
        ?? throw new ValidationFailedException("playerId", "The player is not part of this game.");
}
