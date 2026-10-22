using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Risk.Application.Map;

namespace Risk.Api.Controllers;

/// <summary>The static board: continents, territories and borders. Same for every game.</summary>
[ApiController]
[Route("api/map")]
[AllowAnonymous]
public sealed class MapController(MapService map) : ControllerBase
{
    /// <summary>Returns the whole board. Never changes at runtime, so clients may cache it.</summary>
    /// <response code="200">Board returned.</response>
    [HttpGet]
    [ResponseCache(Duration = 3600)]
    [ProducesResponseType<MapResponse>(StatusCodes.Status200OK)]
    public async Task<ActionResult<MapResponse>> Get(CancellationToken ct)
        => Ok(await map.GetMapAsync(ct));
}
