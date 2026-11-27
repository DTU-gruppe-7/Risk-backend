using Risk.Domain.Entities;
using Risk.Domain.Services;

namespace Risk.Application.Cards;

/// <summary>A Risk territory / bonus card.</summary>
/// <param name="Id">Card ID.</param>
/// <param name="GameId">Owning game.</param>
/// <param name="PlayerId">Holder player ID, or null if in draw pile.</param>
/// <param name="Type">Card symbol type.</param>
/// <param name="TerritoryIdentifier">Depicted territory slug, or null for Wild cards.</param>
/// <param name="Used">Whether the card has been traded in.</param>
public sealed record BonusCardResponse(
    Guid Id, Guid GameId, Guid? PlayerId,
    BonusCardType Type, string? TerritoryIdentifier, bool Used)
{
    internal static BonusCardResponse From(BonusCard b) =>
        new(b.Id, b.GameId, b.PlayerId, b.Type, b.TerritoryIdentifier, b.Used);
}

/// <summary>Three cards to trade.</summary>
/// <param name="CardIds">If several cards show territories the player owns, the territory bonus goes to the first of them in this order.</param>
public sealed record TradeCardsRequest(IReadOnlyList<Guid> CardIds);

/// <summary>What a card trade gave the player.</summary>
/// <param name="TroopsAwarded">The set's value, added to the reserve. Does not include the territory bonus.</param>
/// <param name="BonusTerritoryId">Territory state id that got the territory bonus, or null when none was given.</param>
/// <param name="BonusTroops">Troops placed directly on that territory (2, or 0 when none).</param>
public sealed record TradeCardsResponse(int TroopsAwarded, Guid? BonusTerritoryId = null, int BonusTroops = 0);

/// <summary>What trading the selected cards would give now. Nothing is saved.</summary>
/// <param name="IsValid">The cards are three different cards from the player's hand forming a set that can be traded.</param>
/// <param name="Troops">What the set gives the reserve, or null when not valid.</param>
/// <param name="BonusTerritoryId">Territory state id that would get the territory bonus, or null when none.</param>
/// <param name="BonusTroops">Troops that would be placed directly on that territory (0 when none).</param>
public sealed record TradePreviewResponse(bool IsValid, int? Troops, Guid? BonusTerritoryId, int BonusTroops)
{
    internal static TradePreviewResponse From(CardTradeResult? result) =>
        result is null
            ? new TradePreviewResponse(false, null, null, 0)
            : new TradePreviewResponse(true, result.ReserveTroops, result.BonusTerritoryId, result.BonusTroops);
}

/// <summary>The card trade rules, the same for every game. Built from the rules the trades themselves use.</summary>
/// <param name="FixedSets">Every kind of set without wild cards and its value with <see cref="CardTradeMode.Fixed"/>.</param>
/// <param name="FirstProgressiveValues">The first set values with <see cref="CardTradeMode.Progressive"/>, in trade order.</param>
/// <param name="OwnedTerritoryBonus">Extra troops on a held territory shown on a traded card (once per turn).</param>
/// <param name="CardsPerTrade">Cards in a set.</param>
/// <param name="ForcedTradeHandSize">A player holding this many cards must trade in their Deployment phase.</param>
public sealed record CardTradeRulesResponse(
    IReadOnlyList<CardTradeSetResponse> FixedSets,
    IReadOnlyList<int> FirstProgressiveValues,
    int OwnedTerritoryBonus,
    int CardsPerTrade,
    int ForcedTradeHandSize)
{
    /// <summary>Enough progressive values to show where the fixed steps end and +5 begins.</summary>
    private const int ProgressiveValuesShown = 7;

    public static CardTradeRulesResponse Current { get; } = new(
        CardTradeRules.FixedSets.Select(set => new CardTradeSetResponse(set.Types, set.Troops)).ToList(),
        CardTradeRules.ProgressiveValues(0, ProgressiveValuesShown),
        CardTradeRules.TerritoryBonusTroops,
        CardTradeRules.CardsPerTrade,
        Game.ForcedCardTradeThreshold);
}

/// <summary>A kind of set and the troops it gives.</summary>
public sealed record CardTradeSetResponse(IReadOnlyList<BonusCardType> Types, int Troops);

/// <summary>
/// Pushed over the game hub to the player who got new cards, and to nobody else: cards are secret.
/// Sent once the change is saved.
/// </summary>
/// <param name="Cards">Only the new cards, not the whole hand.</param>
public sealed record CardsReceivedMessage(Guid GameId, IReadOnlyList<BonusCardResponse> Cards, CardsReceivedReason Reason);

/// <summary>Why a player got cards.</summary>
public enum CardsReceivedReason
{
    /// <summary>The card for conquering a territory, drawn when the player's turn ended.</summary>
    Conquest,

    /// <summary>The cards of a player the receiver knocked out.</summary>
    Elimination
}

/// <summary>A mission card.</summary>
/// <param name="Id">Card ID.</param>
/// <param name="GameId">Owning game.</param>
/// <param name="PlayerId">Assigned player ID, or null if unassigned.</param>
/// <param name="Title">Short mission title.</param>
/// <param name="Description">Full victory condition.</param>
/// <param name="IsCompleted">Whether the mission has been completed.</param>
public sealed record MissionCardResponse(
    Guid Id, Guid GameId, Guid? PlayerId,
    string Title, string Description, bool IsCompleted,
    MissionObjectiveType ObjectiveType, string? TargetIdentifier, Guid? TargetPlayerId)
{
    internal static MissionCardResponse From(MissionCard m) =>
        new(m.Id, m.GameId, m.PlayerId, m.Title, m.Description, m.IsCompleted,
            m.ObjectiveType, m.TargetIdentifier, m.TargetPlayerId);
}
