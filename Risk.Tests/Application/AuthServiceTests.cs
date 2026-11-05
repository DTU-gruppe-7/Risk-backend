using Risk.Application.Auth;
using Risk.Application.Exceptions;

namespace Risk.Tests.Application;

public class AuthServiceTests
{
    private const string Email = "alice@example.com";
    private const string Password = "correct-horse-battery";
    private static readonly TimeSpan RefreshLifetime = TimeSpan.FromDays(7);

    private readonly FakeClock _clock = new(new DateTimeOffset(2026, 10, 28, 12, 0, 0, TimeSpan.Zero));
    private readonly FakeIdentityService _identity = new();
    private readonly FakeRefreshTokenRepository _refreshTokens = new();
    private readonly FakeUnitOfWork _unitOfWork = new();
    private readonly AuthService _auth;

    public AuthServiceTests()
    {
        _auth = new AuthService(new FakeUserRepository(), _identity, new FakeJwtTokenService(), _refreshTokens,
            _unitOfWork, new AuthTokenSettings(RefreshLifetime), _clock);
    }

    private async Task<AuthResult> RegisterAndLoginAsync()
    {
        await _auth.RegisterAsync(new RegisterRequest("Alice", Email, Password));
        return await _auth.LoginAsync(new LoginRequest(Email, Password));
    }

    [Fact]
    public async Task Login_issues_a_refresh_token_and_stores_only_its_hash()
    {
        var login = await RegisterAndLoginAsync();

        Assert.False(string.IsNullOrWhiteSpace(login.RefreshToken));
        Assert.Equal(_clock.Now + RefreshLifetime, login.RefreshTokenExpiresAt);

        var stored = _refreshTokens.Tokens.Last();
        Assert.Equal(RefreshTokenSecrets.Hash(login.RefreshToken), stored.TokenHash);
        Assert.DoesNotContain(_refreshTokens.Tokens, t => t.TokenHash == login.RefreshToken);
    }

    [Fact]
    public async Task Wrong_password_is_rejected()
    {
        await RegisterAndLoginAsync();
        await Assert.ThrowsAsync<AuthenticationFailedException>(
            () => _auth.LoginAsync(new LoginRequest(Email, "wrong-password-123")));
    }

    [Fact]
    public async Task Refresh_rotates_the_token_and_returns_a_new_access_token()
    {
        var login = await RegisterAndLoginAsync();

        var refreshed = await _auth.RefreshAsync(login.RefreshToken);

        Assert.NotNull(refreshed);
        Assert.NotEqual(login.RefreshToken, refreshed.RefreshToken);
        Assert.NotEqual(login.Response.AccessToken, refreshed.Response.AccessToken);
        Assert.Equal(login.Response.User, refreshed.Response.User);

        var oldToken = _refreshTokens.Tokens.Single(t => t.TokenHash == RefreshTokenSecrets.Hash(login.RefreshToken));
        var newToken = _refreshTokens.Tokens.Single(t => t.TokenHash == RefreshTokenSecrets.Hash(refreshed.RefreshToken));
        Assert.True(oldToken.IsRevoked);
        Assert.False(newToken.IsRevoked);
        Assert.Equal(oldToken.FamilyId, newToken.FamilyId);
    }

    [Fact]
    public async Task Refresh_extends_the_session_from_the_time_of_the_refresh()
    {
        var login = await RegisterAndLoginAsync();
        _clock.Advance(TimeSpan.FromDays(6));

        var refreshed = await _auth.RefreshAsync(login.RefreshToken);

        Assert.NotNull(refreshed);
        Assert.Equal(_clock.Now + RefreshLifetime, refreshed.RefreshTokenExpiresAt);
    }

    [Fact]
    public async Task Reusing_an_old_refresh_token_ends_the_whole_login()
    {
        var login = await RegisterAndLoginAsync();
        var refreshed = await _auth.RefreshAsync(login.RefreshToken);
        Assert.NotNull(refreshed);

        // An attacker (or a stale copy) replays the first token.
        Assert.Null(await _auth.RefreshAsync(login.RefreshToken));

        // The newest token from the same login no longer works either.
        Assert.Null(await _auth.RefreshAsync(refreshed.RefreshToken));
    }

    [Fact]
    public async Task Reuse_in_one_login_does_not_affect_another_login()
    {
        var phone = await RegisterAndLoginAsync();
        var laptop = await _auth.LoginAsync(new LoginRequest(Email, Password));

        await _auth.RefreshAsync(phone.RefreshToken);
        await _auth.RefreshAsync(phone.RefreshToken); // reuse → phone login revoked

        Assert.NotNull(await _auth.RefreshAsync(laptop.RefreshToken));
    }

    [Fact]
    public async Task Expired_refresh_token_is_rejected()
    {
        var login = await RegisterAndLoginAsync();
        _clock.Advance(RefreshLifetime + TimeSpan.FromSeconds(1));

        Assert.Null(await _auth.RefreshAsync(login.RefreshToken));
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("not-a-token-we-issued")]
    public async Task Missing_or_unknown_refresh_token_is_rejected(string? token)
    {
        await RegisterAndLoginAsync();
        Assert.Null(await _auth.RefreshAsync(token));
    }

    [Fact]
    public async Task Locked_out_account_cannot_refresh()
    {
        var login = await RegisterAndLoginAsync();
        _identity.LockedOut.Add(login.Response.User.Id);

        Assert.Null(await _auth.RefreshAsync(login.RefreshToken));
    }

    [Fact]
    public async Task Logout_revokes_the_refresh_token()
    {
        var login = await RegisterAndLoginAsync();
        var refreshed = await _auth.RefreshAsync(login.RefreshToken);

        await _auth.LogoutAsync(refreshed!.RefreshToken);

        Assert.Null(await _auth.RefreshAsync(refreshed.RefreshToken));
        var family = _refreshTokens.Tokens.Single(t => t.TokenHash == RefreshTokenSecrets.Hash(login.RefreshToken)).FamilyId;
        Assert.All(_refreshTokens.Tokens.Where(t => t.FamilyId == family), t => Assert.True(t.IsRevoked));
    }

    [Theory]
    [InlineData(null)]
    [InlineData("not-a-token-we-issued")]
    public async Task Logout_without_a_valid_token_does_nothing(string? token)
    {
        var login = await RegisterAndLoginAsync();

        await _auth.LogoutAsync(token);

        Assert.NotNull(await _auth.RefreshAsync(login.RefreshToken));
    }

    [Fact]
    public async Task Login_cleans_up_expired_tokens()
    {
        await RegisterAndLoginAsync();
        _clock.Advance(RefreshLifetime + TimeSpan.FromDays(1));

        await _auth.LoginAsync(new LoginRequest(Email, Password));

        Assert.Single(_refreshTokens.Tokens);
    }

    [Fact]
    public void Generated_tokens_are_random_and_url_safe()
    {
        var tokens = Enumerable.Range(0, 100).Select(_ => RefreshTokenSecrets.Generate()).ToList();

        Assert.Equal(100, tokens.Distinct().Count());
        Assert.All(tokens, t => Assert.Matches("^[A-Za-z0-9_-]{43}$", t));
    }
}
