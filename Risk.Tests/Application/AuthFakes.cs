using Risk.Application.Interfaces;
using Risk.Application.Interfaces.Repository;
using Risk.Domain.Entities;

namespace Risk.Tests.Application;

// In-memory stand-ins for the auth ports, so the refresh logic can be tested without a database.

internal sealed class FakeClock(DateTimeOffset start) : TimeProvider
{
    public DateTimeOffset Now { get; set; } = start;
    public override DateTimeOffset GetUtcNow() => Now;
    public void Advance(TimeSpan by) => Now += by;
}

internal sealed class FakeUserRepository : IUserRepository
{
    public List<User> Users { get; } = [];

    public Task<User?> GetByIdAsync(Guid id, CancellationToken cancellationToken = default) =>
        Task.FromResult(Users.FirstOrDefault(u => u.Id == id));

    public Task<User?> GetByEmailAsync(string email, CancellationToken cancellationToken = default) =>
        Task.FromResult(Users.FirstOrDefault(u => u.Email == User.NormalizeEmail(email)));

    public void Add(User entity) => Users.Add(entity);
    public void Remove(User entity) => Users.Remove(entity);
}

/// <summary>Accounts keyed by email, with the password stored in plain text (test only).</summary>
internal sealed class FakeIdentityService : IIdentityService
{
    private readonly Dictionary<string, (Guid UserId, string Password)> _accounts = [];
    public HashSet<Guid> LockedOut { get; } = [];

    public Task CreateAccountAsync(Guid domainUserId, string email, string password, CancellationToken cancellationToken = default)
    {
        _accounts[User.NormalizeEmail(email)] = (domainUserId, password);
        return Task.CompletedTask;
    }

    public Task<AuthenticatedAccount?> AuthenticateAsync(string email, string password, CancellationToken cancellationToken = default) =>
        Task.FromResult(_accounts.TryGetValue(User.NormalizeEmail(email), out var a) && a.Password == password
            ? new AuthenticatedAccount(a.UserId, [])
            : null);

    public Task<IReadOnlyList<string>?> GetRolesAsync(Guid domainUserId, CancellationToken cancellationToken = default) =>
        Task.FromResult(_accounts.Values.Any(a => a.UserId == domainUserId) && !LockedOut.Contains(domainUserId)
            ? (IReadOnlyList<string>)[]
            : null);
}

internal sealed class FakeJwtTokenService : IJwtTokenService
{
    public string CreateToken(User user, IEnumerable<string> roles) => $"access-{user.Id}-{Guid.NewGuid()}";
}

internal sealed class FakeRefreshTokenRepository : IRefreshTokenRepository
{
    public List<RefreshToken> Tokens { get; } = [];

    public Task<RefreshToken?> GetByHashAsync(string tokenHash, CancellationToken cancellationToken = default) =>
        Task.FromResult(Tokens.FirstOrDefault(t => t.TokenHash == tokenHash));

    public void Add(RefreshToken token) => Tokens.Add(token);

    public Task<bool> TryRevokeAsync(Guid tokenId, DateTimeOffset now, CancellationToken cancellationToken = default)
    {
        var token = Tokens.FirstOrDefault(t => t.Id == tokenId && !t.IsRevoked);
        token?.Revoke(now);
        return Task.FromResult(token is not null);
    }

    public Task RevokeFamilyAsync(Guid familyId, DateTimeOffset now, CancellationToken cancellationToken = default)
    {
        foreach (var token in Tokens.Where(t => t.FamilyId == familyId)) token.Revoke(now);
        return Task.CompletedTask;
    }

    public Task DeleteExpiredAsync(Guid userId, DateTimeOffset now, CancellationToken cancellationToken = default)
    {
        Tokens.RemoveAll(t => t.UserId == userId && t.IsExpired(now));
        return Task.CompletedTask;
    }
}
