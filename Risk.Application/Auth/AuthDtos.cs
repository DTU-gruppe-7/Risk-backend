using Risk.Domain.Entities;

namespace Risk.Application.Auth;

/// <summary>Payload required to create a new account.</summary>
/// <param name="UserName">Publicly visible name shown in-game.</param>
/// <param name="Email">Email address used for login.</param>
/// <param name="Password">Must satisfy the configured password policy (min 12 chars).</param>
public sealed record RegisterRequest(string UserName, string Email, string Password);

/// <summary>Payload required to authenticate.</summary>
public sealed record LoginRequest(string Email, string Password);

/// <summary>Returned after a successful register or login.</summary>
/// <param name="AccessToken">Signed JWT — include as <c>Authorization: Bearer {token}</c> on subsequent requests.</param>
/// <param name="User">The authenticated user.</param>
public sealed record AuthResponse(string AccessToken, AuthUserResponse User);

/// <summary>The authenticated user, as embedded in <see cref="AuthResponse"/>.</summary>
/// <param name="Id">The stable domain user ID (matches player user IDs).</param>
/// <param name="UserName">Publicly visible in-game name.</param>
/// <param name="Email">The user's email address.</param>
public sealed record AuthUserResponse(Guid Id, string UserName, string Email)
{
    internal static AuthUserResponse From(User u) => new(u.Id, u.Username, u.Email);
}
