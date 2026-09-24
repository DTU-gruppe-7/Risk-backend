namespace Risk.Domain.Entities;

/// <summary>
/// Configuration options for a single Risk game.
/// Owned by one <see cref="Game"/>.
/// </summary>
public sealed class GameSetting
{
    public Guid Id { get; private set; } = Guid.CreateVersion7();

    public Guid GameId { get; private set; }

    /// <summary>Maximum number of players allowed in the game (2–6 for classic Risk).</summary>
    public int MaxPlayers { get; private set; } = 6;

    /// <summary>Total time limit (in seconds) for the entire game, 0 = no limit.</summary>
    public int Timer { get; private set; } = 0;

    /// <summary>Time limit (in seconds) allowed per turn, 0 = no limit.</summary>
    public int TurnTimeLimit { get; private set; } = 0;

    /// <summary>When true, players can only see territories adjacent to their own.</summary>
    public bool HasFogOfWar { get; private set; } = false;

    /// <summary>When true, each player receives a secret mission card as a win condition.</summary>
    public bool HasMissions { get; private set; } = false;

    // Navigation property
    public Game? Game { get; private set; }

    // EF Core requires a parameterless constructor
    private GameSetting() { }

    public GameSetting(Guid gameId, int maxPlayers = 6, int timer = 0, int turnTimeLimit = 0,
        bool hasFogOfWar = false, bool hasMissions = false)
    {
        if (gameId == Guid.Empty)
            throw new ArgumentException("GameId must be a valid GUID.", nameof(gameId));
        if (maxPlayers is < 2 or > 6)
            throw new ArgumentOutOfRangeException(nameof(maxPlayers), "MaxPlayers must be between 2 and 6.");
        if (timer < 0)
            throw new ArgumentOutOfRangeException(nameof(timer), "Timer must be non-negative.");
        if (turnTimeLimit < 0)
            throw new ArgumentOutOfRangeException(nameof(turnTimeLimit), "TurnTimeLimit must be non-negative.");

        GameId = gameId;
        MaxPlayers = maxPlayers;
        Timer = timer;
        TurnTimeLimit = turnTimeLimit;
        HasFogOfWar = hasFogOfWar;
        HasMissions = hasMissions;
    }
}
