using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Risk.Application.Games;
using Risk.Domain.Entities;

namespace Risk.Api.Controllers;

/// <summary>Manages Risk game sessions and their settings.</summary>
[ApiController]
[Route("api/games")]
[Authorize]
public sealed class GamesController(GameService games) : ControllerBase
{
    /// <summary>Lists games, optionally filtered by state.</summary>
    /// <param name="state">Optional filter: Lobby | InProgress | Finished</param>
    /// <param name="ct">Cancellation token.</param>
    /// <response code="200">List of games returned.</response>
    [HttpGet]
    [ProducesResponseType<IEnumerable<GameSummaryResponse>>(StatusCodes.Status200OK)]
    public async Task<ActionResult<IEnumerable<GameSummaryResponse>>> GetAll([FromQuery] GameState? state, CancellationToken ct)
        => Ok(await games.GetAllAsync(state, ct));

    /// <summary>Creates a new game with default settings. The caller becomes host.</summary>
    /// <response code="201">Game created.</response>
    /// <response code="400">Invalid settings.</response>
    [HttpPost]
    [ProducesResponseType<GameDetailResponse>(StatusCodes.Status201Created)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status400BadRequest)]
    public async Task<ActionResult<GameDetailResponse>> Create([FromBody] CreateGameRequest request, CancellationToken ct)
    {
        var game = await games.CreateAsync(request, ct);
        return CreatedAtAction(nameof(GetById), new { id = game.Id }, game);
    }

    /// <summary>Returns full details for a game including settings and player list.</summary>
    /// <response code="200">Game found.</response>
    /// <response code="404">Game not found.</response>
    [HttpGet("{id:guid}")]
    [ProducesResponseType<GameDetailResponse>(StatusCodes.Status200OK)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status404NotFound)]
    public async Task<ActionResult<GameDetailResponse>> GetById(Guid id, CancellationToken ct)
        => Ok(await games.GetByIdAsync(id, ct));

    /// <summary>Starts the game (Lobby → InProgress). Only the host may do this.</summary>
    /// <response code="200">Game started.</response>
    /// <response code="403">Caller is not the host.</response>
    /// <response code="404">Game not found.</response>
    /// <response code="409">Game is not in Lobby state.</response>
    [HttpPut("{id:guid}/start")]
    [ProducesResponseType<GameDetailResponse>(StatusCodes.Status200OK)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status403Forbidden)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status404NotFound)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status409Conflict)]
    public async Task<ActionResult<GameDetailResponse>> Start(Guid id, CancellationToken ct)
        => Ok(await games.StartAsync(id, ct));

    /// <summary>Ends the game (InProgress → Finished). Only the host may do this.</summary>
    /// <response code="200">Game ended.</response>
    /// <response code="403">Caller is not the host.</response>
    /// <response code="404">Game not found.</response>
    /// <response code="409">Game is not in InProgress state.</response>
    [HttpPut("{id:guid}/end")]
    [ProducesResponseType<GameDetailResponse>(StatusCodes.Status200OK)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status403Forbidden)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status404NotFound)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status409Conflict)]
    public async Task<ActionResult<GameDetailResponse>> End(Guid id, CancellationToken ct)
        => Ok(await games.EndAsync(id, ct));

    /// <summary>Ends the current phase and advances to the next phase or player turn.</summary>
    /// <response code="200">Phase updated.</response>
    /// <response code="403">Caller is not part of the game.</response>
    /// <response code="404">Game not found.</response>
    /// <response code="409">Game is not in progress.</response>
    [HttpPut("{id:guid}/phase")]
    [ProducesResponseType<GameDetailResponse>(StatusCodes.Status200OK)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status403Forbidden)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status404NotFound)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status409Conflict)]
    public async Task<ActionResult<GameDetailResponse>> SetPhase(Guid id, [FromBody] SetPhaseRequest request, CancellationToken ct)
        => Ok(await games.CompletePhaseAsync(id, request, ct));

    /// <summary>Moves troops along a connected path during Fortify and ends the turn.</summary>
    /// <response code="200">Troops moved and the next player's Deployment phase started.</response>
    /// <response code="404">Game or territory not found.</response>
    /// <response code="409">Fortification is not allowed in the current game state.</response>
    [HttpPut("{id:guid}/fortify")]
    [ProducesResponseType<GameDetailResponse>(StatusCodes.Status200OK)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status404NotFound)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status409Conflict)]
    public async Task<ActionResult<GameDetailResponse>> Fortify(Guid id, [FromBody] FortifyRequest request, CancellationToken ct)
        => Ok(await games.FortifyAsync(id, request, ct));

    /// <summary>Returns the settings for a game.</summary>
    /// <response code="200">Settings returned.</response>
    /// <response code="404">Game or settings not found.</response>
    [HttpGet("{id:guid}/settings")]
    [ProducesResponseType<GameSettingResponse>(StatusCodes.Status200OK)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status404NotFound)]
    public async Task<ActionResult<GameSettingResponse>> GetSettings(Guid id, CancellationToken ct)
        => Ok(await games.GetSettingsAsync(id, ct));

    /// <summary>Updates game settings. Only allowed for the host while in Lobby state.</summary>
    /// <response code="200">Settings updated.</response>
    /// <response code="400">Invalid settings.</response>
    /// <response code="403">Caller is not the host.</response>
    /// <response code="404">Game not found.</response>
    /// <response code="409">Game has already started.</response>
    [HttpPut("{id:guid}/settings")]
    [ProducesResponseType<GameSettingResponse>(StatusCodes.Status200OK)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status400BadRequest)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status403Forbidden)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status404NotFound)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status409Conflict)]
    public async Task<ActionResult<GameSettingResponse>> UpdateSettings(Guid id, [FromBody] UpdateGameSettingRequest request, CancellationToken ct)
        => Ok(await games.UpdateSettingsAsync(id, request, ct));
}
