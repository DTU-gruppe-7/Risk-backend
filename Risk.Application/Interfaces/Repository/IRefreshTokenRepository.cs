using Risk.Domain.Entities;

namespace Risk.Application.Interfaces.Repository;

public interface IRefreshTokenRepository
{
    Task<RefreshToken?> GetByHashAsync(string tokenHash, CancellationToken cancellationToken = default);

    void Add(RefreshToken token);

    /// <summary>
    /// Marks the token as revoked, but only if it is still active. Runs immediately and atomically,
    /// so two requests with the same token can never both win. Returns false if someone else got there first.
    /// </summary>
    Task<bool> TryRevokeAsync(Guid tokenId, DateTimeOffset now, CancellationToken cancellationToken = default);

    /// <summary>Revokes every active token from the same login. Runs immediately.</summary>
    Task RevokeFamilyAsync(Guid familyId, DateTimeOffset now, CancellationToken cancellationToken = default);

    /// <summary>Deletes the user's tokens that have expired (housekeeping, runs immediately).</summary>
    Task DeleteExpiredAsync(Guid userId, DateTimeOffset now, CancellationToken cancellationToken = default);
}
