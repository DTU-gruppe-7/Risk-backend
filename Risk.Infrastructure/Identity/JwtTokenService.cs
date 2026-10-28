using System.Security.Claims;
using Microsoft.Extensions.Options;
using Microsoft.IdentityModel.JsonWebTokens;
using Microsoft.IdentityModel.Tokens;
using Risk.Application.Interfaces;
using Risk.Domain.Entities;

namespace Risk.Infrastructure.Identity;

/// <summary>Creates short-lived signed access tokens (JWTs) for authenticated users.</summary>
public sealed class JwtTokenService(IOptions<JwtOptions> options, TimeProvider clock) : IJwtTokenService
{
    private readonly JwtOptions _options = options.Value;

    public string CreateToken(User user, IEnumerable<string> roles)
    {
        _options.Validate();

        var claims = new List<Claim>
        {
            // Subject: the stable domain user ID (not the Identity user ID)
            new(JwtRegisteredClaimNames.Sub, user.Id.ToString()),
            new(JwtRegisteredClaimNames.Email, user.Email),
            new(JwtRegisteredClaimNames.Name, user.Username),
            // Unique token ID, e.g. for logging or a deny list later
            new(JwtRegisteredClaimNames.Jti, Guid.CreateVersion7().ToString()),
        };
        claims.AddRange(roles.Select(role => new Claim(ClaimTypes.Role, role)));

        var now = clock.GetUtcNow().UtcDateTime;
        var descriptor = new SecurityTokenDescriptor
        {
            Issuer = _options.Issuer,
            Audience = _options.Audience,
            Subject = new ClaimsIdentity(claims),
            IssuedAt = now,
            NotBefore = now,
            Expires = now.Add(_options.AccessTokenLifetime),
            SigningCredentials = new SigningCredentials(_options.SigningKey, SecurityAlgorithms.HmacSha256),
        };

        // JsonWebTokenHandler is the current, faster handler (JwtSecurityTokenHandler is legacy).
        return new JsonWebTokenHandler().CreateToken(descriptor);
    }
}
