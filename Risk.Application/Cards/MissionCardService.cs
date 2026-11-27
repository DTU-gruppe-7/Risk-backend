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
        var me = game.FindPlayerByUser(currentUser.UserId);
        if (me is null) return [];

        var list = await cards.GetByGameIdAsync(gameId, me.Id, ct);
        return list.Select(MissionCardResponse.From).ToList();
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

}
