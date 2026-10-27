using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Risk.Application.Cards;

namespace Risk.Api.Controllers;

/// <summary>Manages secret mission cards in a game.</summary>
[ApiController]
[Route("api/games/{gameId:guid}/mission-cards")]
[Authorize]
public sealed class MissionCardsController(MissionCardService cards) : ControllerBase
{
    /// <summary>
    /// Returns the caller's own mission card(s) in the game. Missions are secret,
    /// so other players' cards are never returned.
    /// </summary>
    /// <response code="200">Mission cards returned.</response>
    /// <response code="403">Caller is not part of the game.</response>
    /// <response code="404">Game not found.</response>
    [HttpGet]
    [ProducesResponseType<IEnumerable<MissionCardResponse>>(StatusCodes.Status200OK)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status403Forbidden)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status404NotFound)]
    public async Task<ActionResult<IEnumerable<MissionCardResponse>>> GetMine(Guid gameId, CancellationToken ct)
        => Ok(await cards.GetMineAsync(gameId, ct));

    /// <summary>Assigns a mission card to a player.</summary>
    /// <response code="200">Mission assigned.</response>
    /// <response code="400">The player is not part of this game.</response>
    /// <response code="403">Caller is not part of the game.</response>
    /// <response code="404">Mission card not found.</response>
    /// <response code="409">Mission already assigned.</response>
    [HttpPut("{id:guid}/assign")]
    [ProducesResponseType<MissionCardResponse>(StatusCodes.Status200OK)]
    [ProducesResponseType<ValidationProblemDetails>(StatusCodes.Status400BadRequest)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status403Forbidden)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status404NotFound)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status409Conflict)]
    public async Task<ActionResult<MissionCardResponse>> Assign(Guid gameId, Guid id, [FromBody] AssignMissionRequest request, CancellationToken ct)
        => Ok(await cards.AssignAsync(gameId, id, request, ct));

    /// <summary>Marks a mission as completed — the assigned player wins.</summary>
    /// <response code="200">Mission completed.</response>
    /// <response code="403">Caller is not part of the game.</response>
    /// <response code="404">Mission card not found.</response>
    /// <response code="409">Mission not assigned or already completed.</response>
    [HttpPut("{id:guid}/complete")]
    [ProducesResponseType<MissionCardResponse>(StatusCodes.Status200OK)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status403Forbidden)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status404NotFound)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status409Conflict)]
    public async Task<ActionResult<MissionCardResponse>> Complete(Guid gameId, Guid id, CancellationToken ct)
        => Ok(await cards.CompleteAsync(gameId, id, ct));
}
