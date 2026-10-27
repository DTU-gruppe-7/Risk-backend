using System.IdentityModel.Tokens.Jwt;
using System.Security.Claims;
using System.Text;
using Microsoft.Extensions.Options;
using Microsoft.IdentityModel.Tokens;
using Risk.Application.Interfaces;
using Risk.Domain.Entities;

namespace Risk.Infrastructure.Identity;

/// <summary>Creates signed JWTs for authenticated users.</summary>
public sealed class JwtTokenService(IOptions<JwtOptions> options) : IJwtTokenService
{
    private readonly JwtOptions _options = options.Value;

    public string CreateToken(User user, IEnumerable<string> roles)
    {
        if (!_options.IsComplete)
            throw new InvalidOperationException("Jwt:Key, Jwt:Issuer and Jwt:Audience must be configured.");

        var signingKey = new SymmetricSecurityKey(Encoding.UTF8.GetBytes(_options.Key));
        var credentials = new SigningCredentials(signingKey, SecurityAlgorithms.HmacSha256);

        var claims = new List<Claim>
        {
            // Subject: the stable domain user ID (not the Identity user ID)
            new(JwtRegisteredClaimNames.Sub, user.Id.ToString()),
            new(JwtRegisteredClaimNames.Email, user.Email),
            new(JwtRegisteredClaimNames.Name, user.Username),
            // Unique token ID — useful for token revocation lists in the future
            new(JwtRegisteredClaimNames.Jti, Guid.CreateVersion7().ToString()),
        };

        claims.AddRange(roles.Select(role => new Claim(ClaimTypes.Role, role)));

        var now = DateTime.UtcNow;
        var token = new JwtSecurityToken(
            issuer:   _options.Issuer,
            audience: _options.Audience,
            claims:   claims,
            notBefore: now,
            expires:  now.Add(_options.Lifetime),
            signingCredentials: credentials);

        return new JwtSecurityTokenHandler().WriteToken(token);
    }
}
