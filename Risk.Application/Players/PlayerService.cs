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
    IBonusCardRepository bonusCards,
    IMissionCardRepository missionCards,
    IUnitOfWork unitOfWork,
    ICurrentUser currentUser,
    GameAccess access,
    FogOfWar fogOfWar)
{
    public async Task<IReadOnlyList<PlayerDetailResponse>> GetAllAsync(Guid gameId, CancellationToken ct = default)
    {
        var game = await access.RequireParticipantWithDetailsAsync(gameId, ct);
        var list = await players.GetPlayersByGameIdAsync(gameId, ct);
        return list.Select(player => ToResponse(game, player)).ToList();
    }

    public async Task<PlayerDetailResponse> GetByIdAsync(Guid gameId, Guid id, CancellationToken ct = default)
    {
        var game = await access.RequireParticipantWithDetailsAsync(gameId, ct);
        var player = await players.GetAsync(gameId, id, ct) ?? throw new NotFoundException("Player", id);
        return ToResponse(game, player);
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

    public async Task<PlayerDetailResponse> EliminateAsync(
        Guid gameId, Guid id, CancellationToken ct = default)
    {
        var game = await access.RequireHostWithDetailsAsync(gameId, ct);
        var player = RequirePlayer(game, id);

        if (!game.EliminatePlayer(id))
            return PlayerDetailResponse.From(player);

        var cards = await bonusCards.GetByPlayerIdAsync(gameId, id, ct);
        foreach (var card in cards)
            card.ReturnToDeck();

        var mission = await missionCards.GetByPlayerIdAsync(gameId, id, ct);
        mission?.ReleaseFromPlayer();

        await unitOfWork.SaveChangesAsync(ct);
        return PlayerDetailResponse.From(player);
    }

    private static Player RequirePlayer(Game game, Guid id) =>
        game.FindPlayer(id) ?? throw new NotFoundException("Player", id);

    private PlayerDetailResponse ToResponse(Game game, Player player) =>
        fogOfWar.IsActive(game)
            ? PlayerDetailResponse.From(player, fogOfWar.ViewerPlayerId(game))
            : PlayerDetailResponse.From(player);
}
