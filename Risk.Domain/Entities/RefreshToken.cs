namespace Risk.Domain.Entities;

/// <summary>
/// En refresh token, som serveren kender den. Selve token-strengen gemmes aldrig,
/// kun dens SHA-256-hash, så et læk af databasen ikke giver adgang til nogens konto.
/// </summary>
/// <remarks>
/// Alle tokens fra samme login deler <see cref="FamilyId"/>. Ved hver refresh tilbagekaldes
/// den gamle token og en ny udstedes i samme familie (rotation). Bliver en allerede brugt
/// token sendt igen, er den sandsynligvis stjålet, og hele familien tilbagekaldes.
/// </remarks>
public sealed class RefreshToken
{
    public Guid Id { get; private set; } = Guid.CreateVersion7();
    public Guid UserId { get; private set; }
    public Guid FamilyId { get; private set; }
    public string TokenHash { get; private set; } = string.Empty;
    public DateTimeOffset CreatedAt { get; private set; }
    public DateTimeOffset ExpiresAt { get; private set; }
    public DateTimeOffset? RevokedAt { get; private set; }

    // EF Core kræver en parameterløs konstruktør
    private RefreshToken() { }

    public RefreshToken(Guid userId, Guid familyId, string tokenHash, DateTimeOffset createdAt, DateTimeOffset expiresAt)
    {
        UserId = userId;
        FamilyId = familyId;
        TokenHash = tokenHash;
        CreatedAt = createdAt;
        ExpiresAt = expiresAt;
    }

    public bool IsRevoked => RevokedAt is not null;

    public bool IsExpired(DateTimeOffset now) => ExpiresAt <= now;

    public void Revoke(DateTimeOffset now) => RevokedAt ??= now;
}
