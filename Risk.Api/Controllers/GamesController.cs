using System.Security.Claims;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Risk.Domain.Entities;
using Risk.Infrastructure.Persistence;

namespace Risk.Api.Controllers;

/// <summary>Manages Risk game sessions and their settings.</summary>
[ApiController]
[Route("api/games")]
[Authorize]
public sealed class GamesController : ControllerBase
{
    private readonly AppDbContext _db;

    public GamesController(AppDbContext db) => _db = db;

    private Guid CallerId =>
        Guid.Parse(User.FindFirstValue(ClaimTypes.NameIdentifier)
                   ?? User.FindFirstValue("sub")
                   ?? throw new InvalidOperationException("No subject claim."));

    // -------------------------------------------------------------------------
    // GET /api/games
    // -------------------------------------------------------------------------

    /// <summary>Lists games, optionally filtered by state.</summary>
    /// <param name="state">Optional filter: Lobby | InProgress | Finished</param>
    /// <response code="200">List of games returned.</response>
    [HttpGet]
    [ProducesResponseType<IEnumerable<GameSummaryResponse>>(StatusCodes.Status200OK)]
    public async Task<ActionResult<IEnumerable<GameSummaryResponse>>> GetAll([FromQuery] GameState? state)
    {
        var query = _db.Games.Include(g => g.Settings).AsQueryable();
        if (state.HasValue)
            query = query.Where(g => g.State == state.Value);

        var games = await query
            .OrderByDescending(g => g.CreatedAt)
            .Select(g => GameSummaryResponse.From(g))
            .ToListAsync();

        return Ok(games);
    }

    // -------------------------------------------------------------------------
    // POST /api/games
    // -------------------------------------------------------------------------

    /// <summary>Creates a new game with default settings. The caller becomes host.</summary>
    /// <response code="201">Game created.</response>
    [HttpPost]
    [ProducesResponseType<GameDetailResponse>(StatusCodes.Status201Created)]
    public async Task<ActionResult<GameDetailResponse>> Create([FromBody] CreateGameRequest request)
    {
        var game = new Game(CallerId);

        var settings = new GameSetting(
            game.Id,
            maxPlayers:    request.MaxPlayers ?? 6,
            timer:         request.Timer ?? 0,
            turnTimeLimit: request.TurnTimeLimit ?? 0,
            hasFogOfWar:   request.HasFogOfWar ?? false,
            hasMissions:   request.HasMissions ?? false);

        _db.Games.Add(game);
        _db.GameSettings.Add(settings);
        await _db.SaveChangesAsync();

        // Reload with settings included
        await _db.Entry(game).Reference(g => g.Settings).LoadAsync();
        return CreatedAtAction(nameof(GetById), new { id = game.Id }, GameDetailResponse.From(game));
    }

    // -------------------------------------------------------------------------
    // GET /api/games/{id}
    // -------------------------------------------------------------------------

    /// <summary>Returns full details for a game including settings and player list.</summary>
    /// <response code="200">Game found.</response>
    /// <response code="404">Game not found.</response>
    [HttpGet("{id:guid}")]
    [ProducesResponseType<GameDetailResponse>(StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<ActionResult<GameDetailResponse>> GetById(Guid id)
    {
        var game = await _db.Games
            .Include(g => g.Settings)
            .Include(g => g.Players)
            .FirstOrDefaultAsync(g => g.Id == id);

        return game is null ? NotFound() : Ok(GameDetailResponse.From(game));
    }

    // -------------------------------------------------------------------------
    // PUT /api/games/{id}/start
    // -------------------------------------------------------------------------

    /// <summary>Starts the game (Lobby → InProgress). Only the host may do this.</summary>
    /// <response code="200">Game started.</response>
    /// <response code="403">Caller is not the host.</response>
    /// <response code="404">Game not found.</response>
    /// <response code="409">Game is not in Lobby state.</response>
    [HttpPut("{id:guid}/start")]
    [ProducesResponseType<GameDetailResponse>(StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status403Forbidden)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    [ProducesResponseType(StatusCodes.Status409Conflict)]
    public async Task<ActionResult<GameDetailResponse>> Start(Guid id)
    {
        var game = await _db.Games.Include(g => g.Settings).Include(g => g.Players)
                                   .FirstOrDefaultAsync(g => g.Id == id);
        if (game is null) return NotFound();
        if (game.HostId != CallerId) return Forbid();

        try { game.Start(); }
        catch (InvalidOperationException ex) { return Conflict(ex.Message); }

        await _db.SaveChangesAsync();
        return Ok(GameDetailResponse.From(game));
    }

    // -------------------------------------------------------------------------
    // PUT /api/games/{id}/end
    // -------------------------------------------------------------------------

    /// <summary>Ends the game (InProgress → Finished). Only the host may do this.</summary>
    /// <response code="200">Game ended.</response>
    /// <response code="403">Caller is not the host.</response>
    /// <response code="404">Game not found.</response>
    /// <response code="409">Game is not in InProgress state.</response>
    [HttpPut("{id:guid}/end")]
    [ProducesResponseType<GameDetailResponse>(StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status403Forbidden)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    [ProducesResponseType(StatusCodes.Status409Conflict)]
    public async Task<ActionResult<GameDetailResponse>> End(Guid id)
    {
        var game = await _db.Games.Include(g => g.Settings).Include(g => g.Players)
                                   .FirstOrDefaultAsync(g => g.Id == id);
        if (game is null) return NotFound();
        if (game.HostId != CallerId) return Forbid();

        try { game.End(); }
        catch (InvalidOperationException ex) { return Conflict(ex.Message); }

        await _db.SaveChangesAsync();
        return Ok(GameDetailResponse.From(game));
    }

    // -------------------------------------------------------------------------
    // PUT /api/games/{id}/phase
    // -------------------------------------------------------------------------

    /// <summary>Sets the current game phase (Deployment / Attack / Fortify).</summary>
    /// <response code="200">Phase updated.</response>
    /// <response code="404">Game not found.</response>
    [HttpPut("{id:guid}/phase")]
    [ProducesResponseType<GameDetailResponse>(StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<ActionResult<GameDetailResponse>> SetPhase(Guid id, [FromBody] SetPhaseRequest request)
    {
        var game = await _db.Games.Include(g => g.Settings).Include(g => g.Players)
                                   .FirstOrDefaultAsync(g => g.Id == id);
        if (game is null) return NotFound();

        game.SetPhase(request.Phase);
        await _db.SaveChangesAsync();
        return Ok(GameDetailResponse.From(game));
    }

    // -------------------------------------------------------------------------
    // PUT /api/games/{id}/current-player
    // -------------------------------------------------------------------------

    /// <summary>Sets which player's turn it currently is.</summary>
    /// <response code="200">Current player updated.</response>
    /// <response code="404">Game not found.</response>
    [HttpPut("{id:guid}/current-player")]
    [ProducesResponseType<GameDetailResponse>(StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<ActionResult<GameDetailResponse>> SetCurrentPlayer(Guid id, [FromBody] SetCurrentPlayerRequest request)
    {
        var game = await _db.Games.Include(g => g.Settings).Include(g => g.Players)
                                   .FirstOrDefaultAsync(g => g.Id == id);
        if (game is null) return NotFound();

        game.SetCurrentPlayer(request.PlayerId);
        await _db.SaveChangesAsync();
        return Ok(GameDetailResponse.From(game));
    }

    // -------------------------------------------------------------------------
    // GET /api/games/{id}/settings
    // -------------------------------------------------------------------------

    /// <summary>Returns the settings for a game.</summary>
    /// <response code="200">Settings returned.</response>
    /// <response code="404">Game or settings not found.</response>
    [HttpGet("{id:guid}/settings")]
    [ProducesResponseType<GameSettingResponse>(StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<ActionResult<GameSettingResponse>> GetSettings(Guid id)
    {
        var settings = await _db.GameSettings.FirstOrDefaultAsync(s => s.GameId == id);
        return settings is null ? NotFound() : Ok(GameSettingResponse.From(settings));
    }

    // -------------------------------------------------------------------------
    // PUT /api/games/{id}/settings
    // -------------------------------------------------------------------------

    /// <summary>Updates game settings. Only allowed while in Lobby state.</summary>
    /// <response code="200">Settings updated.</response>
    /// <response code="403">Caller is not the host.</response>
    /// <response code="404">Game not found.</response>
    /// <response code="409">Game has already started.</response>
    [HttpPut("{id:guid}/settings")]
    [ProducesResponseType<GameSettingResponse>(StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status403Forbidden)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    [ProducesResponseType(StatusCodes.Status409Conflict)]
    public async Task<ActionResult<GameSettingResponse>> UpdateSettings(Guid id, [FromBody] UpdateGameSettingRequest request)
    {
        var game = await _db.Games.Include(g => g.Settings).FirstOrDefaultAsync(g => g.Id == id);
        if (game is null) return NotFound();
        if (game.HostId != CallerId) return Forbid();
        if (game.State != GameState.Lobby) return Conflict("Settings can only be changed while the game is in the Lobby.");

        // Replace the settings entity with new values
        var old = game.Settings;
        if (old is not null)
            _db.GameSettings.Remove(old);

        var updated = new GameSetting(
            game.Id,
            maxPlayers:    request.MaxPlayers,
            timer:         request.Timer,
            turnTimeLimit: request.TurnTimeLimit,
            hasFogOfWar:   request.HasFogOfWar,
            hasMissions:   request.HasMissions);

        _db.GameSettings.Add(updated);
        await _db.SaveChangesAsync();

        return Ok(GameSettingResponse.From(updated));
    }
}

// -------------------------------------------------------------------------
// DTOs
// -------------------------------------------------------------------------

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
        new(g.Id, g.HostId, g.State, g.Players.Count, g.Settings?.MaxPlayers ?? 6, g.CreatedAt);
}

/// <summary>Full game details including settings and players.</summary>
/// <param name="Id">Game ID.</param>
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
    Guid Id, Guid HostId, GameState State,
    Guid? CurrentPlayerId, GamePhase CurrentPhase,
    GameSettingResponse? Settings,
    IEnumerable<PlayerSummaryResponse> Players,
    DateTimeOffset CreatedAt, DateTimeOffset? StartedAt, DateTimeOffset? EndedAt)
{
    internal static GameDetailResponse From(Game g) =>
        new(g.Id, g.HostId, g.State, g.CurrentPlayerId, g.CurrentPhase,
            g.Settings is null ? null : GameSettingResponse.From(g.Settings),
            g.Players.Select(PlayerSummaryResponse.From),
            g.CreatedAt, g.StartedAt, g.EndedAt);
}

/// <summary>Game configuration snapshot.</summary>
public sealed record GameSettingResponse(
    Guid Id, Guid GameId, int MaxPlayers, int Timer,
    int TurnTimeLimit, bool HasFogOfWar, bool HasMissions)
{
    internal static GameSettingResponse From(GameSetting s) =>
        new(s.Id, s.GameId, s.MaxPlayers, s.Timer, s.TurnTimeLimit, s.HasFogOfWar, s.HasMissions);
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
public sealed record CreateGameRequest(
    int? MaxPlayers, int? Timer, int? TurnTimeLimit,
    bool? HasFogOfWar, bool? HasMissions);

/// <summary>Payload to fully replace game settings.</summary>
public sealed record UpdateGameSettingRequest(
    int MaxPlayers, int Timer, int TurnTimeLimit,
    bool HasFogOfWar, bool HasMissions);

/// <summary>Payload to change the current game phase.</summary>
/// <param name="Phase">New phase.</param>
public sealed record SetPhaseRequest(GamePhase Phase);

/// <summary>Payload to set the active player.</summary>
/// <param name="PlayerId">ID of the player whose turn it now is.</param>
public sealed record SetCurrentPlayerRequest(Guid PlayerId);
