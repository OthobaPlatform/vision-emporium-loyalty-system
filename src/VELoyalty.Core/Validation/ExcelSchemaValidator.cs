using System.Globalization;

namespace VELoyalty.Core.Validation;

/// <summary>
/// Validates Excel file schema: required column presence and type constraints per column.
/// </summary>
public static class ExcelSchemaValidator
{
    /// <summary>
    /// Required column names for the Excel import schema.
    /// </summary>
    public static readonly IReadOnlyList<string> RequiredColumns = new[]
    {
        "customer identifier",
        "customer name",
        "customer phone number",
        "outlet identifier",
        "purchase date",
        "purchase amount",
        "product category"
    };

    /// <summary>
    /// Validates that all required columns are present in the provided column headers.
    /// Column matching is case-insensitive.
    /// </summary>
    /// <param name="columnHeaders">The column headers from the Excel file.</param>
    /// <returns>A ValidationResult indicating success or failure with missing column details.</returns>
    public static ValidationResult ValidateColumns(IReadOnlyList<string> columnHeaders)
    {
        if (columnHeaders == null || columnHeaders.Count == 0)
            return ValidationResult.Failure("No columns found in the file.");

        var normalizedHeaders = columnHeaders
            .Select(h => h.Trim().ToLowerInvariant())
            .ToHashSet();

        var missingColumns = RequiredColumns
            .Where(required => !normalizedHeaders.Contains(required))
            .ToList();

        if (missingColumns.Count > 0)
        {
            var errors = missingColumns
                .Select(col => $"Required column '{col}' is missing.")
                .ToList();
            return ValidationResult.Failure(errors);
        }

        return ValidationResult.Success();
    }

    /// <summary>
    /// Validates a single row of data against the expected type constraints.
    /// </summary>
    /// <param name="row">Dictionary mapping column name (lowercase) to cell value.</param>
    /// <param name="rowNumber">1-based row number for error reporting.</param>
    /// <returns>A ValidationResult indicating success or failure with specific type constraint violations.</returns>
    public static ValidationResult ValidateRow(IReadOnlyDictionary<string, string?> row, int rowNumber)
    {
        var errors = new List<string>();

        // Customer identifier: non-empty string
        ValidateNonEmpty(row, "customer identifier", rowNumber, errors);

        // Customer name: non-empty string, max 200 characters
        var customerName = GetValue(row, "customer name");
        if (string.IsNullOrWhiteSpace(customerName))
        {
            errors.Add($"Row {rowNumber}: Customer name is required.");
        }
        else if (customerName.Length > Constants.MaxCustomerNameLength)
        {
            errors.Add($"Row {rowNumber}: Customer name must not exceed {Constants.MaxCustomerNameLength} characters.");
        }

        // Customer phone number: Valid_Phone_Number format
        var phone = GetValue(row, "customer phone number");
        if (string.IsNullOrWhiteSpace(phone))
        {
            errors.Add($"Row {rowNumber}: Customer phone number is required.");
        }
        else
        {
            var phoneResult = PhoneNumberValidator.Validate(phone);
            if (!phoneResult.IsValid)
            {
                errors.Add($"Row {rowNumber}: Customer phone number is invalid - {phoneResult.Errors[0]}");
            }
        }

        // Outlet identifier: non-empty string
        ValidateNonEmpty(row, "outlet identifier", rowNumber, errors);

        // Purchase date: ISO 8601 date format, not in the future
        var dateStr = GetValue(row, "purchase date");
        if (string.IsNullOrWhiteSpace(dateStr))
        {
            errors.Add($"Row {rowNumber}: Purchase date is required.");
        }
        else if (!DateOnly.TryParse(dateStr, CultureInfo.InvariantCulture, DateTimeStyles.None, out var parsedDate)
              && !DateTime.TryParse(dateStr, CultureInfo.InvariantCulture, DateTimeStyles.None, out _))
        {
            errors.Add($"Row {rowNumber}: Purchase date '{dateStr}' is not a valid date format.");
        }
        else
        {
            // Check if date is in the future (using system timezone)
            var today = DateOnly.FromDateTime(TimeZoneInfo.ConvertTimeFromUtc(DateTime.UtcNow, Constants.SystemTimeZone));
            if (DateOnly.TryParse(dateStr, CultureInfo.InvariantCulture, DateTimeStyles.None, out var dateOnly) && dateOnly > today)
            {
                errors.Add($"Row {rowNumber}: Purchase date cannot be in the future.");
            }
        }

        // Purchase amount: numeric value between 0.01 and 999,999,999.99
        var amountStr = GetValue(row, "purchase amount");
        if (string.IsNullOrWhiteSpace(amountStr))
        {
            errors.Add($"Row {rowNumber}: Purchase amount is required.");
        }
        else if (!decimal.TryParse(amountStr, NumberStyles.Number, CultureInfo.InvariantCulture, out var amount))
        {
            errors.Add($"Row {rowNumber}: Purchase amount '{amountStr}' is not a valid numeric value.");
        }
        else
        {
            if (amount < Constants.MinPurchaseAmount)
                errors.Add($"Row {rowNumber}: Purchase amount must be at least {Constants.MinPurchaseAmount}.");
            if (amount > Constants.MaxPurchaseAmount)
                errors.Add($"Row {rowNumber}: Purchase amount must not exceed {Constants.MaxPurchaseAmount}.");
        }

        // Product category: non-empty string
        ValidateNonEmpty(row, "product category", rowNumber, errors);

        return errors.Count == 0
            ? ValidationResult.Success()
            : ValidationResult.Failure(errors);
    }

    private static void ValidateNonEmpty(
        IReadOnlyDictionary<string, string?> row,
        string columnName,
        int rowNumber,
        List<string> errors)
    {
        var value = GetValue(row, columnName);
        if (string.IsNullOrWhiteSpace(value))
        {
            var displayName = char.ToUpper(columnName[0]) + columnName[1..];
            errors.Add($"Row {rowNumber}: {displayName} is required.");
        }
    }

    private static string? GetValue(IReadOnlyDictionary<string, string?> row, string columnName)
    {
        return row.TryGetValue(columnName, out var value) ? value : null;
    }
}
