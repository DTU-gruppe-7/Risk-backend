using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Risk.Domain.Entities;
using Risk.Infrastructure.Persistence;

namespace Risk.Api.Controllers;

/// <summary>Read-only access to continents in a game.</summary>
[ApiController]
[Route("api/games/{gameId:guid}/continents")]
[Authorize]
public sealed class ContinentsController : ControllerBase
{
    private readonly AppDbContext _db;

    public ContinentsController(AppDbContext db) => _db = db;

    // -------------------------------------------------------------------------
    // GET /api/games/{gameId}/continents
    // -------------------------------------------------------------------------

    /// <summary>Lists all continents in a game.</summary>
    /// <response code="200">Continents returned.</response>
    /// <response code="404">Game not found.</response>
    [HttpGet]
    [ProducesResponseType<IEnumerable<ContinentSummaryResponse>>(StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<ActionResult<IEnumerable<ContinentSummaryResponse>>> GetAll(Guid gameId)
    {
        if (!await _db.Games.AnyAsync(g => g.Id == gameId)) return NotFound();

        var continents = await _db.Continents
            .Where(c => c.GameId == gameId)
            .OrderBy(c => c.Name)
            .Select(c => ContinentSummaryResponse.From(c))
            .ToListAsync();

        return Ok(continents);
    }

    // -------------------------------------------------------------------------
    // GET /api/games/{gameId}/continents/{id}
    // -------------------------------------------------------------------------

    /// <summary>Returns a continent with its territory IDs.</summary>
    /// <response code="200">Continent returned.</response>
    /// <response code="404">Continent not found.</response>
    [HttpGet("{id:guid}")]
    [ProducesResponseType<ContinentDetailResponse>(StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<ActionResult<ContinentDetailResponse>> GetById(Guid gameId, Guid id)
    {
        var continent = await _db.Continents
            .Include(c => c.Territories)
            .FirstOrDefaultAsync(c => c.Id == id && c.GameId == gameId);

        return continent is null ? NotFound() : Ok(ContinentDetailResponse.From(continent));
    }
}

// -------------------------------------------------------------------------
// DTOs
// -------------------------------------------------------------------------

/// <summary>Lightweight continent for list views.</summary>
/// <param name="Id">Continent ID.</param>
/// <param name="GameId">Owning game.</param>
/// <param name="Name">Continent name.</param>
/// <param name="CaptureBonus">Bonus troops for controlling all territories.</param>
/// <param name="TerritoryCount">Number of territories in this continent.</param>
public sealed record ContinentSummaryResponse(
    Guid Id, Guid GameId, string Name, int CaptureBonus, int TerritoryCount)
{
    internal static ContinentSummaryResponse From(Continent c) =>
        new(c.Id, c.GameId, c.Name, c.CaptureBonus, c.Territories.Count);
}

/// <summary>Full continent detail including territory IDs.</summary>
/// <param name="Id">Continent ID.</param>
/// <param name="GameId">Owning game.</param>
/// <param name="Name">Continent name.</param>
/// <param name="CaptureBonus">Bonus troops for controlling all territories.</param>
/// <param name="TerritoryIds">IDs of territories belonging to this continent.</param>
public sealed record ContinentDetailResponse(
    Guid Id, Guid GameId, string Name, int CaptureBonus, IEnumerable<Guid> TerritoryIds)
{
    internal static ContinentDetailResponse From(Continent c) =>
        new(c.Id, c.GameId, c.Name, c.CaptureBonus, c.Territories.Select(t => t.Id));
}
