using Microsoft.AspNetCore.Mvc;
using Risk.Application.Auth;

namespace Risk.Api.Controllers;

/// <summary>
/// Registration, login, refresh and logout.
/// The access token is returned in the body and kept in memory by the frontend.
/// The refresh token is only ever sent as an HttpOnly cookie, so JavaScript (and XSS) cannot read it.
/// </summary>
[ApiController]
[Route("api/[controller]")]
public sealed class AuthController(AuthService auth, IWebHostEnvironment environment) : ControllerBase
{
    private const string RefreshCookieName = "risk_refresh";

    // The browser only sends the cookie to the auth endpoints, not to every API call.
    private const string RefreshCookiePath = "/api/auth";

    /// <summary>Creates a new account, returns an access token and sets the refresh cookie.</summary>
    /// <response code="200">Registration succeeded.</response>
    /// <response code="400">Validation failed or email already taken.</response>
    [HttpPost("register")]
    [ProducesResponseType<AuthResponse>(StatusCodes.Status200OK)]
    [ProducesResponseType<ValidationProblemDetails>(StatusCodes.Status400BadRequest)]
    public async Task<ActionResult<AuthResponse>> Register([FromBody] RegisterRequest request, CancellationToken ct)
        => Issue(await auth.RegisterAsync(request, ct));

    /// <summary>Validates credentials, returns an access token and sets the refresh cookie.</summary>
    /// <response code="200">Login succeeded.</response>
    /// <response code="401">Email or password incorrect.</response>
    [HttpPost("login")]
    [ProducesResponseType<AuthResponse>(StatusCodes.Status200OK)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status401Unauthorized)]
    public async Task<ActionResult<AuthResponse>> Login([FromBody] LoginRequest request, CancellationToken ct)
        => Issue(await auth.LoginAsync(request, ct));

    /// <summary>
    /// Exchanges the refresh cookie for a new access token. The refresh token is rotated:
    /// the old one stops working and a new cookie is set.
    /// </summary>
    /// <response code="200">A new access token.</response>
    /// <response code="401">No valid refresh cookie. The user must log in again.</response>
    [HttpPost("refresh")]
    [ProducesResponseType<AuthResponse>(StatusCodes.Status200OK)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status401Unauthorized)]
    public async Task<ActionResult<AuthResponse>> Refresh(CancellationToken ct)
    {
        var result = await auth.RefreshAsync(Request.Cookies[RefreshCookieName], ct);
        if (result is null)
        {
            DeleteRefreshCookie();
            return Problem(statusCode: StatusCodes.Status401Unauthorized, title: "Unauthorized.",
                detail: "The session has expired. Log in again.");
        }

        return Issue(result);
    }

    /// <summary>Ends the login: revokes the refresh token and deletes the cookie. Always succeeds.</summary>
    /// <response code="204">Logged out.</response>
    [HttpPost("logout")]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    public async Task<IActionResult> Logout(CancellationToken ct)
    {
        await auth.LogoutAsync(Request.Cookies[RefreshCookieName], ct);
        DeleteRefreshCookie();
        return NoContent();
    }

    private ActionResult<AuthResponse> Issue(AuthResult result)
    {
        Response.Cookies.Append(RefreshCookieName, result.RefreshToken, CookieOptions(result.RefreshTokenExpiresAt));
        // Token responses must never be cached by the browser or a proxy.
        Response.Headers.CacheControl = "no-store";
        return Ok(result.Response);
    }

    private void DeleteRefreshCookie() =>
        Response.Cookies.Delete(RefreshCookieName, CookieOptions(expires: null));

    private CookieOptions CookieOptions(DateTimeOffset? expires) => new()
    {
        HttpOnly = true,
        // Always Secure outside Development. Locally (plain http://localhost) the browser would drop it.
        Secure = Request.IsHttps || !environment.IsDevelopment(),
        // Frontend and API share one origin (Vite proxy locally, Caddy in production),
        // so the cookie is never needed on cross-site requests. This also blocks CSRF.
        SameSite = SameSiteMode.Strict,
        Path = RefreshCookiePath,
        Expires = expires,
        IsEssential = true,
    };
}
