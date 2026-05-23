using System.Text.Json.Serialization;

namespace VELoyalty.NotificationHandler.Models;

/// <summary>
/// Represents a notification event to be processed by the Notification Lambda.
/// </summary>
public class NotificationEvent
{
    /// <summary>
    /// Type of notification: "Eligibility" or "Reminder".
    /// </summary>
    [JsonPropertyName("type")]
    public string Type { get; set; } = string.Empty;

    /// <summary>
    /// Customer identifier.
    /// </summary>
    [JsonPropertyName("customerId")]
    public string CustomerId { get; set; } = string.Empty;

    /// <summary>
    /// Customer name for SMS personalization.
    /// </summary>
    [JsonPropertyName("customerName")]
    public string CustomerName { get; set; } = string.Empty;

    /// <summary>
    /// Customer phone number in E.164 format.
    /// </summary>
    [JsonPropertyName("phoneNumber")]
    public string PhoneNumber { get; set; } = string.Empty;

    /// <summary>
    /// The 6-digit verification code.
    /// </summary>
    [JsonPropertyName("verificationCode")]
    public string VerificationCode { get; set; } = string.Empty;

    /// <summary>
    /// Description of the gift.
    /// </summary>
    [JsonPropertyName("giftDescription")]
    public string GiftDescription { get; set; } = string.Empty;

    /// <summary>
    /// Name of the designated outlet.
    /// </summary>
    [JsonPropertyName("outletName")]
    public string OutletName { get; set; } = string.Empty;

    /// <summary>
    /// Expiry date of the verification code (ISO 8601 date format).
    /// Used for reminder notifications.
    /// </summary>
    [JsonPropertyName("expiryDate")]
    public string? ExpiryDate { get; set; }

    /// <summary>
    /// Current attempt number (1-based). Used for retry tracking.
    /// </summary>
    [JsonPropertyName("attemptNumber")]
    public int AttemptNumber { get; set; } = 1;
}

/// <summary>
/// Represents a reminder check event triggered by EventBridge.
/// </summary>
public class ReminderCheckEvent
{
    /// <summary>
    /// Type of event: always "ReminderCheck".
    /// </summary>
    [JsonPropertyName("type")]
    public string Type { get; set; } = "ReminderCheck";
}
