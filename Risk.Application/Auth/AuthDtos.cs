using Risk.Domain.Entities;

namespace Risk.Application.Auth;

/// <summary>Payload required to create a new account.</summary>
/// <param name="UserName">Publicly visible name shown in-game.</param>
/// <param name="Email">Email address used for login.</param>
/// <param name="Password">Must satisfy the configured password policy (min 12 chars).</param>
public sealed record RegisterRequest(string UserName, string Email, string Password);

/// <summary>Payload required to authenticate.</summary>
public sealed record LoginRequest(string Email, string Password);

/// <summary>Returned after a successful register, login or refresh.</summary>
/// <param name="AccessToken">Short-lived signed JWT — include as <c>Authorization: Bearer {token}</c> on subsequent requests.
/// The refresh token is not part of the body: it is set as an HttpOnly cookie that JavaScript cannot read.</param>
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

/// <summary>
/// What the use cases hand to the API: the response body plus the refresh token,
/// which the API puts in a cookie instead of the body.
/// </summary>
public sealed record AuthResult(AuthResponse Response, string RefreshToken, DateTimeOffset RefreshTokenExpiresAt);
