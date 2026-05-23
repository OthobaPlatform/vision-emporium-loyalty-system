using Amazon.DynamoDBv2;
using Amazon.DynamoDBv2.Model;
using Amazon.SQS;
using Amazon.SQS.Model;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using System.Text.Json;
using VELoyalty.Core;
using VELoyalty.Core.Validation;
using VELoyalty.Data;
using VELoyalty.Notifications;
using VELoyalty.NotificationHandler.Models;

namespace VELoyalty.NotificationHandler.Services;

/// <summary>
/// Configuration options for the notification handler.
/// </summary>
public class NotificationHandlerOptions
{
    /// <summary>
    /// SQS queue URL for scheduling retry attempts.
    /// </summary>
    public string RetryQueueUrl { get; set; } = string.Empty;

    /// <summary>
    /// DynamoDB table name.
    /// </summary>
    public string TableName { get; set; } = DynamoDbContext.TableName;
}

/// <summary>
/// Core notification processing service that handles eligibility and reminder SMS delivery.
/// </summary>
public class NotificationService
{
    private readonly ISmsGatewayClient _smsClient;
    private readonly IAmazonDynamoDB _dynamoDb;
    private readonly IAmazonSQS _sqsClient;
    private readonly NotificationHandlerOptions _options;
    private readonly ILogger<NotificationService> _logger;

    public NotificationService(
        ISmsGatewayClient smsClient,
        IAmazonDynamoDB dynamoDb,
        IAmazonSQS sqsClient,
        IOptions<NotificationHandlerOptions> options,
        ILogger<NotificationService> logger)
    {
        _smsClient = smsClient ?? throw new ArgumentNullException(nameof(smsClient));
        _dynamoDb = dynamoDb ?? throw new ArgumentNullException(nameof(dynamoDb));
        _sqsClient = sqsClient ?? throw new ArgumentNullException(nameof(sqsClient));
        _options = options?.Value ?? throw new ArgumentNullException(nameof(options));
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));
    }

    /// <summary>
    /// Processes a notification event (eligibility or reminder).
    /// Validates phone number, sends SMS, handles retries, and records notification log.
    /// </summary>
    public async Task ProcessNotificationAsync(NotificationEvent notification, CancellationToken cancellationToken = default)
    {
        _logger.LogInformation(
            "Processing {Type} notification for customer {CustomerId}, attempt {Attempt}",
            notification.Type,
            notification.CustomerId,
            notification.AttemptNumber);

        // Validate phone number before attempting delivery
        var phoneValidation = PhoneNumberValidator.Validate(notification.PhoneNumber);
        if (!phoneValidation.IsValid)
        {
            _logger.LogWarning(
                "Undeliverable notification for customer {CustomerId}: invalid phone number {Phone}",
                notification.CustomerId,
                notification.PhoneNumber);

            await RecordNotificationLogAsync(
                notification,
                deliveryStatus: "Undeliverable",
                failureReason: $"Invalid phone number: {string.Join("; ", phoneValidation.Errors)}",
                cancellationToken: cancellationToken);
            return;
        }

        // Compose the SMS message based on notification type
        var message = ComposeMessage(notification);

        // Attempt to send the SMS
        var result = await _smsClient.SendSmsAsync(notification.PhoneNumber, message, cancellationToken);

        if (result.IsSuccess)
        {
            _logger.LogInformation(
                "SMS sent successfully to {Phone} for customer {CustomerId}. MessageId: {MessageId}",
                notification.PhoneNumber,
                notification.CustomerId,
                result.MessageId);

            await RecordNotificationLogAsync(
                notification,
                deliveryStatus: "Sent",
                messageContent: message,
                cancellationToken: cancellationToken);
        }
        else
        {
            _logger.LogWarning(
                "SMS delivery failed for customer {CustomerId} on attempt {Attempt}: {Error}",
                notification.CustomerId,
                notification.AttemptNumber,
                result.ErrorMessage);

            if (notification.AttemptNumber < Constants.MaxSmsRetryAttempts)
            {
                // Schedule retry via SQS with delay
                await ScheduleRetryAsync(notification, cancellationToken);

                await RecordNotificationLogAsync(
                    notification,
                    deliveryStatus: "Failed",
                    failureReason: result.ErrorMessage,
                    messageContent: message,
                    cancellationToken: cancellationToken);
            }
            else
            {
                // All retries exhausted - mark as permanently failed
                _logger.LogError(
                    "All {MaxAttempts} SMS delivery attempts exhausted for customer {CustomerId}. Marking as permanently failed.",
                    Constants.MaxSmsRetryAttempts,
                    notification.CustomerId);

                await RecordNotificationLogAsync(
                    notification,
                    deliveryStatus: "PermanentlyFailed",
                    failureReason: $"All {Constants.MaxSmsRetryAttempts} attempts exhausted. Last error: {result.ErrorMessage}",
                    messageContent: message,
                    cancellationToken: cancellationToken);
            }
        }
    }

    /// <summary>
    /// Processes a reminder check: queries active verification codes within 7 days of expiration
    /// and sends reminder SMS for codes that haven't already received a reminder.
    /// </summary>
    public async Task ProcessReminderCheckAsync(CancellationToken cancellationToken = default)
    {
        _logger.LogInformation("Processing reminder check for codes expiring within {Days} days", Constants.ReminderDaysBeforeExpiry);

        var now = DateTime.UtcNow;
        var reminderThreshold = now.AddDays(Constants.ReminderDaysBeforeExpiry);

        // Query all active verification codes that expire within the reminder window
        var activeCodes = await QueryActiveCodesExpiringWithinAsync(reminderThreshold, cancellationToken);

        _logger.LogInformation("Found {Count} active codes within reminder window", activeCodes.Count);

        foreach (var code in activeCodes)
        {
            // Check if a reminder has already been sent for this code
            var reminderAlreadySent = await HasReminderBeenSentAsync(code.CustomerId, code.Code, cancellationToken);
            if (reminderAlreadySent)
            {
                _logger.LogDebug("Reminder already sent for code {Code}, skipping", code.Code);
                continue;
            }

            // Look up customer and outlet details
            var customer = await GetCustomerAsync(code.CustomerId, cancellationToken);
            var outlet = await GetOutletAsync(code.OutletId, cancellationToken);

            if (customer == null || outlet == null)
            {
                _logger.LogWarning(
                    "Cannot send reminder for code {Code}: customer or outlet not found",
                    code.Code);
                continue;
            }

            var notification = new NotificationEvent
            {
                Type = "Reminder",
                CustomerId = code.CustomerId,
                CustomerName = customer.Name,
                PhoneNumber = customer.PhoneNumber,
                VerificationCode = code.Code,
                GiftDescription = code.GiftDescription,
                OutletName = outlet.Name,
                ExpiryDate = DateOnly.FromDateTime(code.ExpiresAt).ToString("yyyy-MM-dd"),
                AttemptNumber = 1
            };

            await ProcessNotificationAsync(notification, cancellationToken);
        }
    }

    private string ComposeMessage(NotificationEvent notification)
    {
        return notification.Type switch
        {
            "Eligibility" => NotificationComposer.ComposeEligibilitySms(
                notification.CustomerName,
                notification.GiftDescription,
                notification.OutletName,
                notification.VerificationCode),

            "Reminder" => NotificationComposer.ComposeReminderSms(
                notification.CustomerName,
                notification.VerificationCode,
                notification.GiftDescription,
                notification.OutletName,
                DateOnly.Parse(notification.ExpiryDate ?? DateOnly.FromDateTime(DateTime.UtcNow.AddDays(7)).ToString("yyyy-MM-dd"))),

            _ => throw new ArgumentException($"Unknown notification type: {notification.Type}")
        };
    }

    private async Task ScheduleRetryAsync(NotificationEvent notification, CancellationToken cancellationToken)
    {
        if (string.IsNullOrWhiteSpace(_options.RetryQueueUrl))
        {
            _logger.LogWarning("Retry queue URL not configured. Cannot schedule retry for customer {CustomerId}", notification.CustomerId);
            return;
        }

        var retryEvent = new NotificationEvent
        {
            Type = notification.Type,
            CustomerId = notification.CustomerId,
            CustomerName = notification.CustomerName,
            PhoneNumber = notification.PhoneNumber,
            VerificationCode = notification.VerificationCode,
            GiftDescription = notification.GiftDescription,
            OutletName = notification.OutletName,
            ExpiryDate = notification.ExpiryDate,
            AttemptNumber = notification.AttemptNumber + 1
        };

        // Delay of 1 hour (3600 seconds) between retry attempts
        var delaySeconds = Constants.SmsRetryIntervalHours * 3600;

        var sendRequest = new SendMessageRequest
        {
            QueueUrl = _options.RetryQueueUrl,
            MessageBody = JsonSerializer.Serialize(retryEvent, NotificationJsonContext.Default.NotificationEvent),
            DelaySeconds = Math.Min(delaySeconds, 900) // SQS max delay is 900 seconds (15 min)
        };

        await _sqsClient.SendMessageAsync(sendRequest, cancellationToken);

        _logger.LogInformation(
            "Scheduled retry attempt {Attempt} for customer {CustomerId} with {Delay}s delay",
            retryEvent.AttemptNumber,
            notification.CustomerId,
            delaySeconds);
    }

    private async Task RecordNotificationLogAsync(
        NotificationEvent notification,
        string deliveryStatus,
        string? failureReason = null,
        string? messageContent = null,
        CancellationToken cancellationToken = default)
    {
        var now = DateTime.UtcNow;
        var notificationId = Guid.NewGuid().ToString("N");

        var item = new Dictionary<string, AttributeValue>
        {
            [DynamoDbContext.PkAttribute] = new() { S = KeyBuilder.NotificationPk(notification.CustomerId) },
            [DynamoDbContext.SkAttribute] = new() { S = KeyBuilder.NotificationSk(now, notification.Type) },
            ["NotificationId"] = new() { S = notificationId },
            ["CustomerId"] = new() { S = notification.CustomerId },
            ["PhoneNumber"] = new() { S = notification.PhoneNumber },
            ["MessageType"] = new() { S = notification.Type },
            ["DeliveryStatus"] = new() { S = deliveryStatus },
            ["AttemptCount"] = new() { N = notification.AttemptNumber.ToString() },
            ["SentAt"] = new() { S = now.ToString("O") },
            ["VerificationCode"] = new() { S = notification.VerificationCode }
        };

        if (!string.IsNullOrWhiteSpace(failureReason))
            item["FailureReason"] = new() { S = failureReason };

        if (!string.IsNullOrWhiteSpace(messageContent))
            item["Content"] = new() { S = messageContent };

        var request = new PutItemRequest
        {
            TableName = _options.TableName,
            Item = item
        };

        try
        {
            await _dynamoDb.PutItemAsync(request, cancellationToken);
            _logger.LogDebug("Recorded notification log {NotificationId} with status {Status}", notificationId, deliveryStatus);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to record notification log for customer {CustomerId}", notification.CustomerId);
        }
    }

    private async Task<List<VerificationCodeInfo>> QueryActiveCodesExpiringWithinAsync(
        DateTime expiryThreshold,
        CancellationToken cancellationToken)
    {
        // Scan for active codes - in production, this would use a GSI optimized for this query pattern.
        // For MVP, we scan with a filter for active codes expiring within the window.
        var results = new List<VerificationCodeInfo>();

        var request = new ScanRequest
        {
            TableName = _options.TableName,
            FilterExpression = "begins_with(SK, :skPrefix) AND #status = :active AND ExpiresAt <= :threshold AND ExpiresAt > :now",
            ExpressionAttributeNames = new Dictionary<string, string>
            {
                ["#status"] = "Status"
            },
            ExpressionAttributeValues = new Dictionary<string, AttributeValue>
            {
                [":skPrefix"] = new() { S = "ELIG#" },
                [":active"] = new() { S = "Active" },
                [":threshold"] = new() { S = expiryThreshold.ToString("O") },
                [":now"] = new() { S = DateTime.UtcNow.ToString("O") }
            }
        };

        try
        {
            var response = await _dynamoDb.ScanAsync(request, cancellationToken);

            foreach (var item in response.Items)
            {
                if (item.TryGetValue("VerificationCode", out var code) &&
                    item.TryGetValue("CustomerId", out var customerId) &&
                    item.TryGetValue("OutletId", out var outletId) &&
                    item.TryGetValue("GiftDescription", out var giftDesc) &&
                    item.TryGetValue("ExpiresAt", out var expiresAt))
                {
                    results.Add(new VerificationCodeInfo
                    {
                        Code = code.S,
                        CustomerId = customerId.S,
                        OutletId = outletId.S,
                        GiftDescription = giftDesc.S,
                        ExpiresAt = DateTime.Parse(expiresAt.S)
                    });
                }
            }
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error querying active codes for reminder check");
        }

        return results;
    }

    private async Task<bool> HasReminderBeenSentAsync(string customerId, string code, CancellationToken cancellationToken)
    {
        var request = new QueryRequest
        {
            TableName = _options.TableName,
            KeyConditionExpression = "PK = :pk AND begins_with(SK, :skPrefix)",
            FilterExpression = "VerificationCode = :code AND MessageType = :type",
            ExpressionAttributeValues = new Dictionary<string, AttributeValue>
            {
                [":pk"] = new() { S = KeyBuilder.NotificationPk(customerId) },
                [":skPrefix"] = new() { S = DateTime.UtcNow.AddDays(-30).ToString("yyyy-MM-dd") },
                [":code"] = new() { S = code },
                [":type"] = new() { S = "Reminder" }
            },
            Limit = 1
        };

        try
        {
            var response = await _dynamoDb.QueryAsync(request, cancellationToken);
            return response.Items.Count > 0;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error checking if reminder was sent for code {Code}", code);
            return false;
        }
    }

    private async Task<Customer?> GetCustomerAsync(string customerId, CancellationToken cancellationToken)
    {
        var request = new GetItemRequest
        {
            TableName = _options.TableName,
            Key = new Dictionary<string, AttributeValue>
            {
                [DynamoDbContext.PkAttribute] = new() { S = KeyBuilder.CustomerPk(customerId) },
                [DynamoDbContext.SkAttribute] = new() { S = KeyBuilder.CustomerSk() }
            }
        };

        try
        {
            var response = await _dynamoDb.GetItemAsync(request, cancellationToken);
            if (response.Item is not { Count: > 0 })
                return null;

            return new Customer(
                CustomerId: response.Item["CustomerId"].S,
                Name: response.Item["Name"].S,
                PhoneNumber: response.Item["PhoneNumber"].S,
                QualifyingPurchases: int.Parse(response.Item.GetValueOrDefault("QualifyingPurchases")?.N ?? "0"),
                CurrentCycleId: response.Item.GetValueOrDefault("CurrentCycleId")?.S ?? ""
            );
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error fetching customer {CustomerId}", customerId);
            return null;
        }
    }

    private async Task<Outlet?> GetOutletAsync(string outletId, CancellationToken cancellationToken)
    {
        var request = new GetItemRequest
        {
            TableName = _options.TableName,
            Key = new Dictionary<string, AttributeValue>
            {
                [DynamoDbContext.PkAttribute] = new() { S = KeyBuilder.OutletPk(outletId) },
                [DynamoDbContext.SkAttribute] = new() { S = KeyBuilder.OutletSk() }
            }
        };

        try
        {
            var response = await _dynamoDb.GetItemAsync(request, cancellationToken);
            if (response.Item is not { Count: > 0 })
                return null;

            return new Outlet(
                OutletId: response.Item["OutletId"].S,
                Name: response.Item["Name"].S,
                Address: response.Item.GetValueOrDefault("Address")?.S ?? "",
                PhoneNumber: response.Item.GetValueOrDefault("PhoneNumber")?.S ?? "",
                AssignedManagerId: response.Item.GetValueOrDefault("AssignedManagerId")?.S ?? "",
                IsActive: response.Item.GetValueOrDefault("IsActive")?.BOOL ?? true
            );
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error fetching outlet {OutletId}", outletId);
            return null;
        }
    }
}

/// <summary>
/// Internal model for verification code query results.
/// </summary>
internal class VerificationCodeInfo
{
    public string Code { get; set; } = string.Empty;
    public string CustomerId { get; set; } = string.Empty;
    public string OutletId { get; set; } = string.Empty;
    public string GiftDescription { get; set; } = string.Empty;
    public DateTime ExpiresAt { get; set; }
}
