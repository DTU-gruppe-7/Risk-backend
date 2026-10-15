using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Risk.Domain.Entities;
using Risk.Infrastructure.Persistence;

namespace Risk.Api.Controllers;

/// <summary>Manages territories in a game (board state, claiming, troop movements).</summary>
[ApiController]
[Route("api/games/{gameId:guid}/territories")]
[Authorize]
public sealed class TerritoriesController : ControllerBase
{
    private readonly AppDbContext _db;

    public TerritoriesController(AppDbContext db) => _db = db;

    // -------------------------------------------------------------------------
    // GET /api/games/{gameId}/territories
    // -------------------------------------------------------------------------

    /// <summary>Lists all active territories in a game, optionally filtered by owner.</summary>
    /// <param name="gameId">Game ID.</param>
    /// <param name="ownerId">Optional: filter to territories owned by this player ID.</param>
    /// <response code="200">Territory list returned.</response>
    /// <response code="404">Game not found.</response>
    [HttpGet]
    [ProducesResponseType<IEnumerable<TerritoryResponse>>(StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<ActionResult<IEnumerable<TerritoryResponse>>> GetAll(Guid gameId, [FromQuery] Guid? ownerId)
    {
        if (!await _db.Games.AnyAsync(g => g.Id == gameId)) return NotFound();

        var query = _db.Territories
            .Include(t => t.Continent)
            .Where(t => t.Continent!.GameId == gameId && t.Active);

        if (ownerId.HasValue)
            query = query.Where(t => t.OwnerId == ownerId.Value);

        var territories = await query.OrderBy(t => t.Identifier).ToListAsync();
        return Ok(territories.Select(TerritoryResponse.From));
    }

    // -------------------------------------------------------------------------
    // GET /api/games/{gameId}/territories/{id}
    // -------------------------------------------------------------------------

    /// <summary>Returns a single territory including its neighbouring territory IDs.</summary>
    /// <response code="200">Territory returned.</response>
    /// <response code="404">Territory not found.</response>
    [HttpGet("{id:guid}")]
    [ProducesResponseType<TerritoryDetailResponse>(StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<ActionResult<TerritoryDetailResponse>> GetById(Guid gameId, Guid id)
    {
        var territory = await _db.Territories
            .Include(t => t.Continent)
            .Include(t => t.Neighbors)
            .FirstOrDefaultAsync(t => t.Id == id && t.Continent!.GameId == gameId);

        return territory is null ? NotFound() : Ok(TerritoryDetailResponse.From(territory));
    }

    // -------------------------------------------------------------------------
    // PUT /api/games/{gameId}/territories/{id}/claim
    // -------------------------------------------------------------------------

    /// <summary>Claims an unoccupied territory for a player.</summary>
    /// <response code="200">Territory claimed.</response>
    /// <response code="400">Domain rule violation (e.g. already occupied).</response>
    /// <response code="404">Territory not found.</response>
    [HttpPut("{id:guid}/claim")]
    [ProducesResponseType<TerritoryResponse>(StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<ActionResult<TerritoryResponse>> Claim(Guid gameId, Guid id, [FromBody] ClaimTerritoryRequest request)
    {
        var territory = await _db.Territories
            .Include(t => t.Continent)
            .FirstOrDefaultAsync(t => t.Id == id && t.Continent!.GameId == gameId);

        if (territory is null) return NotFound();

        try { territory.Claim(request.PlayerId, request.InitialTroops); }
        catch (Exception ex) when (ex is ArgumentException or ArgumentOutOfRangeException)
        {
            return BadRequest(ex.Message);
        }

        await _db.SaveChangesAsync();
        return Ok(TerritoryResponse.From(territory));
    }

    // -------------------------------------------------------------------------
    // PUT /api/games/{gameId}/territories/{id}/troops
    // -------------------------------------------------------------------------

    /// <summary>Adds or removes troops from a territory.</summary>
    /// <response code="200">Troops updated.</response>
    /// <response code="400">Domain rule violation (e.g. removing more troops than present).</response>
    /// <response code="404">Territory not found.</response>
    [HttpPut("{id:guid}/troops")]
    [ProducesResponseType<TerritoryResponse>(StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<ActionResult<TerritoryResponse>> UpdateTroops(Guid gameId, Guid id, [FromBody] UpdateTroopsRequest request)
    {
        var territory = await _db.Territories
            .Include(t => t.Continent)
            .FirstOrDefaultAsync(t => t.Id == id && t.Continent!.GameId == gameId);

        if (territory is null) return NotFound();

        try
        {
            if (request.Delta > 0) territory.AddTroops(request.Delta);
            else if (request.Delta < 0) territory.RemoveTroops(-request.Delta);
        }
        catch (Exception ex) when (ex is ArgumentOutOfRangeException or InvalidOperationException)
        {
            return BadRequest(ex.Message);
        }

        await _db.SaveChangesAsync();
        return Ok(TerritoryResponse.From(territory));
    }
}

// -------------------------------------------------------------------------
// DTOs
// -------------------------------------------------------------------------

/// <summary>Territory state without neighbours.</summary>
/// <param name="Id">Territory ID.</param>
/// <param name="ContinentId">Owning continent.</param>
/// <param name="Identifier">Stable slug (e.g. "north-africa").</param>
/// <param name="OwnerId">Player ID of current owner, or null if unoccupied.</param>
/// <param name="TroopCount">Troops currently stationed here.</param>
/// <param name="Active">Whether the territory is active in this game.</param>
public sealed record TerritoryResponse(
    Guid Id, Guid ContinentId, string Identifier,
    Guid? OwnerId, int TroopCount, bool Active)
{
    internal static TerritoryResponse From(Territory t) =>
        new(t.Id, t.ContinentId, t.Identifier, t.OwnerId, t.TroopCount, t.Active);
}

/// <summary>Territory detail including neighbouring territory IDs.</summary>
/// <param name="Id">Territory ID.</param>
/// <param name="ContinentId">Owning continent.</param>
/// <param name="Identifier">Stable slug.</param>
/// <param name="OwnerId">Current owner player ID, or null.</param>
/// <param name="TroopCount">Stationed troops.</param>
/// <param name="Active">Active in this game.</param>
/// <param name="NeighborIds">IDs of adjacent territories.</param>
public sealed record TerritoryDetailResponse(
    Guid Id, Guid ContinentId, string Identifier,
    Guid? OwnerId, int TroopCount, bool Active,
    IEnumerable<Guid> NeighborIds)
{
    internal static TerritoryDetailResponse From(Territory t) =>
        new(t.Id, t.ContinentId, t.Identifier, t.OwnerId, t.TroopCount, t.Active,
            t.Neighbors.Select(n => n.Id));
}

/// <summary>Payload to claim a territory for a player.</summary>
/// <param name="PlayerId">Player claiming the territory.</param>
/// <param name="InitialTroops">Troops to place on claim (minimum 1).</param>
public sealed record ClaimTerritoryRequest(Guid PlayerId, int InitialTroops = 1);

/// <summary>Payload to add or remove troops from a territory.</summary>
/// <param name="Delta">Positive to add troops, negative to remove.</param>
public sealed record UpdateTroopsRequest(int Delta);
