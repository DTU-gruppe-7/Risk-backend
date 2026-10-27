using Risk.Domain.Common;

namespace Risk.Domain.Entities;

/// <summary>
/// Configuration options for a single Risk game.
/// Owned by one <see cref="Game"/>, and only changed through it.
/// </summary>
public sealed class GameSetting
{
    public const int DefaultMaxPlayers = 6;
    public const int MinPlayers = 2;

    public Guid Id { get; private set; } = Guid.CreateVersion7();

    public Guid GameId { get; private set; }

    /// <summary>Maximum number of players allowed in the game (2–6 for classic Risk).</summary>
    public int MaxPlayers { get; private set; } = DefaultMaxPlayers;

    /// <summary>Total time limit (in seconds) for the entire game, 0 = no limit.</summary>
    public int Timer { get; private set; } = 0;

    /// <summary>Time limit (in seconds) allowed per turn, 0 = no limit.</summary>
    public int TurnTimeLimit { get; private set; } = 0;

    /// <summary>When true, players can only see territories adjacent to their own.</summary>
    public bool HasFogOfWar { get; private set; } = false;

    /// <summary>When true, each player receives a secret mission card as a win condition.</summary>
    public bool HasMissions { get; private set; } = false;

    /// <summary>
    /// Blizzard game mode: how many territories become impassable when the game starts.
    /// 0 = mode off. Placement never splits the board, see <see cref="Map.BlizzardPlacement"/>.
    /// </summary>
    public int BlizzardCount { get; private set; } = 0;

    public bool HasBlizzards => BlizzardCount > 0;

    // Navigation property
    public Game? Game { get; private set; }

    // EF Core requires a parameterless constructor
    private GameSetting() { }

    public GameSetting(Guid gameId, int maxPlayers = DefaultMaxPlayers, int timer = 0, int turnTimeLimit = 0,
        bool hasFogOfWar = false, bool hasMissions = false, int blizzardCount = 0)
    {
        GameId = Guard.NotEmpty(gameId, nameof(gameId));
        Update(maxPlayers, timer, turnTimeLimit, hasFogOfWar, hasMissions, blizzardCount);
    }

    internal void Update(int maxPlayers, int timer, int turnTimeLimit, bool hasFogOfWar, bool hasMissions,
        int blizzardCount = 0)
    {
        MaxPlayers = Guard.InRange(maxPlayers, MinPlayers, DefaultMaxPlayers, nameof(maxPlayers));
        Timer = Guard.NotNegative(timer, nameof(timer));
        TurnTimeLimit = Guard.NotNegative(turnTimeLimit, nameof(turnTimeLimit));
        HasFogOfWar = hasFogOfWar;
        HasMissions = hasMissions;
        // The upper limit depends on the map, so BlizzardPlacement checks it when the game starts.
        BlizzardCount = Guard.NotNegative(blizzardCount, nameof(blizzardCount));
    }
}
