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
    public GameVictoryMode VictoryMode { get; private set; } = GameVictoryMode.CompleteConquest;
    public bool HasMissions => VictoryMode == GameVictoryMode.Mission;

    /// <summary>
    /// Blizzard game mode: how many territories become impassable when the game starts.
    /// 0 = mode off. Placement never splits the board, see <see cref="Map.BlizzardPlacement"/>.
    /// </summary>
    public int BlizzardCount { get; private set; } = 0;

    public bool HasBlizzards => BlizzardCount > 0;

    /// <summary>How traded bonus card sets are valued. See <see cref="Services.CardTradeRules"/>.</summary>
    public CardTradeMode CardTradeMode { get; private set; } = CardTradeMode.Progressive;

    // Navigation property
    public Game? Game { get; private set; }

    // EF Core requires a parameterless constructor
    private GameSetting() { }

    public GameSetting(Guid gameId, int maxPlayers = DefaultMaxPlayers, int timer = 0, int turnTimeLimit = 0,
        bool hasFogOfWar = false, bool hasMissions = false, int blizzardCount = 0,
        GameVictoryMode? victoryMode = null, CardTradeMode? cardTradeMode = null)
    {
        GameId = Guard.NotEmpty(gameId, nameof(gameId));
        Update(maxPlayers, timer, turnTimeLimit, hasFogOfWar, hasMissions, blizzardCount, victoryMode, cardTradeMode);
    }

    internal void Update(int maxPlayers, int timer, int turnTimeLimit, bool hasFogOfWar, bool hasMissions,
        int blizzardCount = 0, GameVictoryMode? victoryMode = null, CardTradeMode? cardTradeMode = null)
    {
        MaxPlayers = Guard.InRange(maxPlayers, MinPlayers, DefaultMaxPlayers, nameof(maxPlayers));
        Timer = Guard.NotNegative(timer, nameof(timer));
        TurnTimeLimit = Guard.NotNegative(turnTimeLimit, nameof(turnTimeLimit));
        HasFogOfWar = hasFogOfWar;
        VictoryMode = victoryMode ?? (hasMissions ? GameVictoryMode.Mission : GameVictoryMode.CompleteConquest);
        // The upper limit depends on the map, so BlizzardPlacement checks it when the game starts.
        BlizzardCount = Guard.NotNegative(blizzardCount, nameof(blizzardCount));
        CardTradeMode = cardTradeMode ?? CardTradeMode.Progressive;
    }

}

public enum GameVictoryMode
{
    CompleteConquest,
    Mission
}

/// <summary>How traded bonus card sets are valued.</summary>
public enum CardTradeMode
{
    /// <summary>Every set traded in the game is worth more than the last: 4, 6, 8, 10, 12, 15, then +5.</summary>
    Progressive,

    /// <summary>3 infantry = 4, 3 cavalry = 6, 3 artillery = 8, one of each = 10.</summary>
    Fixed
}
