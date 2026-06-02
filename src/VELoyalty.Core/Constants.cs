namespace VELoyalty.Core;

/// <summary>
/// System-wide constants for the Vision Emporium Loyalty System.
/// </summary>
public static class Constants
{
    /// <summary>
    /// System currency: Bangladeshi Taka (ISO 4217).
    /// All monetary amounts are stored as decimals with two fractional digits.
    /// </summary>
    public const string SystemCurrency = "BDT";

    /// <summary>
    /// System time zone identifier: Asia/Dhaka (UTC+06:00).
    /// All persisted timestamps are stored in UTC and presented in this time zone in the UI.
    /// </summary>
    public const string SystemTimeZoneId = "Asia/Dhaka";

    /// <summary>
    /// Default country code for phone numbers: Bangladesh (+880).
    /// Applied when no country code is supplied; numbers are normalized to E.164 before persistence.
    /// </summary>
    public const string DefaultCountryCode = "+880";

    /// <summary>
    /// Gets the system TimeZoneInfo instance for Asia/Dhaka.
    /// </summary>
    public static readonly TimeZoneInfo SystemTimeZone =
        TimeZoneInfo.FindSystemTimeZoneById(SystemTimeZoneId);

    /// <summary>
    /// The loyalty cycle always runs from June 1 to May 31 of the next year.
    /// This method computes the current cycle ID based on today's date.
    /// E.g., if today is between June 1, 2025 and May 31, 2026 → "2025-2026"
    /// </summary>
    public static string GetCurrentCycleId()
    {
        var today = DateOnly.FromDateTime(TimeZoneInfo.ConvertTimeFromUtc(DateTime.UtcNow, SystemTimeZone));
        return GetCycleIdForDate(today);
    }

    /// <summary>
    /// Gets the cycle ID for a given date.
    /// Cycle runs June 1 → May 31. If date is Jan-May, it belongs to the previous year's cycle.
    /// </summary>
    public static string GetCycleIdForDate(DateOnly date)
    {
        var cycleStartYear = date.Month >= 6 ? date.Year : date.Year - 1;
        return $"{cycleStartYear}-{cycleStartYear + 1}";
    }

    /// <summary>
    /// Gets the start date of the current loyalty cycle (June 1).
    /// </summary>
    public static DateOnly GetCurrentCycleStartDate()
    {
        var today = DateOnly.FromDateTime(TimeZoneInfo.ConvertTimeFromUtc(DateTime.UtcNow, SystemTimeZone));
        var startYear = today.Month >= 6 ? today.Year : today.Year - 1;
        return new DateOnly(startYear, 6, 1);
    }

    /// <summary>
    /// Gets the end date of the current loyalty cycle (May 31).
    /// </summary>
    public static DateOnly GetCurrentCycleEndDate()
    {
        var today = DateOnly.FromDateTime(TimeZoneInfo.ConvertTimeFromUtc(DateTime.UtcNow, SystemTimeZone));
        var endYear = today.Month >= 6 ? today.Year + 1 : today.Year;
        return new DateOnly(endYear, 5, 31);
    }

    /// <summary>
    /// Minimum purchase amount allowed (BDT).
    /// </summary>
    public const decimal MinPurchaseAmount = 0.01m;

    /// <summary>
    /// Maximum purchase amount allowed (BDT).
    /// </summary>
    public const decimal MaxPurchaseAmount = 999_999_999.99m;

    /// <summary>
    /// Minimum gift value allowed (BDT).
    /// </summary>
    public const decimal MinGiftValue = 0.01m;

    /// <summary>
    /// Maximum gift value allowed (BDT).
    /// </summary>
    public const decimal MaxGiftValue = 999_999.99m;

    /// <summary>
    /// Minimum loyalty cycle duration in days.
    /// </summary>
    public const int MinCycleDurationDays = 30;

    /// <summary>
    /// Maximum loyalty cycle duration in days.
    /// </summary>
    public const int MaxCycleDurationDays = 730;

    /// <summary>
    /// Minimum number of purchase thresholds that can be configured.
    /// </summary>
    public const int MinThresholdCount = 1;

    /// <summary>
    /// Maximum number of purchase thresholds that can be configured.
    /// </summary>
    public const int MaxThresholdCount = 10;

    /// <summary>
    /// Minimum purchase threshold value (purchase count).
    /// </summary>
    public const int MinThresholdValue = 1;

    /// <summary>
    /// Maximum purchase threshold value (purchase count).
    /// </summary>
    public const int MaxThresholdValue = 100;

    /// <summary>
    /// Minimum sync interval in minutes.
    /// </summary>
    public const int MinSyncIntervalMinutes = 15;

    /// <summary>
    /// Default sync interval in minutes.
    /// </summary>
    public const int DefaultSyncIntervalMinutes = 60;

    /// <summary>
    /// Default verification code expiry in days.
    /// </summary>
    public const int DefaultCodeExpiryDays = 30;

    /// <summary>
    /// Minimum verification code expiry in days.
    /// </summary>
    public const int MinCodeExpiryDays = 7;

    /// <summary>
    /// Maximum verification code expiry in days.
    /// </summary>
    public const int MaxCodeExpiryDays = 90;

    /// <summary>
    /// Number of days before expiry to send a reminder SMS.
    /// </summary>
    public const int ReminderDaysBeforeExpiry = 7;

    /// <summary>
    /// Maximum failed redemption attempts before rate limiting kicks in.
    /// </summary>
    public const int MaxRedemptionAttempts = 5;

    /// <summary>
    /// Rate limit window in minutes for failed redemption attempts.
    /// </summary>
    public const int RateLimitWindowMinutes = 15;

    /// <summary>
    /// Rate limit block duration in minutes after exceeding max attempts.
    /// </summary>
    public const int RateLimitBlockMinutes = 30;

    /// <summary>
    /// Default JWT token expiry in hours.
    /// </summary>
    public const int DefaultTokenExpiryHours = 8;

    /// <summary>
    /// Maximum Excel file size in bytes (10 MB).
    /// </summary>
    public const long MaxExcelFileSizeBytes = 10 * 1024 * 1024;

    /// <summary>
    /// Maximum number of rows allowed in an Excel import file.
    /// </summary>
    public const int MaxExcelRowCount = 100_000;

    /// <summary>
    /// Maximum length for gift description.
    /// </summary>
    public const int MaxGiftDescriptionLength = 200;

    /// <summary>
    /// Maximum length for customer name.
    /// </summary>
    public const int MaxCustomerNameLength = 200;

    /// <summary>
    /// Verification code length (6-digit numeric).
    /// </summary>
    public const int VerificationCodeLength = 6;

    /// <summary>
    /// Maximum SMS notification retry attempts.
    /// </summary>
    public const int MaxSmsRetryAttempts = 3;

    /// <summary>
    /// Interval between SMS retry attempts in hours.
    /// </summary>
    public const int SmsRetryIntervalHours = 1;

    /// <summary>
    /// Maximum API sync retry attempts.
    /// </summary>
    public const int MaxApiSyncRetryAttempts = 3;

    /// <summary>
    /// API sync timeout in seconds.
    /// </summary>
    public const int ApiSyncTimeoutSeconds = 30;
}
