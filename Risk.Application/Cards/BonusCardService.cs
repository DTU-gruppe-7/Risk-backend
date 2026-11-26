using Risk.Application.Common;
using Risk.Application.Exceptions;
using Risk.Application.Interfaces;
using Risk.Application.Interfaces.Repository;
using Risk.Domain.Entities;
using Risk.Domain.Exceptions;

namespace Risk.Application.Cards;

/// <summary>Use cases for bonus (territory) cards.</summary>
public sealed class BonusCardService(
    IBonusCardRepository cards,
    IUnitOfWork unitOfWork,
    GameAccess access,
    FogOfWar fogOfWar)
{
    public async Task<IReadOnlyList<BonusCardResponse>> GetAllAsync(Guid gameId, Guid? playerId, CancellationToken ct = default)
    {
        var game = await access.RequireParticipantWithDetailsAsync(gameId, ct);
        if (fogOfWar.IsActive(game))
            playerId = access.RequireCurrentPlayer(game).Id;

        var list = await cards.GetByGameIdAsync(gameId, playerId, ct);
        return list.Select(BonusCardResponse.From).ToList();
    }

    public async Task<TradeCardsResponse> TradeAsync(
        Guid gameId, TradeCardsRequest request, CancellationToken ct = default)
    {
        var game = await access.RequireParticipantWithDetailsAsync(gameId, ct);
        var player = access.RequireCurrentPlayer(game);
        if (request.CardIds is null || request.CardIds.Count != 3 ||
            request.CardIds.Distinct().Count() != 3)
            throw new DomainValidationException("Exactly three different cards must be traded.");

        var hand = await cards.GetByPlayerIdAsync(gameId, player.Id, ct);
        // Kept in request order: it decides which card's territory gets the territory bonus.
        var selected = request.CardIds
            .Select(id => hand.FirstOrDefault(card => card.Id == id))
            .OfType<BonusCard>()
            .ToList();
        if (selected.Count != 3)
            throw new ForbiddenException("All selected cards must belong to your hand.");

        var result = game.TradeBonusCards(player.Id, selected);
        await unitOfWork.SaveChangesAsync(ct);
        return new TradeCardsResponse(result.ReserveTroops, result.BonusTerritoryId, result.BonusTroops);
    }
}
