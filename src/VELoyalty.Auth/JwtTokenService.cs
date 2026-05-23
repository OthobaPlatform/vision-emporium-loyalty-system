using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using System.Text.Json.Serialization;
using VELoyalty.Core;

namespace VELoyalty.Auth;

/// <summary>
/// Generates and validates signed JWT tokens for authenticated users.
/// </summary>
public interface IJwtTokenService
{
    /// <summary>
    /// Generates a signed JWT token for the specified user.
    /// </summary>
    /// <param name="userId">The user's unique identifier (becomes the 'sub' claim).</param>
    /// <param name="role">The user's role (Admin or Outlet_Manager).</param>
    /// <param name="outletId">The assigned outlet ID (included only for Outlet_Manager).</param>
    /// <returns>An AuthToken containing the signed JWT and its expiration time.</returns>
    AuthToken GenerateToken(string userId, string role, string? outletId);

    /// <summary>
    /// Validates a JWT token by verifying its HMAC-SHA256 signature and checking expiry.
    /// Returns a result containing the extracted claims if valid.
    /// </summary>
    TokenValidationResult ValidateToken(string token);
}

/// <summary>
/// Result of JWT token validation containing extracted claims.
/// </summary>
public record TokenValidationResult(
    bool IsValid,
    string? UserId = null,
    string? Role = null,
    string? OutletId = null,
    string? ErrorMessage = null
);

/// <summary>
/// Source-generated JSON context for AOT-compatible JWT serialization.
/// </summary>
[JsonSerializable(typeof(Dictionary<string, object>))]
[JsonSerializable(typeof(Dictionary<string, JsonElement>))]
[JsonSerializable(typeof(JwtHeaderDto))]
internal partial class JwtSerializerContext : JsonSerializerContext
{
}

internal record JwtHeaderDto
{
    [JsonPropertyName("alg")]
    public string Alg { get; init; } = "HS256";

    [JsonPropertyName("typ")]
    public string Typ { get; init; } = "JWT";
}

/// <summary>
/// Service for generating and validating HMAC-SHA256 signed JWT tokens.
/// Uses manual JWT construction for Native AOT compatibility.
/// </summary>
public sealed class JwtTokenService : IJwtTokenService
{
    private readonly string _secret;
    private readonly int _expiryHours;

    /// <summary>
    /// Creates a new JwtTokenService with the specified secret and default expiry.
    /// </summary>
    /// <param name="secret">The HMAC-SHA256 signing secret.</param>
    public JwtTokenService(string secret) : this(secret, Constants.DefaultTokenExpiryHours)
    {
    }

    /// <summary>
    /// Creates a new JwtTokenService with the specified secret and expiry.
    /// </summary>
    /// <param name="secret">The HMAC-SHA256 signing secret.</param>
    /// <param name="expiryHours">Token expiry in hours (default: 8).</param>
    public JwtTokenService(string secret, int expiryHours)
    {
        if (string.IsNullOrWhiteSpace(secret))
            throw new ArgumentException("JWT secret cannot be null or empty.", nameof(secret));
        _secret = secret;
        _expiryHours = expiryHours;
    }

    /// <inheritdoc />
    public AuthToken GenerateToken(string userId, string role, string? outletId)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(userId);
        ArgumentException.ThrowIfNullOrWhiteSpace(role);

        var now = DateTimeOffset.UtcNow;
        var expires = now.AddHours(_expiryHours);

        var iat = now.ToUnixTimeSeconds();
        var exp = expires.ToUnixTimeSeconds();

        var headerDto = new JwtHeaderDto();
        var headerJson = JsonSerializer.Serialize(headerDto, JwtSerializerContext.Default.JwtHeaderDto);
        var header = Base64UrlEncode(Encoding.UTF8.GetBytes(headerJson));

        // Build payload manually to ensure correct JSON number types for iat/exp
        var payloadBuilder = new StringBuilder();
        payloadBuilder.Append('{');
        payloadBuilder.Append($"\"sub\":\"{EscapeJsonString(userId)}\",");
        payloadBuilder.Append($"\"role\":\"{EscapeJsonString(role)}\",");
        if (role == nameof(UserRole.Outlet_Manager) && !string.IsNullOrWhiteSpace(outletId))
        {
            payloadBuilder.Append($"\"outletId\":\"{EscapeJsonString(outletId)}\",");
        }
        payloadBuilder.Append($"\"iat\":{iat},");
        payloadBuilder.Append($"\"exp\":{exp}");
        payloadBuilder.Append('}');

        var payload = Base64UrlEncode(Encoding.UTF8.GetBytes(payloadBuilder.ToString()));
        var signature = ComputeSignature(header, payload);
        var tokenString = $"{header}.{payload}.{signature}";

        return new AuthToken(tokenString, expires.UtcDateTime);
    }

    /// <inheritdoc />
    public TokenValidationResult ValidateToken(string token)
    {
        if (string.IsNullOrWhiteSpace(token))
        {
            return new TokenValidationResult(false, ErrorMessage: "Token is missing or empty.");
        }

        var parts = token.Split('.');
        if (parts.Length != 3)
        {
            return new TokenValidationResult(false, ErrorMessage: "Token format is invalid.");
        }

        var header = parts[0];
        var payload = parts[1];
        var signature = parts[2];

        // Verify signature
        var expectedSignature = ComputeSignature(header, payload);
        if (!CryptographicEquals(signature, expectedSignature))
        {
            return new TokenValidationResult(false, ErrorMessage: "Token signature is invalid.");
        }

        // Decode payload
        Dictionary<string, JsonElement>? claims;
        try
        {
            var payloadBytes = Base64UrlDecode(payload);
            var payloadJson = Encoding.UTF8.GetString(payloadBytes);
            claims = JsonSerializer.Deserialize(payloadJson, JwtSerializerContext.Default.DictionaryStringJsonElement);
        }
        catch
        {
            return new TokenValidationResult(false, ErrorMessage: "Token payload is malformed.");
        }

        if (claims == null)
        {
            return new TokenValidationResult(false, ErrorMessage: "Token payload is empty.");
        }

        // Check expiry
        if (!claims.TryGetValue("exp", out var expElement))
        {
            return new TokenValidationResult(false, ErrorMessage: "Token is missing expiry claim.");
        }

        long expUnix;
        try
        {
            expUnix = expElement.GetInt64();
        }
        catch
        {
            return new TokenValidationResult(false, ErrorMessage: "Token expiry claim is invalid.");
        }

        var currentTime = DateTimeOffset.UtcNow.ToUnixTimeSeconds();
        if (currentTime >= expUnix)
        {
            return new TokenValidationResult(false, ErrorMessage: "Token has expired.");
        }

        // Extract claims
        var userId = claims.TryGetValue("sub", out var subElement) ? subElement.GetString() : null;
        var userRole = claims.TryGetValue("role", out var roleElement) ? roleElement.GetString() : null;
        var userOutletId = claims.TryGetValue("outletId", out var outletElement) ? outletElement.GetString() : null;

        if (string.IsNullOrEmpty(userId))
        {
            return new TokenValidationResult(false, ErrorMessage: "Token is missing subject (userId) claim.");
        }

        if (string.IsNullOrEmpty(userRole))
        {
            return new TokenValidationResult(false, ErrorMessage: "Token is missing role claim.");
        }

        return new TokenValidationResult(
            IsValid: true,
            UserId: userId,
            Role: userRole,
            OutletId: userOutletId
        );
    }

    private string ComputeSignature(string header, string payload)
    {
        var input = $"{header}.{payload}";
        var keyBytes = Encoding.UTF8.GetBytes(_secret);
        var inputBytes = Encoding.UTF8.GetBytes(input);

        using var hmac = new HMACSHA256(keyBytes);
        var hashBytes = hmac.ComputeHash(inputBytes);
        return Base64UrlEncode(hashBytes);
    }

    private static string Base64UrlEncode(byte[] bytes)
    {
        return Convert.ToBase64String(bytes)
            .TrimEnd('=')
            .Replace('+', '-')
            .Replace('/', '_');
    }

    private static byte[] Base64UrlDecode(string input)
    {
        var base64 = input.Replace('-', '+').Replace('_', '/');
        switch (base64.Length % 4)
        {
            case 2: base64 += "=="; break;
            case 3: base64 += "="; break;
        }
        return Convert.FromBase64String(base64);
    }

    /// <summary>
    /// Constant-time comparison to prevent timing attacks.
    /// </summary>
    private static bool CryptographicEquals(string a, string b)
    {
        var aBytes = Encoding.UTF8.GetBytes(a);
        var bBytes = Encoding.UTF8.GetBytes(b);
        return CryptographicOperations.FixedTimeEquals(aBytes, bBytes);
    }

    private static string EscapeJsonString(string value)
    {
        return value
            .Replace("\\", "\\\\")
            .Replace("\"", "\\\"")
            .Replace("\n", "\\n")
            .Replace("\r", "\\r")
            .Replace("\t", "\\t");
    }
}
