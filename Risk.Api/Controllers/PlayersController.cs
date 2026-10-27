using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Risk.Application.Players;

namespace Risk.Api.Controllers;

/// <summary>Manages players within a game (joining, leaving, game actions).</summary>
[ApiController]
[Route("api/games/{gameId:guid}/players")]
[Authorize]
public sealed class PlayersController(PlayerService players) : ControllerBase
{
    /// <summary>Lists all players in a game.</summary>
    /// <response code="200">Player list returned.</response>
    /// <response code="404">Game not found.</response>
    [HttpGet]
    [ProducesResponseType<IEnumerable<PlayerDetailResponse>>(StatusCodes.Status200OK)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status404NotFound)]
    public async Task<ActionResult<IEnumerable<PlayerDetailResponse>>> GetAll(Guid gameId, CancellationToken ct)
        => Ok(await players.GetAllAsync(gameId, ct));

    /// <summary>Joins the game as the authenticated user. Only allowed in Lobby state.</summary>
    /// <response code="201">Player joined.</response>
    /// <response code="400">Invalid colour.</response>
    /// <response code="404">Game not found.</response>
    /// <response code="409">Game has started, is full, or the caller is already in it.</response>
    [HttpPost]
    [ProducesResponseType<PlayerDetailResponse>(StatusCodes.Status201Created)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status400BadRequest)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status404NotFound)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status409Conflict)]
    public async Task<ActionResult<PlayerDetailResponse>> Join(Guid gameId, [FromBody] JoinGameRequest request, CancellationToken ct)
    {
        var player = await players.JoinAsync(gameId, request, ct);
        return CreatedAtAction(nameof(GetById), new { gameId, id = player.Id }, player);
    }

    /// <summary>Returns a single player by ID.</summary>
    /// <response code="200">Player returned.</response>
    /// <response code="404">Player or game not found.</response>
    [HttpGet("{id:guid}")]
    [ProducesResponseType<PlayerDetailResponse>(StatusCodes.Status200OK)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status404NotFound)]
    public async Task<ActionResult<PlayerDetailResponse>> GetById(Guid gameId, Guid id, CancellationToken ct)
        => Ok(await players.GetByIdAsync(gameId, id, ct));

    /// <summary>Removes the player from the game. Only allowed in Lobby state and by the player themselves.</summary>
    /// <response code="204">Player removed.</response>
    /// <response code="403">Caller is not this player.</response>
    /// <response code="404">Player not found.</response>
    /// <response code="409">Game has already started.</response>
    [HttpDelete("{id:guid}")]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status403Forbidden)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status404NotFound)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status409Conflict)]
    public async Task<IActionResult> Leave(Guid gameId, Guid id, CancellationToken ct)
    {
        await players.LeaveAsync(gameId, id, ct);
        return NoContent();
    }

    /// <summary>Marks a player as eliminated.</summary>
    /// <response code="200">Player eliminated.</response>
    /// <response code="403">Caller is not part of the game.</response>
    /// <response code="404">Player not found.</response>
    [HttpPut("{id:guid}/eliminate")]
    [ProducesResponseType<PlayerDetailResponse>(StatusCodes.Status200OK)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status403Forbidden)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status404NotFound)]
    public async Task<ActionResult<PlayerDetailResponse>> Eliminate(Guid gameId, Guid id, CancellationToken ct)
        => Ok(await players.EliminateAsync(gameId, id, ct));

    /// <summary>Adds reinforcement troops to a player's pool.</summary>
    /// <response code="200">Troops added.</response>
    /// <response code="400">Troops must be positive.</response>
    /// <response code="403">Caller is not part of the game.</response>
    /// <response code="404">Player not found.</response>
    [HttpPut("{id:guid}/reinforce")]
    [ProducesResponseType<PlayerDetailResponse>(StatusCodes.Status200OK)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status400BadRequest)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status403Forbidden)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status404NotFound)]
    public async Task<ActionResult<PlayerDetailResponse>> Reinforce(Guid gameId, Guid id, [FromBody] ReinforceRequest request, CancellationToken ct)
        => Ok(await players.ReinforceAsync(gameId, id, request, ct));
}
