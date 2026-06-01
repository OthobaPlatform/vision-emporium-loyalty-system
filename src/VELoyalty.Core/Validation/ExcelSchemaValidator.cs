using System.Globalization;
using System.Text.RegularExpressions;

namespace VELoyalty.Core.Validation;

/// <summary>
/// Validates Excel/CSV file schema for Vision Emporium sales data.
/// Supports the real format with columns: DIST_ID, DIST_NAME, ITEM_ID, ITEM_NAME,
/// OC_QTY, SR_QNTY, AMNT, CHALLAN_DATE, CHALLAN_NO, COMMP, NET_AMNT, NOTE.
/// </summary>
public static class ExcelSchemaValidator
{
    /// <summary>
    /// Required column names for the Vision Emporium CSV/Excel import schema.
    /// </summary>
    public static readonly IReadOnlyList<string> RequiredColumns = new[]
    {
        "DIST_ID",
        "DIST_NAME",
        "ITEM_ID",
        "ITEM_NAME",
        "OC_QTY",
        "AMNT",
        "CHALLAN_DATE",
        "CHALLAN_NO",
        "NET_AMNT",
        "NOTE"
    };

    /// <summary>
    /// Regex to extract customer name from NOTE field.
    /// Matches: "Name: {name} Mb No:" or "Credit Staff Id: {id} Name: {name}"
    /// </summary>
    private static readonly Regex NameRegex = new(
        @"Name:\s*(.+?)\s*(?:Mb No:|$)",
        RegexOptions.Compiled | RegexOptions.IgnoreCase);

    /// <summary>
    /// Regex to extract phone number from NOTE field.
    /// Matches: "Mb No: 01xxxxxxxxx"
    /// </summary>
    private static readonly Regex PhoneRegex = new(
        @"Mb No:\s*(0\d{10})",
        RegexOptions.Compiled | RegexOptions.IgnoreCase);

    /// <summary>
    /// Regex to extract staff ID from NOTE field.
    /// Matches: "Credit Staff Id: {id}"
    /// </summary>
    private static readonly Regex StaffIdRegex = new(
        @"Credit Staff Id:\s*(\d+)",
        RegexOptions.Compiled | RegexOptions.IgnoreCase);

    /// <summary>
    /// Validates that all required columns are present in the provided column headers.
    /// Column matching is case-insensitive.
    /// </summary>
    public static ValidationResult ValidateColumns(IReadOnlyList<string> columnHeaders)
    {
        if (columnHeaders == null || columnHeaders.Count == 0)
            return ValidationResult.Failure("No columns found in the file.");

        var normalizedHeaders = columnHeaders
            .Select(h => h.Trim().ToUpperInvariant())
            .ToHashSet();

        var missingColumns = RequiredColumns
            .Where(required => !normalizedHeaders.Contains(required.ToUpperInvariant()))
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
    /// Validates a single row of data against the Vision Emporium schema constraints.
    /// </summary>
    public static ValidationResult ValidateRow(IReadOnlyDictionary<string, string?> row, int rowNumber)
    {
        var errors = new List<string>();

        // DIST_ID: required numeric
        var distId = GetValue(row, "DIST_ID");
        if (string.IsNullOrWhiteSpace(distId))
            errors.Add($"Row {rowNumber}: DIST_ID (outlet identifier) is required.");

        // ITEM_ID: required
        var itemId = GetValue(row, "ITEM_ID");
        if (string.IsNullOrWhiteSpace(itemId))
            errors.Add($"Row {rowNumber}: ITEM_ID is required.");

        // ITEM_NAME: required
        var itemName = GetValue(row, "ITEM_NAME");
        if (string.IsNullOrWhiteSpace(itemName))
            errors.Add($"Row {rowNumber}: ITEM_NAME is required.");

        // CHALLAN_DATE: required, parseable date (dd/MM/yyyy or dd/MM/yyyy hh:mm:ss tt)
        var dateStr = GetValue(row, "CHALLAN_DATE");
        if (string.IsNullOrWhiteSpace(dateStr))
        {
            errors.Add($"Row {rowNumber}: CHALLAN_DATE is required.");
        }
        else if (!TryParseChallanDate(dateStr, out _))
        {
            errors.Add($"Row {rowNumber}: CHALLAN_DATE '{dateStr}' is not a valid date format (expected dd/MM/yyyy).");
        }

        // CHALLAN_NO: required
        var challanNo = GetValue(row, "CHALLAN_NO");
        if (string.IsNullOrWhiteSpace(challanNo))
            errors.Add($"Row {rowNumber}: CHALLAN_NO is required.");

        // NET_AMNT: required numeric (can be 0 for freebies)
        var netAmntStr = GetValue(row, "NET_AMNT");
        if (string.IsNullOrWhiteSpace(netAmntStr))
        {
            errors.Add($"Row {rowNumber}: NET_AMNT is required.");
        }
        else if (!decimal.TryParse(netAmntStr, NumberStyles.Number, CultureInfo.InvariantCulture, out var netAmnt))
        {
            errors.Add($"Row {rowNumber}: NET_AMNT '{netAmntStr}' is not a valid numeric value.");
        }
        else if (netAmnt < 0)
        {
            errors.Add($"Row {rowNumber}: NET_AMNT cannot be negative.");
        }

        // NOTE: must contain either a phone number or staff ID for customer identification
        var note = GetValue(row, "NOTE");
        if (string.IsNullOrWhiteSpace(note))
        {
            errors.Add($"Row {rowNumber}: NOTE is required (must contain customer phone or staff ID).");
        }
        else
        {
            var phone = ExtractPhoneNumber(note);
            var staffId = ExtractStaffId(note);
            if (string.IsNullOrWhiteSpace(phone) && string.IsNullOrWhiteSpace(staffId))
            {
                errors.Add($"Row {rowNumber}: NOTE must contain a phone number (Mb No:) or staff ID (Credit Staff Id:).");
            }
        }

        return errors.Count == 0
            ? ValidationResult.Success()
            : ValidationResult.Failure(errors);
    }

    /// <summary>
    /// Extracts customer phone number from the NOTE field and normalizes to E.164 (+880).
    /// </summary>
    public static string? ExtractPhoneNumber(string? note)
    {
        if (string.IsNullOrWhiteSpace(note)) return null;

        var match = PhoneRegex.Match(note);
        if (!match.Success) return null;

        var localNumber = match.Groups[1].Value.Trim();
        if (string.IsNullOrWhiteSpace(localNumber)) return null;

        // Normalize: 01xxxxxxxxx → +8801xxxxxxxxx
        return $"+88{localNumber}";
    }

    /// <summary>
    /// Extracts customer name from the NOTE field.
    /// </summary>
    public static string? ExtractCustomerName(string? note)
    {
        if (string.IsNullOrWhiteSpace(note)) return null;

        var match = NameRegex.Match(note);
        if (!match.Success) return null;

        var name = match.Groups[1].Value.Trim();
        return string.IsNullOrWhiteSpace(name) ? null : name;
    }

    /// <summary>
    /// Extracts staff ID from the NOTE field (for credit/staff purchases).
    /// </summary>
    public static string? ExtractStaffId(string? note)
    {
        if (string.IsNullOrWhiteSpace(note)) return null;

        var match = StaffIdRegex.Match(note);
        return match.Success ? match.Groups[1].Value.Trim() : null;
    }

    /// <summary>
    /// Converts amount from thousands BDT (as in CSV) to actual BDT.
    /// E.g., 0.2650 → 265.0, 56.20 → 56200.0
    /// </summary>
    public static decimal ConvertFromThousands(decimal amountInThousands)
    {
        return amountInThousands * 1000m;
    }

    /// <summary>
    /// Tries to parse the CHALLAN_DATE field (format: dd/MM/yyyy or dd/MM/yyyy hh:mm:ss tt).
    /// </summary>
    public static bool TryParseChallanDate(string dateStr, out DateOnly result)
    {
        result = default;

        // Try dd/MM/yyyy HH:mm:ss tt
        if (DateTime.TryParseExact(dateStr.Trim(), 
            new[] { "dd/MM/yyyy hh:mm:ss tt", "dd/MM/yyyy HH:mm:ss", "dd/MM/yyyy", "d/M/yyyy hh:mm:ss tt", "d/M/yyyy" },
            CultureInfo.InvariantCulture, DateTimeStyles.None, out var dt))
        {
            result = DateOnly.FromDateTime(dt);
            return true;
        }

        // Fallback: try general DateTime parsing
        if (DateTime.TryParse(dateStr.Trim(), CultureInfo.InvariantCulture, DateTimeStyles.None, out var dtFallback))
        {
            result = DateOnly.FromDateTime(dtFallback);
            return true;
        }

        return false;
    }

    private static string? GetValue(IReadOnlyDictionary<string, string?> row, string columnName)
    {
        // Try exact match first, then case-insensitive
        if (row.TryGetValue(columnName, out var value)) return value;
        var key = row.Keys.FirstOrDefault(k => k.Equals(columnName, StringComparison.OrdinalIgnoreCase));
        return key != null ? row[key] : null;
    }
}
