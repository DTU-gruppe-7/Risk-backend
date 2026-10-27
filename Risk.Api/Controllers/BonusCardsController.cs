using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Risk.Application.Cards;

namespace Risk.Api.Controllers;

/// <summary>Manages Risk territory cards (bonus cards) in a game.</summary>
[ApiController]
[Route("api/games/{gameId:guid}/bonus-cards")]
[Authorize]
public sealed class BonusCardsController(BonusCardService cards) : ControllerBase
{
    /// <summary>Lists bonus cards in a game, optionally filtered by player.</summary>
    /// <param name="gameId">Game ID.</param>
    /// <param name="playerId">Optional: only return cards held by this player.</param>
    /// <param name="ct">Cancellation token.</param>
    /// <response code="200">Card list returned.</response>
    /// <response code="404">Game not found.</response>
    [HttpGet]
    [ProducesResponseType<IEnumerable<BonusCardResponse>>(StatusCodes.Status200OK)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status404NotFound)]
    public async Task<ActionResult<IEnumerable<BonusCardResponse>>> GetAll(Guid gameId, [FromQuery] Guid? playerId, CancellationToken ct)
        => Ok(await cards.GetAllAsync(gameId, playerId, ct));

    /// <summary>Assigns a bonus card from the draw pile to a player.</summary>
    /// <response code="200">Card assigned.</response>
    /// <response code="400">The player is not part of this game.</response>
    /// <response code="403">Caller is not part of the game.</response>
    /// <response code="404">Card not found.</response>
    /// <response code="409">Card already used.</response>
    [HttpPut("{id:guid}/assign")]
    [ProducesResponseType<BonusCardResponse>(StatusCodes.Status200OK)]
    [ProducesResponseType<ValidationProblemDetails>(StatusCodes.Status400BadRequest)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status403Forbidden)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status404NotFound)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status409Conflict)]
    public async Task<ActionResult<BonusCardResponse>> Assign(Guid gameId, Guid id, [FromBody] AssignCardRequest request, CancellationToken ct)
        => Ok(await cards.AssignAsync(gameId, id, request, ct));

    /// <summary>Marks a bonus card as used (traded in for reinforcements).</summary>
    /// <response code="200">Card marked as used.</response>
    /// <response code="403">Caller is not part of the game.</response>
    /// <response code="404">Card not found.</response>
    /// <response code="409">Card already used.</response>
    [HttpPut("{id:guid}/use")]
    [ProducesResponseType<BonusCardResponse>(StatusCodes.Status200OK)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status403Forbidden)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status404NotFound)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status409Conflict)]
    public async Task<ActionResult<BonusCardResponse>> Use(Guid gameId, Guid id, CancellationToken ct)
        => Ok(await cards.UseAsync(gameId, id, ct));
}
