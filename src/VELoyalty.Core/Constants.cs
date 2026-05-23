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
