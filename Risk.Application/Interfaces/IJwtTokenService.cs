using Risk.Domain.Entities;

namespace Risk.Application.Interfaces;

/// <summary>
/// Generates a signed JWT for an authenticated domain user.
/// The implementation lives in Infrastructure so that Application
/// never takes a dependency on any JWT library.
/// </summary>
public interface IJwtTokenService
{
    /// <summary>
    /// Creates a signed JWT containing the user's identity claims.
    /// </summary>
    /// <param name="user">The domain user the token is issued for.</param>
    /// <param name="roles">Any roles assigned to the user.</param>
    /// <returns>A compact, signed JWT string.</returns>
    string CreateToken(User user, IEnumerable<string> roles);
}
