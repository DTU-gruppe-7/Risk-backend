using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Risk.Application.Cards;

namespace Risk.Api.Controllers;

/// <summary>Game rules that are the same for every game, so clients can explain them without copying them.</summary>
[ApiController]
[Route("api/rules")]
[AllowAnonymous]
public sealed class RulesController : ControllerBase
{
    /// <summary>The card trade rules: set values in both trade modes, the territory bonus and the forced trade.</summary>
    /// <response code="200">Rules returned. They never change at runtime, so clients may cache them.</response>
    [HttpGet("card-trades")]
    [ResponseCache(Duration = 3600)]
    [ProducesResponseType<CardTradeRulesResponse>(StatusCodes.Status200OK)]
    public ActionResult<CardTradeRulesResponse> GetCardTrades() => Ok(CardTradeRulesResponse.Current);
}
