using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Risk.Application.Territories;

namespace Risk.Api.Controllers;

/// <summary>Manages territories in a game (board state, claiming, troop movements).</summary>
[ApiController]
[Route("api/games/{gameId:guid}/territories")]
[Authorize]
public sealed class TerritoriesController(TerritoryService territories) : ControllerBase
{
    /// <summary>
    /// Lists every territory's state in a game (blizzards included), optionally filtered by owner.
    /// Empty until the game has started. Continents and neighbours come from <c>GET /api/map</c>.
    /// </summary>
    /// <param name="gameId">Game ID.</param>
    /// <param name="ownerId">Optional: filter to territories owned by this player ID.</param>
    /// <param name="ct">Cancellation token.</param>
    /// <response code="200">Territory list returned.</response>
    /// <response code="404">Game not found.</response>
    [HttpGet]
    [ProducesResponseType<IEnumerable<TerritoryResponse>>(StatusCodes.Status200OK)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status404NotFound)]
    public async Task<ActionResult<IEnumerable<TerritoryResponse>>> GetAll(Guid gameId, [FromQuery] Guid? ownerId, CancellationToken ct)
        => Ok(await territories.GetAllAsync(gameId, ownerId, ct));

    /// <summary>Returns a single territory's state.</summary>
    /// <response code="200">Territory returned.</response>
    /// <response code="404">Territory not found.</response>
    [HttpGet("{id:guid}")]
    [ProducesResponseType<TerritoryResponse>(StatusCodes.Status200OK)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status404NotFound)]
    public async Task<ActionResult<TerritoryResponse>> GetById(Guid gameId, Guid id, CancellationToken ct)
        => Ok(await territories.GetByIdAsync(gameId, id, ct));

    /// <summary>Claims an unoccupied territory for a player.</summary>
    /// <response code="200">Territory claimed.</response>
    /// <response code="400">Invalid troop count, or the player is not in this game.</response>
    /// <response code="403">Caller is not part of the game.</response>
    /// <response code="404">Territory not found.</response>
    /// <response code="409">Territory is already occupied, or a blizzard.</response>
    [HttpPut("{id:guid}/claim")]
    [ProducesResponseType<TerritoryResponse>(StatusCodes.Status200OK)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status400BadRequest)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status403Forbidden)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status404NotFound)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status409Conflict)]
    public async Task<ActionResult<TerritoryResponse>> Claim(Guid gameId, Guid id, [FromBody] ClaimTerritoryRequest request, CancellationToken ct)
        => Ok(await territories.ClaimAsync(gameId, id, request, ct));

    /// <summary>Adds or removes troops from a territory.</summary>
    /// <response code="200">Troops updated.</response>
    /// <response code="403">Caller is not part of the game.</response>
    /// <response code="404">Territory not found.</response>
    /// <response code="409">Removing more troops than present, or adding to a blizzard.</response>
    [HttpPut("{id:guid}/troops")]
    [ProducesResponseType<TerritoryResponse>(StatusCodes.Status200OK)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status403Forbidden)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status404NotFound)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status409Conflict)]
    public async Task<ActionResult<TerritoryResponse>> UpdateTroops(Guid gameId, Guid id, [FromBody] UpdateTroopsRequest request, CancellationToken ct)
        => Ok(await territories.UpdateTroopsAsync(gameId, id, request, ct));
}
