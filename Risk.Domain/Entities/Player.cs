namespace Risk.Domain.Entities;

/// <summary>
/// Represents a user's participation in a single Risk game.
/// Bridges <see cref="User"/> and <see cref="Game"/>.
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
    public BonusCard? BonusCard { get; private set; }
    public MissionCard? MissionCard { get; private set; }

    private readonly List<Territory> _territories = [];
    public IReadOnlyCollection<Territory> Territories => _territories.AsReadOnly();

    // EF Core requires a parameterless constructor
    private Player() { }

    public Player(Guid gameId, Guid userId, string color, int turnOrder)
    {
        if (gameId == Guid.Empty)
            throw new ArgumentException("GameId must be a valid GUID.", nameof(gameId));
        if (userId == Guid.Empty)
            throw new ArgumentException("UserId must be a valid GUID.", nameof(userId));
        if (string.IsNullOrWhiteSpace(color))
            throw new ArgumentException("Color must not be empty.", nameof(color));
        if (turnOrder < 1)
            throw new ArgumentOutOfRangeException(nameof(turnOrder), "TurnOrder must be at least 1.");

        GameId = gameId;
        UserId = userId;
        Color = color.Trim().ToLowerInvariant();
        TurnOrder = turnOrder;
    }

    public void AddReinforcements(int troops)
    {
        if (troops <= 0)
            throw new ArgumentOutOfRangeException(nameof(troops), "Troops must be positive.");

        ReinforcementTroops += troops;
    }

    public void PlaceTroops(int troops)
    {
        if (troops <= 0 || troops > ReinforcementTroops)
            throw new InvalidOperationException("Invalid troop placement amount.");

        ReinforcementTroops -= troops;
    }

    public void Eliminate()
    {
        IsEliminated = true;
    }
}
