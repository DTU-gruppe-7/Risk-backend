using Risk.Domain.Common;
using Risk.Domain.Exceptions;

namespace Risk.Domain.Entities;

/// <summary>
/// Represents a user's participation in a single Risk game.
/// Bridges <see cref="User"/> and <see cref="Game"/>. Part of the <see cref="Game"/>
/// aggregate, so state changes go through the game (hence the internal methods).
/// </summary>
public sealed class Player
{
    public Guid Id { get; private set; } = Guid.CreateVersion7();

    public Guid GameId { get; private set; }
    public Guid UserId { get; private set; }

    /// <summary>The colour assigned to this player on the board (e.g. "red", "blue").</summary>
    public string Color { get; private set; } = string.Empty;

    /// <summary>Total reinforcement troops the player currently holds and has not yet placed.</summary>
    public int ReinforcementTroops { get; private set; } = 0;

    /// <summary>Whether this player has been eliminated from the game.</summary>
    public bool IsEliminated { get; private set; } = false;

    /// <summary>Turn order position (1-indexed).</summary>
    public int TurnOrder { get; private set; }

    public DateTimeOffset JoinedAt { get; private set; } = DateTimeOffset.UtcNow;

    // Navigation properties
    public Game? Game { get; private set; }
    public User? User { get; private set; }
    public MissionCard? MissionCard { get; private set; }

    private readonly List<BonusCard> _bonusCards = [];
    /// <summary>Bonus cards currently in the player's hand.</summary>
    public IReadOnlyCollection<BonusCard> BonusCards => _bonusCards.AsReadOnly();

    private readonly List<Territory> _territories = [];
    public IReadOnlyCollection<Territory> Territories => _territories.AsReadOnly();

    // EF Core requires a parameterless constructor
    private Player() { }

    internal Player(Guid gameId, Guid userId, string color, int turnOrder)
    {
        GameId = Guard.NotEmpty(gameId, nameof(gameId));
        UserId = Guard.NotEmpty(userId, nameof(userId));
        Color = Guard.NotBlank(color, nameof(color)).ToLowerInvariant();
        TurnOrder = Guard.Positive(turnOrder, nameof(turnOrder));
    }

    internal void AddReinforcements(int troops)
    {
        ReinforcementTroops += Guard.Positive(troops, nameof(troops));
    }

    internal void PlaceTroops(int troops)
    {
        Guard.Positive(troops, nameof(troops));
        if (troops > ReinforcementTroops)
            throw new BusinessRuleException("Cannot place more troops than the player has in reserve.");

        ReinforcementTroops -= troops;
    }

    internal void Eliminate()
    {
        IsEliminated = true;
    }
}
