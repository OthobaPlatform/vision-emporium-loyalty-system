namespace VELoyalty.Auth;

/// <summary>
/// Provides password hashing and verification using BCrypt with cost factor 12.
/// </summary>
public interface IPasswordHasher
{
    /// <summary>
    /// Hashes a plaintext password using BCrypt with cost factor 12.
    /// </summary>
    /// <param name="password">The plaintext password to hash.</param>
    /// <returns>The BCrypt hash string.</returns>
    string HashPassword(string password);

    /// <summary>
    /// Verifies a plaintext password against a stored BCrypt hash.
    /// </summary>
    /// <param name="password">The plaintext password to verify.</param>
    /// <param name="hash">The stored BCrypt hash.</param>
    /// <returns>True if the password matches the hash; otherwise false.</returns>
    bool VerifyPassword(string password, string hash);
}

/// <summary>
/// BCrypt-based password hasher with cost factor 12.
/// </summary>
public sealed class PasswordHasher : IPasswordHasher
{
    private const int WorkFactor = 12;

    /// <inheritdoc />
    public string HashPassword(string password)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(password);
        return BCrypt.Net.BCrypt.HashPassword(password, WorkFactor);
    }

    /// <inheritdoc />
    public bool VerifyPassword(string password, string hash)
    {
        if (string.IsNullOrWhiteSpace(password) || string.IsNullOrWhiteSpace(hash))
            return false;

        try
        {
            return BCrypt.Net.BCrypt.Verify(password, hash);
        }
        catch
        {
            // If the hash is malformed, verification fails
            return false;
        }
    }
}
