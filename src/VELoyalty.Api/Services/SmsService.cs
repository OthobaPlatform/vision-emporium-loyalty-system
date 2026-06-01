using Microsoft.Extensions.Logging;
using VELoyalty.Notifications;

namespace VELoyalty.Api.Services;

/// <summary>
/// High-level SMS service that composes and sends loyalty notification messages.
/// Falls back to console logging when no SMS gateway is configured.
/// </summary>
public class SmsService
{
    private readonly ISmsGatewayClient _smsClient;
    private readonly IConfiguration _configuration;
    private readonly ILogger<SmsService> _logger;

    public SmsService(
        ISmsGatewayClient smsClient,
        IConfiguration configuration,
        ILogger<SmsService> logger)
    {
        _smsClient = smsClient;
        _configuration = configuration;
        _logger = logger;
    }

    /// <summary>
    /// Sends an SMS when a customer reaches a purchase threshold and earns a gift.
    /// </summary>
    public async Task SendThresholdReachedSmsAsync(
        string phone,
        string customerName,
        string giftDescription,
        string otpCode,
        string outletName,
        CancellationToken ct = default)
    {
        var message = $"Dear {customerName}, congratulations! You've earned a gift from Vision Emporium! " +
                      $"Gift: {giftDescription}. Your verification code: {otpCode}. " +
                      $"Please visit {outletName} to claim. Code valid for 30 days.";

        await SendAsync(phone, message, "ThresholdReached", ct);
    }

    /// <summary>
    /// Sends a progress update SMS when a customer is 1 purchase away from the next threshold.
    /// </summary>
    public async Task SendProgressUpdateSmsAsync(
        string phone,
        string customerName,
        int currentPurchases,
        int nextThreshold,
        string giftDescription,
        CancellationToken ct = default)
    {
        var remaining = nextThreshold - currentPurchases;
        var message = $"Dear {customerName}, you're just {remaining} purchase(s) away from your next Vision Emporium reward! " +
                      $"Keep shopping to earn: {giftDescription}.";

        await SendAsync(phone, message, "ProgressUpdate", ct);
    }

    /// <summary>
    /// Sends a confirmation SMS after a gift has been successfully redeemed.
    /// </summary>
    public async Task SendRedemptionConfirmationSmsAsync(
        string phone,
        string customerName,
        string giftDescription,
        CancellationToken ct = default)
    {
        var message = $"Dear {customerName}, your gift ({giftDescription}) has been successfully redeemed at Vision Emporium. " +
                      $"Thank you for your loyalty!";

        await SendAsync(phone, message, "RedemptionConfirmation", ct);
    }

    /// <summary>
    /// Determines whether to use the real SMS gateway or fake/console mode, then sends.
    /// </summary>
    private async Task SendAsync(string phone, string message, string messageType, CancellationToken ct)
    {
        if (string.IsNullOrWhiteSpace(phone))
        {
            _logger.LogWarning("Cannot send {MessageType} SMS: no phone number provided", messageType);
            return;
        }

        var smsEnabled = _configuration.GetValue<bool>("Sms:Enabled");

        if (!smsEnabled)
        {
            // Fake mode: log to console
            _logger.LogInformation("[FAKE SMS] To: {Phone} | Type: {MessageType} | Message: {Message}",
                phone, messageType, message);
            return;
        }

        var result = await _smsClient.SendSmsAsync(phone, message, ct);
        if (result.IsSuccess)
        {
            _logger.LogInformation("SMS sent to {Phone} ({MessageType}). MessageId: {MessageId}",
                phone, messageType, result.MessageId);
        }
        else
        {
            _logger.LogWarning("Failed to send SMS to {Phone} ({MessageType}): {Error}",
                phone, messageType, result.ErrorMessage);
        }
    }
}
