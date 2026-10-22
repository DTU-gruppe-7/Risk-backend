using Risk.Application.Common;
using Risk.Application.Exceptions;
using Risk.Application.Interfaces;
using Risk.Application.Interfaces.Repository;
using Risk.Domain.Entities;

namespace Risk.Application.Cards;

/// <summary>Use cases for bonus (territory) cards.</summary>
public sealed class BonusCardService(
    IBonusCardRepository cards,
    IUnitOfWork unitOfWork,
    GameAccess access)
{
    public async Task<IReadOnlyList<BonusCardResponse>> GetAllAsync(Guid gameId, Guid? playerId, CancellationToken ct = default)
    {
        await access.EnsureGameExistsAsync(gameId, ct);
        var list = await cards.GetByGameIdAsync(gameId, playerId, ct);
        return list.Select(BonusCardResponse.From).ToList();
    }

    public async Task<BonusCardResponse> AssignAsync(Guid gameId, Guid id, AssignCardRequest request, CancellationToken ct = default)
    {
        var game = await access.RequireParticipantAsync(gameId, ct);
        GameAccess.RequirePlayerInGame(game, request.PlayerId);
        return await ChangeAsync(gameId, id, card => card.AssignToPlayer(request.PlayerId), ct);
    }

    public async Task<BonusCardResponse> UseAsync(Guid gameId, Guid id, CancellationToken ct = default)
    {
        await access.RequireParticipantAsync(gameId, ct);
        return await ChangeAsync(gameId, id, card => card.MarkAsUsed(), ct);
    }

    private async Task<BonusCardResponse> ChangeAsync(Guid gameId, Guid id, Action<BonusCard> change, CancellationToken ct)
    {
        var card = await cards.GetAsync(gameId, id, ct) ?? throw new NotFoundException("Bonus card", id);
        change(card);
        await unitOfWork.SaveChangesAsync(ct);
        return BonusCardResponse.From(card);
    }
}
