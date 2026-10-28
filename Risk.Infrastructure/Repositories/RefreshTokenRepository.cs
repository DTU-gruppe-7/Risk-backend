using Microsoft.EntityFrameworkCore;
using Risk.Application.Interfaces.Repository;
using Risk.Domain.Entities;
using Risk.Infrastructure.Persistence;

namespace Risk.Infrastructure.Repositories;

public sealed class RefreshTokenRepository(AppDbContext context) : IRefreshTokenRepository
{
    private readonly DbSet<RefreshToken> _tokens = context.Set<RefreshToken>();

    public Task<RefreshToken?> GetByHashAsync(string tokenHash, CancellationToken cancellationToken = default) =>
        _tokens.FirstOrDefaultAsync(t => t.TokenHash == tokenHash, cancellationToken);

    public void Add(RefreshToken token) => _tokens.Add(token);

    public async Task<bool> TryRevokeAsync(Guid tokenId, DateTimeOffset now, CancellationToken cancellationToken = default)
    {
        // UPDATE ... WHERE revoked_at IS NULL: only one concurrent request can change the row.
        var updated = await _tokens
            .Where(t => t.Id == tokenId && t.RevokedAt == null)
            .ExecuteUpdateAsync(s => s.SetProperty(t => t.RevokedAt, now), cancellationToken);
        return updated == 1;
    }

    public Task RevokeFamilyAsync(Guid familyId, DateTimeOffset now, CancellationToken cancellationToken = default) =>
        _tokens
            .Where(t => t.FamilyId == familyId && t.RevokedAt == null)
            .ExecuteUpdateAsync(s => s.SetProperty(t => t.RevokedAt, now), cancellationToken);

    public Task DeleteExpiredAsync(Guid userId, DateTimeOffset now, CancellationToken cancellationToken = default) =>
        _tokens
            .Where(t => t.UserId == userId && t.ExpiresAt <= now)
            .ExecuteDeleteAsync(cancellationToken);
}
