using System.Text;
using Microsoft.IdentityModel.Tokens;

namespace Risk.Infrastructure.Identity;

/// <summary>
/// The "Jwt" configuration section. Read in one place and shared by token
/// creation (<see cref="JwtTokenService"/>) and token validation (Program.cs).
/// </summary>
public sealed class JwtOptions
{
    public const string SectionName = "Jwt";

    /// <summary>HS256 needs a key of at least 256 bits.</summary>
    public const int MinKeyBytes = 32;

    public string Key { get; set; } = string.Empty;
    public string Issuer { get; set; } = string.Empty;
    public string Audience { get; set; } = string.Empty;

    /// <summary>Kept short: an access token cannot be revoked, so a stolen one should die quickly.</summary>
    public TimeSpan AccessTokenLifetime { get; set; } = TimeSpan.FromMinutes(15);

    /// <summary>How long a login lasts without activity. Each refresh issues a new token with a fresh lifetime.</summary>
    public TimeSpan RefreshTokenLifetime { get; set; } = TimeSpan.FromDays(7);

    /// <summary>Tolerance for clocks that are slightly out of sync between servers.</summary>
    public TimeSpan ClockSkew { get; set; } = TimeSpan.FromSeconds(30);

    public bool IsComplete =>
        !string.IsNullOrWhiteSpace(Key) &&
        !string.IsNullOrWhiteSpace(Issuer) &&
        !string.IsNullOrWhiteSpace(Audience);

    /// <summary>Throws with a readable message if the configuration cannot be used to issue tokens safely.</summary>
    public void Validate()
    {
        if (!IsComplete)
            throw new InvalidOperationException(
                "Jwt:Key, Jwt:Issuer og Jwt:Audience skal være sat. Lokalt: opret .env ud fra .env.example, " +
                "eller sæt Jwt__Key, Jwt__Issuer og Jwt__Audience i drift.");

        if (Encoding.UTF8.GetByteCount(Key) < MinKeyBytes)
            throw new InvalidOperationException(
                $"Jwt:Key skal være mindst {MinKeyBytes} bytes. Generér fx en med: openssl rand -base64 48");

        if (AccessTokenLifetime <= TimeSpan.Zero || RefreshTokenLifetime <= AccessTokenLifetime)
            throw new InvalidOperationException(
                "Jwt:AccessTokenLifetime skal være positiv og kortere end Jwt:RefreshTokenLifetime.");
    }

    public SymmetricSecurityKey SigningKey => new(Encoding.UTF8.GetBytes(Key));

    /// <summary>The rules every incoming access token is checked against.</summary>
    public TokenValidationParameters CreateValidationParameters() => new()
    {
        ValidateIssuer = true,
        ValidIssuer = Issuer,
        ValidateAudience = true,
        ValidAudience = Audience,
        ValidateIssuerSigningKey = true,
        IssuerSigningKey = SigningKey,
        // Only accept the algorithm we sign with, never "none" or a different one chosen by the token.
        ValidAlgorithms = [SecurityAlgorithms.HmacSha256],
        ValidateLifetime = true,
        RequireExpirationTime = true,
        RequireSignedTokens = true,
        ClockSkew = ClockSkew,
    };
}
