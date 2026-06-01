using Microsoft.Extensions.Logging;
using VELoyalty.Core;
using VELoyalty.Data.Repositories;
using VELoyalty.Notifications;

namespace VELoyalty.Api.Services;

/// <summary>
/// High-level SMS service that composes and sends loyalty notification messages.
/// Reads SMS configuration from DynamoDB with 5-minute caching.
/// Logs failed notifications for retry capability.
/// </summary>
public class SmsService
{
    private readonly ISmsGatewayClient _smsClient;
    private readonly ConfigRepository _configRepository;
    private readonly NotificationRepository _notificationRepository;
    private readonly ILogger<SmsService> _logger;

    private SmsConfig? _cachedSmsConfig;
    private DateTime _cacheExpiry = DateTime.MinValue;
    private static readonly TimeSpan CacheDuration = TimeSpan.FromMinutes(5);

    public SmsService(
        ISmsGatewayClient smsClient,
        ConfigRepository configRepository,
        NotificationRepository notificationRepository,
        ILogger<SmsService> logger)
    {
        _smsClient = smsClient;
        _configRepository = configRepository;
        _notificationRepository = notificationRepository;
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

        await SendAsync(phone, message, "ThresholdReached", customerName, ct);
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

        await SendAsync(phone, message, "ProgressUpdate", customerName, ct);
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

        await SendAsync(phone, message, "RedemptionConfirmation", customerName, ct);
    }

    /// <summary>
    /// Retries sending a previously failed notification.
    /// </summary>
    public async Task<bool> RetrySendAsync(NotificationLog notification, CancellationToken ct = default)
    {
        var smsConfig = await GetSmsConfigAsync(ct);
        if (smsConfig is null || !smsConfig.Enabled)
        {
            _logger.LogWarning("Cannot retry notification {NotificationId}: SMS is disabled", notification.NotificationId);
            return false;
        }

        var result = await _smsClient.SendSmsAsync(notification.PhoneNumber, notification.Content, ct);

        if (result.IsSuccess)
        {
            _logger.LogInformation("Retry SMS sent to {Phone} ({MessageType}). MessageId: {MessageId}",
                notification.PhoneNumber, notification.MessageType, result.MessageId);

            await _notificationRepository.UpdateNotificationStatusAsync(
                notification, "Sent", null, ct);
            return true;
        }

        _logger.LogWarning("Retry failed for notification {NotificationId}: {Error}",
            notification.NotificationId, result.ErrorMessage);

        await _notificationRepository.UpdateNotificationStatusAsync(
            notification, "Failed", result.ErrorMessage, ct);
        return false;
    }

    /// <summary>
    /// Gets the cached SMS configuration from DynamoDB.
    /// </summary>
    private async Task<SmsConfig?> GetSmsConfigAsync(CancellationToken ct)
    {
        if (_cachedSmsConfig is not null && DateTime.UtcNow < _cacheExpiry)
        {
            return _cachedSmsConfig;
        }

        _cachedSmsConfig = await _configRepository.GetSmsConfigAsync(ct);
        _cacheExpiry = DateTime.UtcNow.Add(CacheDuration);
        return _cachedSmsConfig;
    }

    /// <summary>
    /// Determines whether to use the real SMS gateway or fake/console mode, then sends.
    /// Logs failed notifications to DynamoDB for retry.
    /// </summary>
    private async Task SendAsync(string phone, string message, string messageType, string customerId, CancellationToken ct)
    {
        if (string.IsNullOrWhiteSpace(phone))
        {
            _logger.LogWarning("Cannot send {MessageType} SMS: no phone number provided", messageType);
            return;
        }

        var smsConfig = await GetSmsConfigAsync(ct);
        var smsEnabled = smsConfig?.Enabled ?? false;

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

            // Store failed notification for retry
            var notification = new NotificationLog(
                NotificationId: Guid.NewGuid().ToString(),
                CustomerId: customerId,
                PhoneNumber: phone,
                MessageType: messageType,
                Content: message,
                DeliveryStatus: "Failed",
                FailureReason: result.ErrorMessage,
                AttemptCount: 1,
                SentAt: DateTime.UtcNow
            );

            try
            {
                await _notificationRepository.PutNotificationAsync(notification, ct);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Failed to store notification log for {Phone}", phone);
            }
        }
    }
}
