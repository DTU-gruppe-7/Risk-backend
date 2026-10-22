using Risk.Domain.Entities;

namespace Risk.Application.Games;

/// <summary>Lightweight game summary for list views.</summary>
/// <param name="Id">Game ID.</param>
/// <param name="HostId">Domain user ID of the host.</param>
/// <param name="State">Current game state.</param>
/// <param name="PlayerCount">Number of players currently in the game.</param>
/// <param name="MaxPlayers">Maximum players allowed (from settings).</param>
/// <param name="CreatedAt">When the game was created.</param>
public sealed record GameSummaryResponse(
    Guid Id, Guid HostId, GameState State,
    int PlayerCount, int MaxPlayers, DateTimeOffset CreatedAt)
{
    internal static GameSummaryResponse From(Game g) =>
        new(g.Id, g.HostId, g.State, g.Players.Count, g.MaxPlayers, g.CreatedAt);
}

/// <summary>Full game details including settings and players.</summary>
/// <param name="Id">Game ID.</param>
/// <param name="Version">Increases on every change to the game. Clients can ignore updates older than what they have.</param>
/// <param name="HostId">Domain user ID of the host.</param>
/// <param name="State">Current game state.</param>
/// <param name="CurrentPlayerId">Player whose turn it is (null if not started).</param>
/// <param name="CurrentPhase">Current turn phase.</param>
/// <param name="Settings">Game settings snapshot.</param>
/// <param name="Players">Participating players.</param>
/// <param name="CreatedAt">When the game was created.</param>
/// <param name="StartedAt">When the game started (null if not yet started).</param>
/// <param name="EndedAt">When the game ended (null if not finished).</param>
public sealed record GameDetailResponse(
    Guid Id, long Version, Guid HostId, GameState State,
    Guid? CurrentPlayerId, GamePhase CurrentPhase,
    GameSettingResponse? Settings,
    IEnumerable<PlayerSummaryResponse> Players,
    DateTimeOffset CreatedAt, DateTimeOffset? StartedAt, DateTimeOffset? EndedAt)
{
    internal static GameDetailResponse From(Game g) =>
        new(g.Id, g.Version, g.HostId, g.State, g.CurrentPlayerId, g.CurrentPhase,
            g.Settings is null ? null : GameSettingResponse.From(g.Settings),
            g.Players.OrderBy(p => p.TurnOrder).Select(PlayerSummaryResponse.From).ToList(),
            g.CreatedAt, g.StartedAt, g.EndedAt);
}

/// <summary>Game configuration snapshot.</summary>
/// <param name="BlizzardCount">Blizzard mode: territories made impassable at start. 0 = off.</param>
public sealed record GameSettingResponse(
    Guid Id, Guid GameId, int MaxPlayers, int Timer,
    int TurnTimeLimit, bool HasFogOfWar, bool HasMissions, int BlizzardCount)
{
    internal static GameSettingResponse From(GameSetting s) =>
        new(s.Id, s.GameId, s.MaxPlayers, s.Timer, s.TurnTimeLimit, s.HasFogOfWar, s.HasMissions, s.BlizzardCount);
}

/// <summary>Lightweight player summary embedded in game responses.</summary>
public sealed record PlayerSummaryResponse(
    Guid Id, Guid UserId, string Color, int TurnOrder, bool IsEliminated)
{
    internal static PlayerSummaryResponse From(Player p) =>
        new(p.Id, p.UserId, p.Color, p.TurnOrder, p.IsEliminated);
}

/// <summary>Payload to create a new game. All fields are optional; defaults apply.</summary>
/// <param name="MaxPlayers">2–6. Default: 6.</param>
/// <param name="Timer">Total game time limit in seconds. 0 = unlimited.</param>
/// <param name="TurnTimeLimit">Per-turn time limit in seconds. 0 = unlimited.</param>
/// <param name="HasFogOfWar">Enable fog-of-war. Default: false.</param>
/// <param name="HasMissions">Enable secret mission cards. Default: false.</param>
/// <param name="BlizzardCount">Blizzard mode: territories made impassable at start. Default: 0 (off).</param>
public sealed record CreateGameRequest(
    int? MaxPlayers, int? Timer, int? TurnTimeLimit,
    bool? HasFogOfWar, bool? HasMissions, int? BlizzardCount = null);

/// <summary>Payload to fully replace game settings.</summary>
/// <param name="BlizzardCount">Blizzard mode: territories made impassable at start. 0 = off.</param>
public sealed record UpdateGameSettingRequest(
    int MaxPlayers, int Timer, int TurnTimeLimit,
    bool HasFogOfWar, bool HasMissions, int BlizzardCount = 0);

/// <summary>Payload to change the current game phase.</summary>
/// <param name="Phase">New phase.</param>
public sealed record SetPhaseRequest(GamePhase Phase);

/// <summary>Payload to set the active player.</summary>
/// <param name="PlayerId">ID of the player whose turn it now is.</param>
public sealed record SetCurrentPlayerRequest(Guid PlayerId);
