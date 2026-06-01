using Amazon.DynamoDBv2.Model;
using VELoyalty.Core;

namespace VELoyalty.Data.Repositories;

/// <summary>
/// Repository for managing notification log entries in DynamoDB.
/// Notification items use PK = NOTIF#{customerId}, SK = {timestamp}#{type}.
/// GSI1 is used for querying failed notifications across all customers.
/// </summary>
public class NotificationRepository : DynamoDbRepository
{
    public NotificationRepository(DynamoDbContext context) : base(context)
    {
    }

    /// <summary>
    /// Stores a notification log entry.
    /// </summary>
    public async Task PutNotificationAsync(NotificationLog notification, CancellationToken cancellationToken = default)
    {
        var item = AttributeValueSerializer.NewItem(
                KeyBuilder.NotificationPk(notification.CustomerId),
                KeyBuilder.NotificationSk(notification.SentAt, notification.MessageType))
            .WithString("NotificationId", notification.NotificationId)
            .WithString("CustomerId", notification.CustomerId)
            .WithString("PhoneNumber", notification.PhoneNumber)
            .WithString("MessageType", notification.MessageType)
            .WithString("Content", notification.Content)
            .WithString("DeliveryStatus", notification.DeliveryStatus)
            .WithNullableString("FailureReason", notification.FailureReason)
            .WithInt("AttemptCount", notification.AttemptCount)
            .WithDateTime("SentAt", notification.SentAt)
            .WithGsi1("NOTIF_STATUS#Failed", $"NOTIF#{notification.SentAt:yyyy-MM-ddTHH:mm:ss.fffZ}")
            .WithGsi2($"NOTIFID#{notification.NotificationId}", $"NOTIF#{notification.DeliveryStatus}")
            .Build();

        // Only write GSI1 if status is Failed (for querying failed notifications)
        if (notification.DeliveryStatus != "Failed")
        {
            item.Remove(DynamoDbContext.Gsi1Pk);
            item.Remove(DynamoDbContext.Gsi1Sk);
        }

        await PutItemAsync(item, cancellationToken: cancellationToken);
    }

    /// <summary>
    /// Gets a notification by its ID using GSI2.
    /// </summary>
    public async Task<NotificationLog?> GetNotificationByIdAsync(string notificationId, CancellationToken cancellationToken = default)
    {
        var items = await QueryAsync(
            keyConditionExpression: "GSI2PK = :pk",
            expressionAttributeValues: new Dictionary<string, AttributeValue>
            {
                [":pk"] = AttributeValueSerializer.ToS($"NOTIFID#{notificationId}")
            },
            indexName: DynamoDbContext.Gsi2IndexName,
            limit: 1,
            cancellationToken: cancellationToken);

        var item = items.FirstOrDefault();
        return item is null ? null : MapToNotificationLog(item);
    }

    /// <summary>
    /// Gets all failed notifications using GSI1.
    /// </summary>
    public async Task<List<NotificationLog>> GetFailedNotificationsAsync(int limit = 100, CancellationToken cancellationToken = default)
    {
        var items = await QueryAsync(
            keyConditionExpression: "GSI1PK = :pk",
            expressionAttributeValues: new Dictionary<string, AttributeValue>
            {
                [":pk"] = AttributeValueSerializer.ToS("NOTIF_STATUS#Failed")
            },
            indexName: DynamoDbContext.Gsi1IndexName,
            scanIndexForward: false,
            limit: limit,
            cancellationToken: cancellationToken);

        return items.Select(MapToNotificationLog).ToList();
    }

    /// <summary>
    /// Updates a notification's delivery status and attempt count.
    /// </summary>
    public async Task UpdateNotificationStatusAsync(
        NotificationLog notification,
        string newStatus,
        string? failureReason,
        CancellationToken cancellationToken = default)
    {
        // Since we need to update GSI keys based on status, we re-put the full item
        var updated = notification with
        {
            DeliveryStatus = newStatus,
            FailureReason = failureReason,
            AttemptCount = notification.AttemptCount + 1
        };

        await PutNotificationAsync(updated, cancellationToken);
    }

    private static NotificationLog MapToNotificationLog(Dictionary<string, AttributeValue> item) =>
        new(
            NotificationId: AttributeValueSerializer.GetRequiredString(item, "NotificationId"),
            CustomerId: AttributeValueSerializer.GetRequiredString(item, "CustomerId"),
            PhoneNumber: AttributeValueSerializer.GetRequiredString(item, "PhoneNumber"),
            MessageType: AttributeValueSerializer.GetRequiredString(item, "MessageType"),
            Content: AttributeValueSerializer.GetRequiredString(item, "Content"),
            DeliveryStatus: AttributeValueSerializer.GetRequiredString(item, "DeliveryStatus"),
            FailureReason: AttributeValueSerializer.GetString(item, "FailureReason"),
            AttemptCount: AttributeValueSerializer.GetInt(item, "AttemptCount"),
            SentAt: AttributeValueSerializer.GetDateTime(item, "SentAt")
        );
}
