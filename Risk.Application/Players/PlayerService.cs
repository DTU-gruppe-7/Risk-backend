using Risk.Application.Common;
using Risk.Application.Exceptions;
using Risk.Application.Interfaces;
using Risk.Application.Interfaces.Repository;
using Risk.Domain.Entities;

namespace Risk.Application.Players;

/// <summary>
/// Use cases for players in a game. Players are part of the <see cref="Game"/>
/// aggregate, so every change goes through the game and its rules.
/// </summary>
public sealed class PlayerService(
    IPlayerRepository players,
    IUnitOfWork unitOfWork,
    ICurrentUser currentUser,
    GameAccess access)
{
    public async Task<IReadOnlyList<PlayerDetailResponse>> GetAllAsync(Guid gameId, CancellationToken ct = default)
    {
        await access.EnsureGameExistsAsync(gameId, ct);
        var list = await players.GetPlayersByGameIdAsync(gameId, ct);
        return list.Select(PlayerDetailResponse.From).ToList();
    }

    public async Task<PlayerDetailResponse> GetByIdAsync(Guid gameId, Guid id, CancellationToken ct = default)
    {
        var player = await players.GetAsync(gameId, id, ct) ?? throw new NotFoundException("Player", id);
        return PlayerDetailResponse.From(player);
    }

    /// <summary>Joins the game as the current user.</summary>
    public async Task<PlayerDetailResponse> JoinAsync(Guid gameId, JoinGameRequest request, CancellationToken ct = default)
    {
        var game = await access.RequireGameAsync(gameId, ct);
        var player = game.AddPlayer(currentUser.UserId, request.Color);

        await unitOfWork.SaveChangesAsync(ct);
        return PlayerDetailResponse.From(player);
    }

    /// <summary>Removes the player from the game. Only the player themselves may leave.</summary>
    public async Task LeaveAsync(Guid gameId, Guid id, CancellationToken ct = default)
    {
        var game = await access.RequireGameAsync(gameId, ct);
        var player = RequirePlayer(game, id);
        if (player.UserId != currentUser.UserId)
            throw new ForbiddenException("You can only remove yourself from a game.");

        game.RemovePlayer(id);
        await unitOfWork.SaveChangesAsync(ct);
    }

    public Task<PlayerDetailResponse> EliminateAsync(Guid gameId, Guid id, CancellationToken ct = default) =>
        ChangeAsync(gameId, id, game => game.EliminatePlayer(id), ct);

    public Task<PlayerDetailResponse> ReinforceAsync(Guid gameId, Guid id, ReinforceRequest request, CancellationToken ct = default) =>
        ChangeAsync(gameId, id, game => game.ReinforcePlayer(id, request.Troops), ct);

    private async Task<PlayerDetailResponse> ChangeAsync(Guid gameId, Guid id, Action<Game> change, CancellationToken ct)
    {
        var game = await access.RequireParticipantAsync(gameId, ct);
        var player = RequirePlayer(game, id);

        change(game);
        await unitOfWork.SaveChangesAsync(ct);
        return PlayerDetailResponse.From(player);
    }

    private static Player RequirePlayer(Game game, Guid id) =>
        game.FindPlayer(id) ?? throw new NotFoundException("Player", id);
}
