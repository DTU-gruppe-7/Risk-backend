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

/// <summary>A mission card.</summary>
/// <param name="Id">Card ID.</param>
/// <param name="GameId">Owning game.</param>
/// <param name="PlayerId">Assigned player ID, or null if unassigned.</param>
/// <param name="Title">Short mission title.</param>
/// <param name="Description">Full victory condition.</param>
/// <param name="IsCompleted">Whether the mission has been completed.</param>
public sealed record MissionCardResponse(
    Guid Id, Guid GameId, Guid? PlayerId,
    string Title, string Description, bool IsCompleted)
{
    internal static MissionCardResponse From(MissionCard m) =>
        new(m.Id, m.GameId, m.PlayerId, m.Title, m.Description, m.IsCompleted);
}

/// <summary>Payload to assign a mission card to a player.</summary>
/// <param name="PlayerId">Player who receives this mission.</param>
public sealed record AssignMissionRequest(Guid PlayerId);
