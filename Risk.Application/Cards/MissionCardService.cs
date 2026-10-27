using Risk.Application.Common;
using Risk.Application.Exceptions;
using Risk.Application.Interfaces;
using Risk.Application.Interfaces.Repository;
using Risk.Domain.Entities;

namespace Risk.Application.Cards;

/// <summary>Use cases for secret mission cards.</summary>
public sealed class MissionCardService(
    IMissionCardRepository cards,
    IUnitOfWork unitOfWork,
    ICurrentUser currentUser,
    GameAccess access)
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
        GameAccess.RequirePlayerInGame(game, request.PlayerId);
        return await ChangeAsync(gameId, id, card => card.AssignToPlayer(request.PlayerId), ct);
    }

    public async Task<MissionCardResponse> CompleteAsync(Guid gameId, Guid id, CancellationToken ct = default)
    {
        await access.RequireParticipantAsync(gameId, ct);
        return await ChangeAsync(gameId, id, card => card.Complete(), ct);
    }

    private async Task<MissionCardResponse> ChangeAsync(Guid gameId, Guid id, Action<MissionCard> change, CancellationToken ct)
    {
        var card = await cards.GetAsync(gameId, id, ct) ?? throw new NotFoundException("Mission card", id);
        change(card);
        await unitOfWork.SaveChangesAsync(ct);
        return MissionCardResponse.From(card);
    }
}
