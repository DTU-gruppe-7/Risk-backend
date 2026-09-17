using Microsoft.AspNetCore.Mvc;

namespace Risk.Api.Controllers;

/// <summary>Simpelt endpoint til at verificere at API og typegenerering virker.</summary>
[ApiController]
[Route("api/[controller]")]
public sealed class PingController : ControllerBase
{
    /// <summary>Returnerer servertid og status.</summary>
    [HttpGet]
    [ProducesResponseType<PingResponse>(StatusCodes.Status200OK)]
    public ActionResult<PingResponse> Get()
        => Ok(new PingResponse("ok", DateTimeOffset.UtcNow));
}

public sealed record PingResponse(string Status, DateTimeOffset ServerTime);
