using System.Security.Cryptography;
using System.Text;

namespace Risk.Application.Auth;

/// <summary>Creates refresh token strings and the hashes that are stored instead of them.</summary>
public static class RefreshTokenSecrets
{
    private const int TokenBytes = 32; // 256 bits of randomness

    /// <summary>A new random, URL-safe token. Only ever sent to the client, never stored.</summary>
    public static string Generate() =>
        Convert.ToBase64String(RandomNumberGenerator.GetBytes(TokenBytes))
            .TrimEnd('=').Replace('+', '-').Replace('/', '_');

    /// <summary>
    /// SHA-256 as hex. A plain (unsalted) hash is enough here, because the token is 256 random bits
    /// and cannot be guessed, unlike a password.
    /// </summary>
    public static string Hash(string token) =>
        Convert.ToHexStringLower(SHA256.HashData(Encoding.UTF8.GetBytes(token)));
}
