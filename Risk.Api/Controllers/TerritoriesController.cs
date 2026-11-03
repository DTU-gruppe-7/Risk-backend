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

    /// <summary>Deploys troops from the authenticated player's reserve to their territory.</summary>
    [HttpPut("{id:guid}/deploy")]
    [ProducesResponseType<TerritoryResponse>(StatusCodes.Status200OK)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status400BadRequest)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status403Forbidden)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status404NotFound)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status409Conflict)]
    public async Task<ActionResult<TerritoryResponse>> Deploy(Guid gameId, Guid id,
        [FromBody] DeployTroopsRequest request, CancellationToken ct)
        => Ok(await territories.DeployTroopsAsync(gameId, id, request, ct));
}
