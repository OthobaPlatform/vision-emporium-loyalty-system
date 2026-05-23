namespace VELoyalty.Notifications;

/// <summary>
/// Abstraction for sending SMS messages via a third-party gateway.
/// </summary>
public interface ISmsGatewayClient
{
    /// <summary>
    /// Sends an SMS message to the specified phone number.
    /// </summary>
    /// <param name="phoneNumber">Recipient phone number in E.164 format.</param>
    /// <param name="message">The SMS message content.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>A result indicating whether the SMS was sent successfully.</returns>
    Task<SmsDeliveryResult> SendSmsAsync(string phoneNumber, string message, CancellationToken cancellationToken = default);
}

/// <summary>
/// Represents the result of an SMS delivery attempt.
/// </summary>
/// <param name="IsSuccess">Whether the SMS was sent successfully.</param>
/// <param name="MessageId">Gateway-assigned message identifier, if available.</param>
/// <param name="ErrorMessage">Error description if the delivery failed.</param>
public record SmsDeliveryResult(bool IsSuccess, string? MessageId, string? ErrorMessage)
{
    /// <summary>
    /// Creates a successful delivery result.
    /// </summary>
    public static SmsDeliveryResult Success(string? messageId = null) =>
        new(true, messageId, null);

    /// <summary>
    /// Creates a failed delivery result.
    /// </summary>
    public static SmsDeliveryResult Failure(string errorMessage) =>
        new(false, null, errorMessage);
}
