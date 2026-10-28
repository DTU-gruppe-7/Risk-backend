namespace Risk.Application.Auth;

/// <summary>Token lifetimes the use cases need. Filled from the "Jwt" configuration section in Infrastructure.</summary>
/// <param name="RefreshTokenLifetime">How long a login lasts without activity. Renewed on every refresh.</param>
public sealed record AuthTokenSettings(TimeSpan RefreshTokenLifetime);
