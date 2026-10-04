using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Risk.Domain.Entities;
using Risk.Infrastructure.Persistence;

namespace Risk.Api.Controllers;

/// <summary>Manages secret mission cards in a game.</summary>
[ApiController]
[Route("api/games/{gameId:guid}/mission-cards")]
[Authorize]
public sealed class MissionCardsController : ControllerBase
{
    private readonly AppDbContext _db;

    public MissionCardsController(AppDbContext db) => _db = db;

    // -------------------------------------------------------------------------
    // GET /api/games/{gameId}/mission-cards
    // -------------------------------------------------------------------------

    /// <summary>
    /// Lists mission cards in a game. When <paramref name="playerId"/> is provided
    /// only that player's card is returned (used for the player to read their own mission).
    /// </summary>
    /// <param name="gameId">Game ID.</param>
    /// <param name="playerId">Optional: filter to a specific player's mission.</param>
    /// <response code="200">Mission cards returned.</response>
    /// <response code="404">Game not found.</response>
    [HttpGet]
    [ProducesResponseType<IEnumerable<MissionCardResponse>>(StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<ActionResult<IEnumerable<MissionCardResponse>>> GetAll(Guid gameId, [FromQuery] Guid? playerId)
    {
        if (!await _db.Games.AnyAsync(g => g.Id == gameId)) return NotFound();

        var query = _db.MissionCards.Where(m => m.GameId == gameId);
        if (playerId.HasValue)
            query = query.Where(m => m.PlayerId == playerId.Value);

        var cards = await query.OrderBy(m => m.Title).ToListAsync();
        return Ok(cards.Select(MissionCardResponse.From));
    }

    // -------------------------------------------------------------------------
    // PUT /api/games/{gameId}/mission-cards/{id}/assign
    // -------------------------------------------------------------------------

    /// <summary>Assigns a mission card to a player.</summary>
    /// <response code="200">Mission assigned.</response>
    /// <response code="400">Mission already assigned or domain rule violated.</response>
    /// <response code="404">Mission card not found.</response>
    [HttpPut("{id:guid}/assign")]
    [ProducesResponseType<MissionCardResponse>(StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<ActionResult<MissionCardResponse>> Assign(Guid gameId, Guid id, [FromBody] AssignMissionRequest request)
    {
        var card = await _db.MissionCards
            .FirstOrDefaultAsync(m => m.Id == id && m.GameId == gameId);

        if (card is null) return NotFound();

        try { card.AssignToPlayer(request.PlayerId); }
        catch (Exception ex) when (ex is ArgumentException or InvalidOperationException)
        {
            return BadRequest(ex.Message);
        }

        await _db.SaveChangesAsync();
        return Ok(MissionCardResponse.From(card));
    }

    // -------------------------------------------------------------------------
    // PUT /api/games/{gameId}/mission-cards/{id}/complete
    // -------------------------------------------------------------------------

    /// <summary>Marks a mission as completed — the assigned player wins.</summary>
    /// <response code="200">Mission completed.</response>
    /// <response code="400">Mission not assigned or already completed.</response>
    /// <response code="404">Mission card not found.</response>
    [HttpPut("{id:guid}/complete")]
    [ProducesResponseType<MissionCardResponse>(StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<ActionResult<MissionCardResponse>> Complete(Guid gameId, Guid id)
    {
        var card = await _db.MissionCards
            .FirstOrDefaultAsync(m => m.Id == id && m.GameId == gameId);

        if (card is null) return NotFound();

        try { card.Complete(); }
        catch (InvalidOperationException ex) { return BadRequest(ex.Message); }

        await _db.SaveChangesAsync();
        return Ok(MissionCardResponse.From(card));
    }
}

// -------------------------------------------------------------------------
// DTOs
// -------------------------------------------------------------------------

/// <summary>A mission card.</summary>
/// <param name="Id">Card ID.</param>
/// <param name="GameId">Owning game.</param>
/// <param name="PlayerId">Assigned player ID, or null if unassigned.</param>
/// <param name="Title">Short mission title.</param>
/// <param name="Description">Full victory condition.</param>
/// <param name="IsCompleted">Whether the mission has been completed.</param>
public sealed record MissionCardResponse(
    Guid Id, Guid GameId, Guid? PlayerId,
    string Title, string Description, bool IsCompleted)
{
    internal static MissionCardResponse From(MissionCard m) =>
        new(m.Id, m.GameId, m.PlayerId, m.Title, m.Description, m.IsCompleted);
}

/// <summary>Payload to assign a mission card to a player.</summary>
/// <param name="PlayerId">Player who receives this mission.</param>
public sealed record AssignMissionRequest(Guid PlayerId);
