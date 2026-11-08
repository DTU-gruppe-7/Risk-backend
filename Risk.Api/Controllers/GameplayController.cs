using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Risk.Application.DTOs;
using Risk.Application.Interfaces.Services;

namespace Risk.Api.Controllers;

[ApiController]
[Route("api/games/{gameId:guid}/gameplay")]
[Authorize]
public sealed class GameplayController(IGameplayService gameplay) : ControllerBase
{
    [HttpPost("attack")]
    [ProducesResponseType<AttackResultDto>(StatusCodes.Status200OK)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status400BadRequest)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status403Forbidden)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status404NotFound)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status409Conflict)]
    public async Task<ActionResult<AttackResultDto>> Attack(
        Guid gameId, [FromBody] AttackRequest request, CancellationToken ct)
    {
        var result = await gameplay.AttackAsync(new AttackDto(
            gameId,
            Guid.Empty,
            request.FromTerritoryId,
            request.ToTerritoryId,
            request.AttackingTroopsCount,
            request.DefendingTroopsCount,
            request.MovingTroopsCount), ct);
        return Ok(result);
    }

    [HttpPost("blitz")]
    [ProducesResponseType<BlitzResultDto>(StatusCodes.Status200OK)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status400BadRequest)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status403Forbidden)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status404NotFound)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status409Conflict)]
    public async Task<ActionResult<BlitzResultDto>> Blitz(
        Guid gameId, [FromBody] BlitzRequest request, CancellationToken ct)
    {
        var result = await gameplay.BlitzAttackAsync(new BlitzAttackDto(
            gameId,
            Guid.Empty,
            request.FromTerritoryId,
            request.ToTerritoryId,
            request.MovingTroopsCount), ct);
        return Ok(result);
    }

    [HttpPost("conquest")]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    public async Task<IActionResult> CompleteConquest(
        Guid gameId, [FromBody] ConquestRequest request, CancellationToken ct)
    {
        await gameplay.CompleteConquestAsync(new CompleteConquestDto(
            gameId, request.FromTerritoryId, request.ToTerritoryId, request.MovingTroopsCount), ct);
        return NoContent();
    }

    public sealed record AttackRequest(
        Guid FromTerritoryId,
        Guid ToTerritoryId,
        int AttackingTroopsCount,
        int DefendingTroopsCount = 2,
        int? MovingTroopsCount = null);

    public sealed record BlitzRequest(
        Guid FromTerritoryId,
        Guid ToTerritoryId,
        int? MovingTroopsCount = null);

    public sealed record ConquestRequest(
        Guid FromTerritoryId,
        Guid ToTerritoryId,
        int MovingTroopsCount);
}
