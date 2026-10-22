using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Risk.Application.Users;

namespace Risk.Api.Controllers;

/// <summary>Manages the authenticated user's own profile.</summary>
[ApiController]
[Route("api/users")]
[Authorize]
public sealed class UsersController(UserService users) : ControllerBase
{
    /// <summary>Returns the authenticated user's profile.</summary>
    /// <response code="200">Profile returned.</response>
    /// <response code="404">User not found.</response>
    [HttpGet("me")]
    [ProducesResponseType<UserResponse>(StatusCodes.Status200OK)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status404NotFound)]
    public async Task<ActionResult<UserResponse>> GetMe(CancellationToken ct)
        => Ok(await users.GetMeAsync(ct));

    /// <summary>Updates the authenticated user's name.</summary>
    /// <response code="200">Profile updated.</response>
    /// <response code="400">Name is empty.</response>
    /// <response code="404">User not found.</response>
    [HttpPut("me")]
    [ProducesResponseType<UserResponse>(StatusCodes.Status200OK)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status400BadRequest)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status404NotFound)]
    public async Task<ActionResult<UserResponse>> UpdateMe([FromBody] UpdateUserRequest request, CancellationToken ct)
        => Ok(await users.UpdateMeAsync(request, ct));
}
