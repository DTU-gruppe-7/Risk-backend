using Microsoft.Extensions.Options;
using Microsoft.IdentityModel.JsonWebTokens;
using Microsoft.IdentityModel.Tokens;
using Risk.Domain.Entities;
using Risk.Infrastructure.Identity;
using Risk.Tests.Application;

namespace Risk.Tests.Infrastructure;

/// <summary>Issues tokens with <see cref="JwtTokenService"/> and validates them with the same rules as Program.cs.</summary>
public class JwtTokenServiceTests
{
    private readonly FakeClock _clock = new(DateTimeOffset.UtcNow);
    private readonly User _user = new("Alice", "alice@example.com");

    private static JwtOptions Options(string key = "a-test-signing-key-that-is-long-enough-for-hs256") => new()
    {
        Key = key,
        Issuer = "risk-tests",
        Audience = "risk-frontend-tests",
        AccessTokenLifetime = TimeSpan.FromMinutes(15),
    };

    private string CreateToken(JwtOptions options) =>
        new JwtTokenService(Microsoft.Extensions.Options.Options.Create(options), _clock).CreateToken(_user, ["Admin"]);

    private static Task<TokenValidationResult> ValidateAsync(string token, JwtOptions options) =>
        new JsonWebTokenHandler().ValidateTokenAsync(token, options.CreateValidationParameters());

    [Fact]
    public async Task Token_is_valid_and_carries_the_user()
    {
        var options = Options();
        var result = await ValidateAsync(CreateToken(options), options);

        Assert.True(result.IsValid, result.Exception?.Message);
        var jwt = (JsonWebToken)result.SecurityToken;
        Assert.Equal(_user.Id.ToString(), jwt.Subject);
        Assert.Equal(_clock.Now.AddMinutes(15).ToUnixTimeSeconds(), new DateTimeOffset(jwt.ValidTo).ToUnixTimeSeconds());
        Assert.Contains(jwt.Claims, c => c.Value == "Admin");
    }

    [Fact]
    public async Task Expired_token_is_rejected_after_the_clock_skew()
    {
        var options = Options();
        _clock.Now = DateTimeOffset.UtcNow - options.AccessTokenLifetime - options.ClockSkew - TimeSpan.FromSeconds(5);

        var result = await ValidateAsync(CreateToken(options), options);

        Assert.False(result.IsValid);
        Assert.IsType<SecurityTokenExpiredException>(result.Exception);
    }

    [Fact]
    public async Task Token_signed_with_another_key_is_rejected()
    {
        var token = CreateToken(Options("another-signing-key-that-is-also-long-enough-xx"));
        var result = await ValidateAsync(token, Options());
        Assert.False(result.IsValid);
    }

    [Fact]
    public async Task Token_for_another_audience_is_rejected()
    {
        var options = Options();
        var token = CreateToken(options);
        options.Audience = "some-other-app";

        Assert.False((await ValidateAsync(token, options)).IsValid);
    }

    [Fact]
    public async Task Unsigned_token_is_rejected()
    {
        var options = Options();
        var unsigned = new JsonWebTokenHandler().CreateToken(new SecurityTokenDescriptor
        {
            Issuer = options.Issuer,
            Audience = options.Audience,
            Expires = DateTime.UtcNow.AddMinutes(5),
            Claims = new Dictionary<string, object> { ["sub"] = _user.Id.ToString() },
        });

        Assert.False((await ValidateAsync(unsigned, options)).IsValid);
    }

    [Fact]
    public void Too_short_key_is_refused()
    {
        var options = Options("too-short");
        Assert.Throws<InvalidOperationException>(options.Validate);
    }
}
