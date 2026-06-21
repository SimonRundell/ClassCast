using System.Security.Cryptography;

namespace ClassCast.Common.Security;

/// <summary>
/// Hashes and verifies the shared teacher password used by the Teacher Server in
/// workgroup (non-domain) mode. Uses PBKDF2 (HMAC-SHA256) with a random per-password
/// salt so the stored value never reveals the password. The encoded form is a single
/// self-describing string:
/// <code>pbkdf2$&lt;iterations&gt;$&lt;saltBase64&gt;$&lt;hashBase64&gt;</code>
/// which keeps every parameter needed for later verification together in config.json.
/// </summary>
public static class PasswordHasher
{
    private const string Scheme = "pbkdf2";
    private const int SaltBytes = 16;
    private const int HashBytes = 32;
    private const int Iterations = 100_000;
    private static readonly HashAlgorithmName Algorithm = HashAlgorithmName.SHA256;

    /// <summary>
    /// Produces a salted PBKDF2 hash string for the given password, suitable for
    /// storing in <c>config.json</c>.
    /// </summary>
    /// <param name="password">The plain-text password to hash. Must not be empty.</param>
    /// <returns>The encoded hash string.</returns>
    /// <exception cref="ArgumentException">Thrown when <paramref name="password"/> is empty.</exception>
    public static string Hash(string password)
    {
        if (string.IsNullOrEmpty(password))
        {
            throw new ArgumentException("Password must not be empty.", nameof(password));
        }

        byte[] salt = RandomNumberGenerator.GetBytes(SaltBytes);
        byte[] hash = Rfc2898DeriveBytes.Pbkdf2(password, salt, Iterations, Algorithm, HashBytes);

        return string.Join('$', Scheme, Iterations, Convert.ToBase64String(salt), Convert.ToBase64String(hash));
    }

    /// <summary>
    /// Verifies a candidate password against a stored hash produced by <see cref="Hash"/>.
    /// </summary>
    /// <param name="password">The candidate plain-text password.</param>
    /// <param name="stored">The encoded hash string previously produced by <see cref="Hash"/>.</param>
    /// <returns>
    /// <c>true</c> if the password matches; <c>false</c> if it does not, or if either input
    /// is empty or the stored value is malformed.
    /// </returns>
    public static bool Verify(string password, string stored)
    {
        if (string.IsNullOrEmpty(password) || string.IsNullOrWhiteSpace(stored))
        {
            return false;
        }

        string[] parts = stored.Split('$');
        if (parts.Length != 4 || parts[0] != Scheme)
        {
            return false;
        }

        if (!int.TryParse(parts[1], out int iterations) || iterations < 1)
        {
            return false;
        }

        byte[] salt, expected;
        try
        {
            salt = Convert.FromBase64String(parts[2]);
            expected = Convert.FromBase64String(parts[3]);
        }
        catch (FormatException)
        {
            return false;
        }

        byte[] actual = Rfc2898DeriveBytes.Pbkdf2(password, salt, iterations, Algorithm, expected.Length);

        // Constant-time comparison to avoid leaking match progress via timing.
        return CryptographicOperations.FixedTimeEquals(actual, expected);
    }
}
