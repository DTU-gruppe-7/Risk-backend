using Risk.Domain.Common;
using Risk.Domain.Exceptions;

namespace Risk.Domain.Entities;

/// <summary>
/// A Risk territory card held by a player. Matching sets of three cards
/// can be traded in for bonus reinforcement troops.
/// </summary>
public sealed class BonusCard
{
    public Guid Id { get; private set; } = Guid.CreateVersion7();

    public Guid GameId { get; private set; }

    /// <summary>The player currently holding this card. Null when in the draw pile.</summary>
    public Guid? PlayerId { get; private set; }

    /// <summary>Whether this card has already been traded in / used.</summary>
    public bool Used { get; private set; } = false;

    /// <summary>Card type symbol: Infantry, Cavalry, Artillery, or Wild.</summary>
    public BonusCardType Type { get; private set; }

    /// <summary>
    /// The territory depicted on this card (matches <see cref="Territory.Identifier"/>).
    /// Wild cards have no associated territory.
    /// </summary>
    public string? TerritoryIdentifier { get; private set; }

    // Navigation properties
    public Game? Game { get; private set; }
    public Player? Player { get; private set; }

    // EF Core requires a parameterless constructor
    private BonusCard() { }

    public BonusCard(Guid gameId, BonusCardType type, string? territoryIdentifier = null)
    {
        GameId = Guard.NotEmpty(gameId, nameof(gameId));
        Type = type;
        TerritoryIdentifier = territoryIdentifier?.Trim().ToLowerInvariant();
    }

    public void AssignToPlayer(Guid playerId)
    {
        Guard.NotEmpty(playerId, nameof(playerId));
        if (Used)
            throw new BusinessRuleException("Cannot assign a card that has already been used.");

        PlayerId = playerId;
    }

    public void MarkAsUsed()
    {
        if (Used)
            throw new BusinessRuleException("Card has already been used.");

        Used = true;
        PlayerId = null;
    }
}

public enum BonusCardType
{
    Infantry,
    Cavalry,
    Artillery,
    Wild
}
