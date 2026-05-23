using System.Text.RegularExpressions;

namespace VELoyalty.Core.Validation;

/// <summary>
/// Validates and normalizes phone numbers to E.164 format.
/// If no country code is supplied, prepends +880 (Bangladesh default).
/// </summary>
public static partial class PhoneNumberValidator
{
    // E.164 format: + followed by 1-15 digits
    [GeneratedRegex(@"^\+[1-9]\d{1,14}$")]
    private static partial Regex E164Pattern();

    // Detects if a number already starts with a + (has country code)
    [GeneratedRegex(@"^\+")]
    private static partial Regex HasCountryCodePattern();

    // Digits only (local number without country code)
    [GeneratedRegex(@"^\d+$")]
    private static partial Regex DigitsOnlyPattern();

    /// <summary>
    /// Validates a phone number and returns the normalized E.164 format.
    /// </summary>
    /// <param name="phoneNumber">The phone number to validate.</param>
    /// <returns>A ValidationResult. If valid, the normalized number is in the first error slot (overloaded for simplicity).</returns>
    public static ValidationResult Validate(string? phoneNumber)
    {
        if (string.IsNullOrWhiteSpace(phoneNumber))
            return ValidationResult.Failure("Phone number is required.");

        var normalized = Normalize(phoneNumber.Trim());

        if (!E164Pattern().IsMatch(normalized))
            return ValidationResult.Failure($"Phone number '{phoneNumber}' does not conform to E.164 format.");

        return ValidationResult.Success();
    }

    /// <summary>
    /// Normalizes a phone number to E.164 format.
    /// If no country code is present, prepends +880 (Bangladesh).
    /// </summary>
    /// <param name="phoneNumber">The phone number to normalize.</param>
    /// <returns>The normalized phone number in E.164 format, or the original if it cannot be normalized.</returns>
    public static string Normalize(string phoneNumber)
    {
        var trimmed = phoneNumber.Trim();

        // Already has a country code with +
        if (HasCountryCodePattern().IsMatch(trimmed))
            return trimmed;

        // Starts with 00 (international dialing prefix) - replace with +
        if (trimmed.StartsWith("00") && trimmed.Length > 2)
            return "+" + trimmed[2..];

        // Local number (digits only) - prepend default country code
        if (DigitsOnlyPattern().IsMatch(trimmed))
        {
            // If starts with 0 (local trunk prefix), remove it before adding country code
            if (trimmed.StartsWith('0'))
                return Constants.DefaultCountryCode + trimmed[1..];

            return Constants.DefaultCountryCode + trimmed;
        }

        // Return as-is if we can't normalize (will fail E.164 validation)
        return trimmed;
    }

    /// <summary>
    /// Validates and normalizes a phone number, returning both the validation result and the normalized number.
    /// </summary>
    /// <param name="phoneNumber">The phone number to validate and normalize.</param>
    /// <returns>A tuple of (ValidationResult, NormalizedNumber). NormalizedNumber is null if validation fails.</returns>
    public static (ValidationResult Result, string? NormalizedNumber) ValidateAndNormalize(string? phoneNumber)
    {
        if (string.IsNullOrWhiteSpace(phoneNumber))
            return (ValidationResult.Failure("Phone number is required."), null);

        var normalized = Normalize(phoneNumber.Trim());

        if (!E164Pattern().IsMatch(normalized))
            return (ValidationResult.Failure($"Phone number '{phoneNumber}' does not conform to E.164 format."), null);

        return (ValidationResult.Success(), normalized);
    }
}
