using Risk.Application.Common;
using Risk.Application.Exceptions;
using Risk.Application.Interfaces;
using Risk.Application.Interfaces.Repository;
using Risk.Domain.Entities;
using Risk.Domain.Exceptions;
using Risk.Domain.Services;

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
        if (request.CardIds is null || request.CardIds.Count != CardTradeRules.CardsPerTrade ||
            request.CardIds.Distinct().Count() != CardTradeRules.CardsPerTrade)
            throw new DomainValidationException("Exactly three different cards must be traded.");

        var selected = await CardsFromHandAsync(gameId, player.Id, request.CardIds, ct);
        var result = game.TradeBonusCards(player.Id, selected);
        await unitOfWork.SaveChangesAsync(ct);
        return new TradeCardsResponse(result.ReserveTroops, result.BonusTerritoryId, result.BonusTroops);
    }

    /// <summary>
    /// What trading the cards would give, without saving anything. Runs the same calculation as
    /// <see cref="TradeAsync"/>, but any number of cards may be sent and the phase is not checked.
    /// </summary>
    public async Task<TradePreviewResponse> PreviewTradeAsync(
        Guid gameId, TradeCardsRequest request, CancellationToken ct = default)
    {
        var game = await access.RequireParticipantWithDetailsAsync(gameId, ct);
        var player = access.RequireCurrentPlayer(game);
        var selected = await CardsFromHandAsync(gameId, player.Id, request.CardIds ?? [], ct);
        return TradePreviewResponse.From(game.PreviewCardTrade(player.Id, selected));
    }

    /// <summary>
    /// The player's cards with the given ids, kept in request order: it decides which card's territory
    /// gets the territory bonus. Refuses ids that are not in the player's hand.
    /// </summary>
    private async Task<List<BonusCard>> CardsFromHandAsync(
        Guid gameId, Guid playerId, IReadOnlyList<Guid> cardIds, CancellationToken ct)
    {
        var hand = await cards.GetByPlayerIdAsync(gameId, playerId, ct);
        var selected = cardIds
            .Select(id => hand.FirstOrDefault(card => card.Id == id))
            .OfType<BonusCard>()
            .ToList();
        if (selected.Count != cardIds.Count)
            throw new ForbiddenException("All selected cards must belong to your hand.");
        return selected;
    }
}
