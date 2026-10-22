using Microsoft.AspNetCore.Mvc;
using Risk.Application.Auth;

namespace Risk.Api.Controllers;

/// <summary>Handles user registration and login, returning signed JWTs.</summary>
[ApiController]
[Route("api/[controller]")]
public sealed class AuthController(AuthService auth) : ControllerBase
{
    /// <summary>Creates a new account and returns a JWT on success.</summary>
    /// <response code="200">Registration succeeded — JWT returned.</response>
    /// <response code="400">Validation failed or email already taken.</response>
    [HttpPost("register")]
    [ProducesResponseType<AuthResponse>(StatusCodes.Status200OK)]
    [ProducesResponseType<ValidationProblemDetails>(StatusCodes.Status400BadRequest)]
    public async Task<ActionResult<AuthResponse>> Register([FromBody] RegisterRequest request, CancellationToken ct)
        => Ok(await auth.RegisterAsync(request, ct));

    /// <summary>Validates credentials and returns a fresh JWT.</summary>
    /// <response code="200">Login succeeded — JWT returned.</response>
    /// <response code="401">Email or password incorrect.</response>
    [HttpPost("login")]
    [ProducesResponseType<AuthResponse>(StatusCodes.Status200OK)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status401Unauthorized)]
    public async Task<ActionResult<AuthResponse>> Login([FromBody] LoginRequest request, CancellationToken ct)
        => Ok(await auth.LoginAsync(request, ct));
}
