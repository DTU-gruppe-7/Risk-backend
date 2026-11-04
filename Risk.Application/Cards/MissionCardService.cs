using Risk.Application.Common;
using Risk.Application.Exceptions;
using Risk.Application.Interfaces;
using Risk.Application.Interfaces.Repository;
using Risk.Domain.Entities;
using Risk.Domain.Exceptions;

namespace Risk.Application.Cards;

/// <summary>Use cases for secret mission cards.</summary>
public sealed class MissionCardService(
    IMissionCardRepository cards,
    IUnitOfWork unitOfWork,
    ICurrentUser currentUser,
    GameAccess access,
    IMapRepository mapRepository)
{
    /// <summary>
    /// The current user's own mission cards in the game. Missions are secret,
    /// so other players' cards are never returned.
    /// </summary>
    public async Task<IReadOnlyList<MissionCardResponse>> GetMineAsync(Guid gameId, CancellationToken ct = default)
    {
        var game = await access.RequireParticipantAsync(gameId, ct);
        var me = game.Players.FirstOrDefault(p => p.UserId == currentUser.UserId);
        if (me is null) return [];

        var list = await cards.GetByGameIdAsync(gameId, me.Id, ct);
        return list.Select(MissionCardResponse.From).ToList();
    }

    public async Task<MissionCardResponse> AssignAsync(Guid gameId, Guid id, AssignMissionRequest request, CancellationToken ct = default)
    {
        var game = await access.RequireParticipantAsync(gameId, ct);
        EnsureInProgress(game);
        GameAccess.RequirePlayerInGame(game, request.PlayerId);
        return await ChangeAsync(gameId, id, card => card.AssignToPlayer(request.PlayerId), ct);
    }

    public async Task<MissionCardResponse> CompleteAsync(Guid gameId, Guid id, CancellationToken ct = default)
    {
        var game = await access.RequireParticipantWithDetailsAsync(gameId, ct);
        if (game.State != GameState.InProgress)
            throw new BusinessRuleException("A mission can only be completed while the game is in progress.");
        if (game.Settings?.HasMissions != true)
            throw new BusinessRuleException("Mission victory is not enabled for this game.");

        var player = access.RequireCurrentPlayer(game);
        var map = await mapRepository.GetGraphAsync(ct);
        var card = await cards.GetAsync(gameId, id, ct)
                   ?? throw new NotFoundException("Mission card", id);

        if (card.PlayerId != player.Id)
            throw new ForbiddenException("You can only complete your own mission.");

        card.Complete(game, map);
        game.FinishWithWinner(player.Id);
        await unitOfWork.SaveChangesAsync(ct);
        return MissionCardResponse.From(card);
    }

    private async Task<MissionCardResponse> ChangeAsync(Guid gameId, Guid id, Action<MissionCard> change, CancellationToken ct)
    {
        var card = await cards.GetAsync(gameId, id, ct) ?? throw new NotFoundException("Mission card", id);
        change(card);
        await unitOfWork.SaveChangesAsync(ct);
        return MissionCardResponse.From(card);
    }

    private static void EnsureInProgress(Game game)
    {
        if (game.State != GameState.InProgress)
            throw new BusinessRuleException("Mission cards can only be changed while the game is in progress.");
    }
}
