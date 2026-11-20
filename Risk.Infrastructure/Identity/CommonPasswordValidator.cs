using Microsoft.AspNetCore.Identity;

namespace Risk.Infrastructure.Identity;

/// <summary>
/// Afviser almindelige passwords og passwords, der indeholder brugerens email (NIST SP 800-63B).
/// Listen er indlejret i assembly'en, så registrering ikke afhænger af netværk.
/// </summary>
public sealed class CommonPasswordValidator : IPasswordValidator<ApplicationUser>
{
    private static readonly HashSet<string> CommonPasswords = Load();

    public Task<IdentityResult> ValidateAsync(UserManager<ApplicationUser> manager, ApplicationUser user, string? password)
    {
        // Tomme passwords afvises af Identitys egen længdevalidator.
        if (string.IsNullOrEmpty(password))
            return Task.FromResult(IdentityResult.Success);

        if (CommonPasswords.Contains(password))
            return Failed("PasswordTooCommon", "This password is too common. Choose another one.");

        var emailName = user.Email?.Split('@')[0];
        if (emailName is { Length: >= 4 } && password.Contains(emailName, StringComparison.OrdinalIgnoreCase))
            return Failed("PasswordContainsEmail", "The password must not contain your email address.");

        return Task.FromResult(IdentityResult.Success);
    }

    private static Task<IdentityResult> Failed(string code, string description) =>
        Task.FromResult(IdentityResult.Failed(new IdentityError { Code = code, Description = description }));

    private static HashSet<string> Load()
    {
        using var stream = typeof(CommonPasswordValidator).Assembly.GetManifestResourceStream("common-passwords.txt")
            ?? throw new InvalidOperationException("common-passwords.txt is not embedded in the assembly.");
        using var reader = new StreamReader(stream);

        var passwords = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        while (reader.ReadLine() is { } line)
        {
            if (line.Length > 0)
                passwords.Add(line);
        }
        return passwords;
    }
}