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

    /// <param name="FromTerritoryId">The attacking territory (territory state id).</param>
    /// <param name="ToTerritoryId">The defending territory (territory state id).</param>
    /// <param name="AttackingTroopsCount">How many dice the attacker rolls: 1–3, leaving one troop behind.</param>
    /// <param name="MovingTroopsCount">Troops to move in if the territory is conquered; null leaves the conquest pending.</param>
    /// <param name="DefendingTroopsCount">Ignored: the server always rolls as many dice as the defender may. Will be removed.</param>
    public sealed record AttackRequest(
        Guid FromTerritoryId,
        Guid ToTerritoryId,
        int AttackingTroopsCount,
        [property: Obsolete("Ignored. The server decides the defender's dice.")] int? DefendingTroopsCount = null,
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
