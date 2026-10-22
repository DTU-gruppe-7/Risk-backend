namespace Risk.Infrastructure.Identity;

/// <summary>
/// The "Jwt" configuration section. Read in one place and shared by token
/// creation (<see cref="JwtTokenService"/>) and token validation (Program.cs).
/// </summary>
public sealed class JwtOptions
{
    public const string SectionName = "Jwt";

    public string Key { get; set; } = string.Empty;
    public string Issuer { get; set; } = string.Empty;
    public string Audience { get; set; } = string.Empty;
    public TimeSpan Lifetime { get; set; } = TimeSpan.FromHours(8);

    public bool IsComplete =>
        !string.IsNullOrWhiteSpace(Key) &&
        !string.IsNullOrWhiteSpace(Issuer) &&
        !string.IsNullOrWhiteSpace(Audience);
}
