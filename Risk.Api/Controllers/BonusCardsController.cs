using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Risk.Domain.Entities;
using Risk.Infrastructure.Persistence;

namespace Risk.Api.Controllers;

/// <summary>Manages Risk territory cards (bonus cards) in a game.</summary>
[ApiController]
[Route("api/games/{gameId:guid}/bonus-cards")]
[Authorize]
public sealed class BonusCardsController : ControllerBase
{
    private readonly AppDbContext _db;

    public BonusCardsController(AppDbContext db) => _db = db;

    // -------------------------------------------------------------------------
    // GET /api/games/{gameId}/bonus-cards
    // -------------------------------------------------------------------------

    /// <summary>Lists bonus cards in a game, optionally filtered by player.</summary>
    /// <param name="gameId">Game ID.</param>
    /// <param name="playerId">Optional: only return cards held by this player.</param>
    /// <response code="200">Card list returned.</response>
    /// <response code="404">Game not found.</response>
    [HttpGet]
    [ProducesResponseType<IEnumerable<BonusCardResponse>>(StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<ActionResult<IEnumerable<BonusCardResponse>>> GetAll(Guid gameId, [FromQuery] Guid? playerId)
    {
        if (!await _db.Games.AnyAsync(g => g.Id == gameId)) return NotFound();

        var query = _db.BonusCards.Where(b => b.GameId == gameId);
        if (playerId.HasValue)
            query = query.Where(b => b.PlayerId == playerId.Value);

        var cards = await query.OrderBy(b => b.Type).ToListAsync();
        return Ok(cards.Select(BonusCardResponse.From));
    }

    // -------------------------------------------------------------------------
    // PUT /api/games/{gameId}/bonus-cards/{id}/assign
    // -------------------------------------------------------------------------

    /// <summary>Assigns a bonus card from the draw pile to a player.</summary>
    /// <response code="200">Card assigned.</response>
    /// <response code="400">Card already used or domain rule violated.</response>
    /// <response code="404">Card not found.</response>
    [HttpPut("{id:guid}/assign")]
    [ProducesResponseType<BonusCardResponse>(StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<ActionResult<BonusCardResponse>> Assign(Guid gameId, Guid id, [FromBody] AssignCardRequest request)
    {
        var card = await _db.BonusCards
            .FirstOrDefaultAsync(b => b.Id == id && b.GameId == gameId);

        if (card is null) return NotFound();

        try { card.AssignToPlayer(request.PlayerId); }
        catch (Exception ex) when (ex is ArgumentException or InvalidOperationException)
        {
            return BadRequest(ex.Message);
        }

        await _db.SaveChangesAsync();
        return Ok(BonusCardResponse.From(card));
    }

    // -------------------------------------------------------------------------
    // PUT /api/games/{gameId}/bonus-cards/{id}/use
    // -------------------------------------------------------------------------

    /// <summary>Marks a bonus card as used (traded in for reinforcements).</summary>
    /// <response code="200">Card marked as used.</response>
    /// <response code="400">Card already used.</response>
    /// <response code="404">Card not found.</response>
    [HttpPut("{id:guid}/use")]
    [ProducesResponseType<BonusCardResponse>(StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<ActionResult<BonusCardResponse>> Use(Guid gameId, Guid id)
    {
        var card = await _db.BonusCards
            .FirstOrDefaultAsync(b => b.Id == id && b.GameId == gameId);

        if (card is null) return NotFound();

        try { card.MarkAsUsed(); }
        catch (InvalidOperationException ex) { return BadRequest(ex.Message); }

        await _db.SaveChangesAsync();
        return Ok(BonusCardResponse.From(card));
    }
}

// -------------------------------------------------------------------------
// DTOs
// -------------------------------------------------------------------------

/// <summary>A Risk territory / bonus card.</summary>
/// <param name="Id">Card ID.</param>
/// <param name="GameId">Owning game.</param>
/// <param name="PlayerId">Holder player ID, or null if in draw pile.</param>
/// <param name="Type">Card symbol type.</param>
/// <param name="TerritoryIdentifier">Depicted territory slug, or null for Wild cards.</param>
/// <param name="Used">Whether the card has been traded in.</param>
public sealed record BonusCardResponse(
    Guid Id, Guid GameId, Guid? PlayerId,
    BonusCardType Type, string? TerritoryIdentifier, bool Used)
{
    internal static BonusCardResponse From(BonusCard b) =>
        new(b.Id, b.GameId, b.PlayerId, b.Type, b.TerritoryIdentifier, b.Used);
}

/// <summary>Payload to assign a card to a player.</summary>
/// <param name="PlayerId">Player receiving the card.</param>
public sealed record AssignCardRequest(Guid PlayerId);
