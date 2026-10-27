using Risk.Domain.Entities;

namespace Risk.Application.Users;

/// <summary>Public profile of a domain user.</summary>
/// <param name="Id">Stable domain user ID (also the JWT sub claim).</param>
/// <param name="UserName">In-game name.</param>
/// <param name="Email">Email address.</param>
/// <param name="CreatedAt">When the account was created.</param>
public sealed record UserResponse(Guid Id, string UserName, string Email, DateTimeOffset CreatedAt)
{
    internal static UserResponse From(User u) =>
        new(u.Id, u.Username, u.Email, u.CreatedAt);
}

/// <summary>Payload to update a user profile.</summary>
/// <param name="UserName">New in-game name.</param>
public sealed record UpdateUserRequest(string UserName);
