using Risk.Domain.Exceptions;

namespace Risk.Domain.Common;

/// <summary>Shared argument checks, so every entity validates the same way.</summary>
internal static class Guard
{
    public static Guid NotEmpty(Guid value, string name) =>
        value == Guid.Empty
            ? throw new DomainValidationException($"{name} must be a valid GUID.")
            : value;

    public static string NotBlank(string? value, string name) =>
        string.IsNullOrWhiteSpace(value)
            ? throw new DomainValidationException($"{name} must not be empty.")
            : value.Trim();

    public static int Positive(int value, string name) =>
        value <= 0
            ? throw new DomainValidationException($"{name} must be positive.")
            : value;

    public static int NotNegative(int value, string name) =>
        value < 0
            ? throw new DomainValidationException($"{name} must be non-negative.")
            : value;

    public static int InRange(int value, int min, int max, string name) =>
        value < min || value > max
            ? throw new DomainValidationException($"{name} must be between {min} and {max}.")
            : value;
}
