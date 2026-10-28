namespace Risk.Application.Interfaces;

/// <summary>
/// Login credentials (passwords, roles). The implementation wraps ASP.NET Identity
/// in Infrastructure, so Application never depends on it.
/// </summary>
public interface IIdentityService
{
    /// <summary>Creates login credentials linked to an existing domain user.</summary>
    /// <exception cref="Exceptions.ValidationFailedException">Password policy or uniqueness violated.</exception>
    Task CreateAccountAsync(Guid domainUserId, string email, string password, CancellationToken cancellationToken = default);

    /// <summary>Returns the account if the credentials are valid, otherwise null.</summary>
    Task<AuthenticatedAccount?> AuthenticateAsync(string email, string password, CancellationToken cancellationToken = default);

    /// <summary>
    /// The current roles of the account linked to the domain user, or null if the account
    /// no longer exists or is locked out. Used on refresh, so role changes take effect.
    /// </summary>
    Task<IReadOnlyList<string>?> GetRolesAsync(Guid domainUserId, CancellationToken cancellationToken = default);
}

public sealed record AuthenticatedAccount(Guid DomainUserId, IReadOnlyList<string> Roles);
