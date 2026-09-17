namespace Risk.Domain.Entities;

/// <summary>
/// Brugeren som domænet kender den. Indeholder ingen viden om login,
/// kodeord eller ASP.NET Identity - kun det forretningen interesserer sig for.
/// </summary>
public sealed class User
{
    public Guid Id { get; private set; } = Guid.CreateVersion7();
    public string DisplayName { get; private set; } = string.Empty;
    public string Email { get; private set; } = string.Empty;
    public DateTimeOffset CreatedAt { get; private set; } = DateTimeOffset.UtcNow;

    // EF Core kræver en parameterløs konstruktør
    private User() { }

    public User(string displayName, string email)
    {
        if (string.IsNullOrWhiteSpace(displayName))
            throw new ArgumentException("Navn må ikke være tomt.", nameof(displayName));
        if (string.IsNullOrWhiteSpace(email))
            throw new ArgumentException("Email må ikke være tom.", nameof(email));

        DisplayName = displayName.Trim();
        Email = email.Trim().ToLowerInvariant();
    }

    public void Rename(string displayName)
    {
        if (string.IsNullOrWhiteSpace(displayName))
            throw new ArgumentException("Navn må ikke være tomt.", nameof(displayName));

        DisplayName = displayName.Trim();
    }
}
