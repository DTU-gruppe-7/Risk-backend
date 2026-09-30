using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Risk.Application.Interfaces;
using Risk.Domain.Entities;
using Risk.Infrastructure.Identity;
using Risk.Infrastructure.Persistence;

namespace Risk.Api.Controllers;

/// <summary>Handles user registration and login, returning signed JWTs.</summary>
[ApiController]
[Route("api/[controller]")]
public sealed class AuthController : ControllerBase
{
    private readonly UserManager<ApplicationUser> _userManager;
    private readonly IJwtTokenService _tokenService;
    private readonly AppDbContext _db;

    public AuthController(
        UserManager<ApplicationUser> userManager,
        IJwtTokenService tokenService,
        AppDbContext db)
    {
        _userManager = userManager;
        _tokenService = tokenService;
        _db = db;
    }

    // -------------------------------------------------------------------------
    // POST /api/auth/register
    // -------------------------------------------------------------------------

    /// <summary>Creates a new account and returns a JWT on success.</summary>
    /// <response code="200">Registration succeeded — JWT returned.</response>
    /// <response code="400">Validation failed or email already taken.</response>
    [HttpPost("register")]
    [ProducesResponseType<AuthResponse>(StatusCodes.Status200OK)]
    [ProducesResponseType<ValidationProblemDetails>(StatusCodes.Status400BadRequest)]
    public async Task<ActionResult<AuthResponse>> Register([FromBody] RegisterRequest request)
    {
        // 1. Create the domain user (business identity)
        var domainUser = new User(request.DisplayName, request.Email);
        _db.DomainUsers.Add(domainUser);
        await _db.SaveChangesAsync();

        // 2. Create the Identity user (login credentials), linked to the domain user
        var identityUser = new ApplicationUser
        {
            UserName = request.Email,
            Email    = request.Email,
            DomainUserId = domainUser.Id
        };

        var result = await _userManager.CreateAsync(identityUser, request.Password);
        if (!result.Succeeded)
        {
            // Roll back the domain user if identity creation failed
            _db.DomainUsers.Remove(domainUser);
            await _db.SaveChangesAsync();

            foreach (var error in result.Errors)
                ModelState.AddModelError(error.Code, error.Description);

            return ValidationProblem();
        }

        var token = _tokenService.CreateToken(domainUser, []);
        return Ok(new AuthResponse(domainUser.Id, domainUser.DisplayName, domainUser.Email, token));
    }

    // -------------------------------------------------------------------------
    // POST /api/auth/login
    // -------------------------------------------------------------------------

    /// <summary>Validates credentials and returns a fresh JWT.</summary>
    /// <response code="200">Login succeeded — JWT returned.</response>
    /// <response code="401">Email or password incorrect.</response>
    [HttpPost("login")]
    [ProducesResponseType<AuthResponse>(StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status401Unauthorized)]
    public async Task<ActionResult<AuthResponse>> Login([FromBody] LoginRequest request)
    {
        var identityUser = await _userManager.FindByEmailAsync(request.Email);
        if (identityUser is null || !await _userManager.CheckPasswordAsync(identityUser, request.Password))
            return Unauthorized();

        var domainUser = await _db.DomainUsers.FindAsync(identityUser.DomainUserId);
        if (domainUser is null)
            return Unauthorized();

        var roles = await _userManager.GetRolesAsync(identityUser);
        var token = _tokenService.CreateToken(domainUser, roles);
        return Ok(new AuthResponse(domainUser.Id, domainUser.DisplayName, domainUser.Email, token));
    }
}

// -------------------------------------------------------------------------
// Request / response records
// -------------------------------------------------------------------------

/// <summary>Payload required to create a new account.</summary>
/// <param name="DisplayName">Publicly visible name shown in-game.</param>
/// <param name="Email">Email address used for login.</param>
/// <param name="Password">Must satisfy the configured password policy (min 12 chars).</param>
public sealed record RegisterRequest(string DisplayName, string Email, string Password);

/// <summary>Payload required to authenticate.</summary>
public sealed record LoginRequest(string Email, string Password);

/// <summary>Returned after a successful register or login.</summary>
/// <param name="UserId">The stable domain user ID.</param>
/// <param name="DisplayName">Publicly visible in-game name.</param>
/// <param name="Email">The user's email address.</param>
/// <param name="Token">Signed JWT — include as <c>Authorization: Bearer {token}</c> on subsequent requests.</param>
public sealed record AuthResponse(Guid UserId, string DisplayName, string Email, string Token);
