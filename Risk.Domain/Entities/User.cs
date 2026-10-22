using Risk.Domain.Exceptions;

namespace Risk.Domain.Entities;

/// <summary>
/// Brugeren som domænet kender den. Indeholder ingen viden om login,
/// kodeord eller ASP.NET Identity - kun det forretningen interesserer sig for.
/// </summary>
public sealed class User
{
    public Guid Id { get; private set; } = Guid.CreateVersion7();
    public string Username { get; private set; } = string.Empty;
    public string Email { get; private set; } = string.Empty;
    public DateTimeOffset CreatedAt { get; private set; } = DateTimeOffset.UtcNow;

    // EF Core kræver en parameterløs konstruktør
    private User() { }

    public User(string username, string email)
    {
        if (string.IsNullOrWhiteSpace(email))
            throw new DomainValidationException("Email må ikke være tom.");

        Rename(username);
        Email = NormalizeEmail(email);
    }

    public void Rename(string username)
    {
        if (string.IsNullOrWhiteSpace(username))
            throw new DomainValidationException("Navn må ikke være tomt.");

        Username = username.Trim();
    }

    /// <summary>Emails gemmes altid i samme form, så opslag og unik-index er case-insensitive.</summary>
    public static string NormalizeEmail(string email) => email.Trim().ToLowerInvariant();
}
