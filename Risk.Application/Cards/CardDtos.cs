using Risk.Domain.Entities;

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

/// <summary>Payload to assign a card to a player.</summary>
/// <param name="PlayerId">Player receiving the card.</param>
public sealed record AssignCardRequest(Guid PlayerId);

/// <summary>Three cards to trade.</summary>
/// <param name="CardIds">If several cards show territories the player owns, the territory bonus goes to the first of them in this order.</param>
public sealed record TradeCardsRequest(IReadOnlyList<Guid> CardIds);

/// <summary>What a card trade gave the player.</summary>
/// <param name="TroopsAwarded">The set's value, added to the reserve. Does not include the territory bonus.</param>
/// <param name="BonusTerritoryId">Territory state id that got the territory bonus, or null when none was given.</param>
/// <param name="BonusTroops">Troops placed directly on that territory (2, or 0 when none).</param>
public sealed record TradeCardsResponse(int TroopsAwarded, Guid? BonusTerritoryId = null, int BonusTroops = 0);

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

/// <summary>Payload to assign a mission card to a player.</summary>
/// <param name="PlayerId">Player who receives this mission.</param>
public sealed record AssignMissionRequest(Guid PlayerId);
