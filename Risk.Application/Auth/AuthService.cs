using Risk.Application.Exceptions;
using Risk.Application.Interfaces;
using Risk.Application.Interfaces.Repository;
using Risk.Domain.Entities;

namespace Risk.Application.Auth;

/// <summary>
/// Registration, login, refresh and logout.
/// Every successful call issues a short-lived access token (JWT) and a long-lived refresh token.
/// Only a hash of the refresh token is stored, and it is rotated on every use.
/// </summary>
public sealed class AuthService(
    IUserRepository users,
    IIdentityService identity,
    IJwtTokenService tokens,
    IRefreshTokenRepository refreshTokens,
    IUnitOfWork unitOfWork,
    AuthTokenSettings settings,
    TimeProvider clock)
{
    /// <summary>
    /// Creates the domain user and its login credentials in one transaction,
    /// so a failed password check never leaves an orphaned domain user behind.
    /// </summary>
    public async Task<AuthResult> RegisterAsync(RegisterRequest request, CancellationToken ct = default)
    {
        if (await users.GetByEmailAsync(request.Email, ct) is not null)
            throw new ValidationFailedException("DuplicateEmail", $"Email '{request.Email}' is already taken.");

        await using var transaction = await unitOfWork.BeginTransactionAsync(ct);

        var user = new User(request.UserName, request.Email);
        users.Add(user);
        await unitOfWork.SaveChangesAsync(ct);

        await identity.CreateAccountAsync(user.Id, user.Email, request.Password, ct);
        await transaction.CommitAsync(ct);

        return await IssueAsync(user, roles: [], familyId: Guid.CreateVersion7(), ct);
    }

    public async Task<AuthResult> LoginAsync(LoginRequest request, CancellationToken ct = default)
    {
        var account = await identity.AuthenticateAsync(request.Email, request.Password, ct)
                      ?? throw new AuthenticationFailedException();

        var user = await users.GetByIdAsync(account.DomainUserId, ct)
                   ?? throw new AuthenticationFailedException();

        // Housekeeping: old sessions that have run out are no longer needed for anything.
        await refreshTokens.DeleteExpiredAsync(user.Id, clock.GetUtcNow(), ct);

        // A new login starts a new token family (one per device/browser).
        return await IssueAsync(user, account.Roles, familyId: Guid.CreateVersion7(), ct);
    }

    /// <summary>
    /// Exchanges a valid refresh token for a new access token and a new refresh token (rotation).
    /// Returns null if the token is missing, unknown, expired or revoked: the user must log in again.
    /// </summary>
    public async Task<AuthResult?> RefreshAsync(string? refreshToken, CancellationToken ct = default)
    {
        if (string.IsNullOrWhiteSpace(refreshToken))
            return null;

        var stored = await refreshTokens.GetByHashAsync(RefreshTokenSecrets.Hash(refreshToken), ct);
        if (stored is null)
            return null;

        var now = clock.GetUtcNow();

        // A token that was already used is being used again: either an attacker or the real
        // user has an old copy. We cannot tell which, so the whole login is ended.
        if (stored.IsRevoked)
        {
            await refreshTokens.RevokeFamilyAsync(stored.FamilyId, now, ct);
            return null;
        }

        if (stored.IsExpired(now))
            return null;

        // Two requests with the same token at the same time: only one may win.
        // The loser is treated exactly like reuse.
        if (!await refreshTokens.TryRevokeAsync(stored.Id, now, ct))
        {
            await refreshTokens.RevokeFamilyAsync(stored.FamilyId, now, ct);
            return null;
        }

        // Roles are read again, so a changed role (or a locked/deleted account) takes effect on the next refresh.
        var roles = await identity.GetRolesAsync(stored.UserId, ct);
        var user = await users.GetByIdAsync(stored.UserId, ct);
        if (roles is null || user is null)
        {
            await refreshTokens.RevokeFamilyAsync(stored.FamilyId, now, ct);
            return null;
        }

        return await IssueAsync(user, roles, stored.FamilyId, ct);
    }

    /// <summary>Ends the login the refresh token belongs to. Unknown or missing tokens are ignored.</summary>
    public async Task LogoutAsync(string? refreshToken, CancellationToken ct = default)
    {
        if (string.IsNullOrWhiteSpace(refreshToken))
            return;

        var stored = await refreshTokens.GetByHashAsync(RefreshTokenSecrets.Hash(refreshToken), ct);
        if (stored is not null)
            await refreshTokens.RevokeFamilyAsync(stored.FamilyId, clock.GetUtcNow(), ct);
    }

    private async Task<AuthResult> IssueAsync(User user, IEnumerable<string> roles, Guid familyId, CancellationToken ct)
    {
        var now = clock.GetUtcNow();
        var refreshToken = RefreshTokenSecrets.Generate();
        var expiresAt = now.Add(settings.RefreshTokenLifetime);

        refreshTokens.Add(new RefreshToken(user.Id, familyId, RefreshTokenSecrets.Hash(refreshToken), now, expiresAt));
        await unitOfWork.SaveChangesAsync(ct);

        var response = new AuthResponse(tokens.CreateToken(user, roles), AuthUserResponse.From(user));
        return new AuthResult(response, refreshToken, expiresAt);
    }
}
