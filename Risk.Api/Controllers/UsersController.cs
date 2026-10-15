using System.Security.Claims;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Risk.Infrastructure.Persistence;

namespace Risk.Api.Controllers;

/// <summary>Manages the authenticated user's own profile.</summary>
[ApiController]
[Route("api/users")]
[Authorize]
public sealed class UsersController : ControllerBase
{
    private readonly AppDbContext _db;

    public UsersController(AppDbContext db) => _db = db;

    private Guid CallerId =>
        Guid.Parse(User.FindFirstValue(ClaimTypes.NameIdentifier)
                   ?? User.FindFirstValue("sub")
                   ?? throw new InvalidOperationException("No subject claim."));

    // -------------------------------------------------------------------------
    // GET /api/users/me
    // -------------------------------------------------------------------------

    /// <summary>Returns the authenticated user's profile.</summary>
    /// <response code="200">Profile returned.</response>
    /// <response code="404">User not found.</response>
    [HttpGet("me")]
    [ProducesResponseType<UserResponse>(StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<ActionResult<UserResponse>> GetMe()
    {
        var user = await _db.DomainUsers.FindAsync(CallerId);
        return user is null ? NotFound() : Ok(UserResponse.From(user));
    }

    // -------------------------------------------------------------------------
    // PUT /api/users/me
    // -------------------------------------------------------------------------

    /// <summary>Updates the authenticated user's display name.</summary>
    /// <response code="200">Profile updated.</response>
    /// <response code="404">User not found.</response>
    [HttpPut("me")]
    [ProducesResponseType<UserResponse>(StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<ActionResult<UserResponse>> UpdateMe([FromBody] UpdateUserRequest request)
    {
        var user = await _db.DomainUsers.FindAsync(CallerId);
        if (user is null) return NotFound();

        user.Rename(request.DisplayName);
        await _db.SaveChangesAsync();

        return Ok(UserResponse.From(user));
    }
}

// -------------------------------------------------------------------------
// DTOs
// -------------------------------------------------------------------------

/// <summary>Public profile of a domain user.</summary>
/// <param name="Id">Stable domain user ID (also the JWT sub claim).</param>
/// <param name="DisplayName">In-game display name.</param>
/// <param name="Email">Email address.</param>
/// <param name="CreatedAt">When the account was created.</param>
public sealed record UserResponse(Guid Id, string DisplayName, string Email, DateTimeOffset CreatedAt)
{
    internal static UserResponse From(Risk.Domain.Entities.User u) =>
        new(u.Id, u.DisplayName, u.Email, u.CreatedAt);
}

/// <summary>Payload to update a user profile.</summary>
/// <param name="DisplayName">New display name.</param>
public sealed record UpdateUserRequest(string DisplayName);
