using System.Security.Claims;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Risk.Domain.Entities;
using Risk.Infrastructure.Persistence;

namespace Risk.Api.Controllers;

/// <summary>Manages players within a game (joining, leaving, game actions).</summary>
[ApiController]
[Route("api/games/{gameId:guid}/players")]
[Authorize]
public sealed class PlayersController : ControllerBase
{
    private readonly AppDbContext _db;

    public PlayersController(AppDbContext db) => _db = db;

    private Guid CallerId =>
        Guid.Parse(User.FindFirstValue(ClaimTypes.NameIdentifier)
                   ?? User.FindFirstValue("sub")
                   ?? throw new InvalidOperationException("No subject claim."));

    // -------------------------------------------------------------------------
    // GET /api/games/{gameId}/players
    // -------------------------------------------------------------------------

    /// <summary>Lists all players in a game.</summary>
    /// <response code="200">Player list returned.</response>
    /// <response code="404">Game not found.</response>
    [HttpGet]
    [ProducesResponseType<IEnumerable<PlayerDetailResponse>>(StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<ActionResult<IEnumerable<PlayerDetailResponse>>> GetAll(Guid gameId)
    {
        if (!await _db.Games.AnyAsync(g => g.Id == gameId)) return NotFound();

        var players = await _db.Players
            .Where(p => p.GameId == gameId)
            .OrderBy(p => p.TurnOrder)
            .ToListAsync();

        return Ok(players.Select(PlayerDetailResponse.From));
    }

    // -------------------------------------------------------------------------
    // POST /api/games/{gameId}/players  (join)
    // -------------------------------------------------------------------------

    /// <summary>Joins the game as the authenticated user. Only allowed in Lobby state.</summary>
    /// <response code="201">Player joined.</response>
    /// <response code="400">Already in game or game is full.</response>
    /// <response code="404">Game not found.</response>
    /// <response code="409">Game is not in Lobby state.</response>
    [HttpPost]
    [ProducesResponseType<PlayerDetailResponse>(StatusCodes.Status201Created)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    [ProducesResponseType(StatusCodes.Status409Conflict)]
    public async Task<ActionResult<PlayerDetailResponse>> Join(Guid gameId, [FromBody] JoinGameRequest request)
    {
        var game = await _db.Games
            .Include(g => g.Settings)
            .Include(g => g.Players)
            .FirstOrDefaultAsync(g => g.Id == gameId);

        if (game is null) return NotFound();
        if (game.State != GameState.Lobby) return Conflict("The game has already started.");
        if (game.Players.Any(p => p.UserId == CallerId))
            return BadRequest("You are already in this game.");
        if (game.Players.Count >= (game.Settings?.MaxPlayers ?? 6))
            return BadRequest("The game is full.");

        var turnOrder = game.Players.Count + 1;
        var player = new Player(gameId, CallerId, request.Color, turnOrder);
        _db.Players.Add(player);
        await _db.SaveChangesAsync();

        return CreatedAtAction(nameof(GetById),
            new { gameId, id = player.Id },
            PlayerDetailResponse.From(player));
    }

    // -------------------------------------------------------------------------
    // GET /api/games/{gameId}/players/{id}
    // -------------------------------------------------------------------------

    /// <summary>Returns a single player by ID.</summary>
    /// <response code="200">Player returned.</response>
    /// <response code="404">Player or game not found.</response>
    [HttpGet("{id:guid}")]
    [ProducesResponseType<PlayerDetailResponse>(StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<ActionResult<PlayerDetailResponse>> GetById(Guid gameId, Guid id)
    {
        var player = await _db.Players
            .FirstOrDefaultAsync(p => p.Id == id && p.GameId == gameId);

        return player is null ? NotFound() : Ok(PlayerDetailResponse.From(player));
    }

    // -------------------------------------------------------------------------
    // DELETE /api/games/{gameId}/players/{id}  (leave)
    // -------------------------------------------------------------------------

    /// <summary>Removes the player from the game. Only allowed in Lobby state and by the player themselves.</summary>
    /// <response code="204">Player removed.</response>
    /// <response code="403">Caller is not this player.</response>
    /// <response code="404">Player not found.</response>
    /// <response code="409">Game has already started.</response>
    [HttpDelete("{id:guid}")]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    [ProducesResponseType(StatusCodes.Status403Forbidden)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    [ProducesResponseType(StatusCodes.Status409Conflict)]
    public async Task<IActionResult> Leave(Guid gameId, Guid id)
    {
        var player = await _db.Players
            .FirstOrDefaultAsync(p => p.Id == id && p.GameId == gameId);

        if (player is null) return NotFound();
        if (player.UserId != CallerId) return Forbid();

        var game = await _db.Games.FindAsync(gameId);
        if (game?.State != GameState.Lobby) return Conflict("Cannot leave a game that has already started.");

        _db.Players.Remove(player);
        await _db.SaveChangesAsync();
        return NoContent();
    }

    // -------------------------------------------------------------------------
    // PUT /api/games/{gameId}/players/{id}/eliminate
    // -------------------------------------------------------------------------

    /// <summary>Marks a player as eliminated.</summary>
    /// <response code="200">Player eliminated.</response>
    /// <response code="404">Player not found.</response>
    [HttpPut("{id:guid}/eliminate")]
    [ProducesResponseType<PlayerDetailResponse>(StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<ActionResult<PlayerDetailResponse>> Eliminate(Guid gameId, Guid id)
    {
        var player = await _db.Players
            .FirstOrDefaultAsync(p => p.Id == id && p.GameId == gameId);

        if (player is null) return NotFound();
        player.Eliminate();
        await _db.SaveChangesAsync();
        return Ok(PlayerDetailResponse.From(player));
    }

    // -------------------------------------------------------------------------
    // PUT /api/games/{gameId}/players/{id}/reinforce
    // -------------------------------------------------------------------------

    /// <summary>Adds reinforcement troops to a player's pool.</summary>
    /// <response code="200">Troops added.</response>
    /// <response code="404">Player not found.</response>
    [HttpPut("{id:guid}/reinforce")]
    [ProducesResponseType<PlayerDetailResponse>(StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<ActionResult<PlayerDetailResponse>> Reinforce(Guid gameId, Guid id, [FromBody] ReinforceRequest request)
    {
        var player = await _db.Players
            .FirstOrDefaultAsync(p => p.Id == id && p.GameId == gameId);

        if (player is null) return NotFound();

        try { player.AddReinforcements(request.Troops); }
        catch (ArgumentOutOfRangeException ex) { return BadRequest(ex.Message); }

        await _db.SaveChangesAsync();
        return Ok(PlayerDetailResponse.From(player));
    }
}

// -------------------------------------------------------------------------
// DTOs
// -------------------------------------------------------------------------

/// <summary>Full player details.</summary>
/// <param name="Id">Player ID.</param>
/// <param name="GameId">The game this player belongs to.</param>
/// <param name="UserId">Domain user ID.</param>
/// <param name="Color">Board colour.</param>
/// <param name="ReinforcementTroops">Troops available to place.</param>
/// <param name="IsEliminated">Whether the player has been eliminated.</param>
/// <param name="TurnOrder">1-indexed turn order.</param>
/// <param name="JoinedAt">When the player joined.</param>
public sealed record PlayerDetailResponse(
    Guid Id, Guid GameId, Guid UserId, string Color,
    int ReinforcementTroops, bool IsEliminated, int TurnOrder, DateTimeOffset JoinedAt)
{
    internal static PlayerDetailResponse From(Player p) =>
        new(p.Id, p.GameId, p.UserId, p.Color,
            p.ReinforcementTroops, p.IsEliminated, p.TurnOrder, p.JoinedAt);
}

/// <summary>Payload to join a game.</summary>
/// <param name="Color">Requested board colour (e.g. "red", "blue").</param>
public sealed record JoinGameRequest(string Color);

/// <summary>Payload to add reinforcement troops to a player.</summary>
/// <param name="Troops">Number of troops to add (must be positive).</param>
public sealed record ReinforceRequest(int Troops);
