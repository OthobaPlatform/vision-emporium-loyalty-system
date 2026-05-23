using System.Globalization;

namespace VELoyalty.Core.Validation;

/// <summary>
/// Represents a raw transaction record before validation.
/// </summary>
/// <param name="CustomerId">Customer identifier field.</param>
/// <param name="CustomerPhone">Customer phone number field.</param>
/// <param name="OutletId">Outlet identifier field.</param>
/// <param name="PurchaseDate">Purchase date field (string to be parsed).</param>
/// <param name="PurchaseAmount">Purchase amount field (string to be parsed).</param>
public record RawTransaction(
    string? CustomerId,
    string? CustomerPhone,
    string? OutletId,
    string? PurchaseDate,
    string? PurchaseAmount
);

/// <summary>
/// Validates transaction records for required field presence, amount range, and date parsing.
/// </summary>
public static class TransactionValidator
{
    /// <summary>
    /// Validates a raw transaction record.
    /// A record is valid if and only if all required fields are present (non-null, non-whitespace),
    /// the purchase amount is numeric and within 0.01–999,999,999.99,
    /// and the purchase date is parseable.
    /// </summary>
    /// <param name="transaction">The raw transaction to validate.</param>
    /// <returns>A ValidationResult indicating success or failure with specific error messages.</returns>
    public static ValidationResult Validate(RawTransaction transaction)
    {
        var errors = new List<string>();

        // Check required fields
        if (string.IsNullOrWhiteSpace(transaction.CustomerId))
            errors.Add("Customer identifier is required.");

        if (string.IsNullOrWhiteSpace(transaction.CustomerPhone))
            errors.Add("Customer phone number is required.");

        if (string.IsNullOrWhiteSpace(transaction.OutletId))
            errors.Add("Outlet identifier is required.");

        if (string.IsNullOrWhiteSpace(transaction.PurchaseDate))
            errors.Add("Purchase date is required.");

        if (string.IsNullOrWhiteSpace(transaction.PurchaseAmount))
            errors.Add("Purchase amount is required.");

        // If any required field is missing, return early with those errors
        if (errors.Count > 0)
            return ValidationResult.Failure(errors);

        // Validate purchase amount is numeric and within range
        if (!decimal.TryParse(transaction.PurchaseAmount, NumberStyles.Number, CultureInfo.InvariantCulture, out var amount))
        {
            errors.Add("Purchase amount must be a valid numeric value.");
        }
        else
        {
            if (amount < Constants.MinPurchaseAmount)
                errors.Add($"Purchase amount must be at least {Constants.MinPurchaseAmount}.");

            if (amount > Constants.MaxPurchaseAmount)
                errors.Add($"Purchase amount must not exceed {Constants.MaxPurchaseAmount}.");
        }

        // Validate purchase date is parseable
        if (!TryParseDate(transaction.PurchaseDate!))
        {
            errors.Add("Purchase date is not a valid date format.");
        }

        return errors.Count == 0
            ? ValidationResult.Success()
            : ValidationResult.Failure(errors);
    }

    /// <summary>
    /// Attempts to parse a date string. Supports ISO 8601 and common date formats.
    /// </summary>
    private static bool TryParseDate(string dateString)
    {
        return DateOnly.TryParse(dateString, CultureInfo.InvariantCulture, DateTimeStyles.None, out _)
            || DateTime.TryParse(dateString, CultureInfo.InvariantCulture, DateTimeStyles.None, out _);
    }
}
