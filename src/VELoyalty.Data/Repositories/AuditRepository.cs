using Amazon.DynamoDBv2.Model;
using VELoyalty.Core;

namespace VELoyalty.Data.Repositories;

/// <summary>
/// Repository for managing audit trail entries in DynamoDB.
/// Audit records are append-only: only write and query operations are exposed.
/// No update or delete operations are provided to ensure immutability (Requirement 9.5).
/// </summary>
public class AuditRepository : DynamoDbRepository
{
    public AuditRepository(DynamoDbContext context) : base(context) { }

    /// <summary>
    /// Appends a new audit entry. This is the only write operation; audit records cannot be modified or deleted.
    /// </summary>
    /// <param name="entry">The audit entry to record.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    public async Task AppendAsync(AuditEntry entry, CancellationToken cancellationToken = default)
    {
        var item = AttributeValueSerializer.NewItem(
                KeyBuilder.AuditPk(),
                KeyBuilder.AuditSk(entry.Timestamp, entry.EventType))
            .WithString("eventType", entry.EventType)
            .WithString("actorId", entry.ActorId)
            .WithString("entityType", entry.EntityType)
            .WithString("entityId", entry.EntityId)
            .WithStringMap("details", entry.Details)
            .WithDateTime("timestamp", entry.Timestamp)
            .Build();

        await PutItemAsync(item, cancellationToken: cancellationToken);
    }

    /// <summary>
    /// Queries audit entries within a time range, ordered by timestamp.
    /// </summary>
    /// <param name="from">Start of the time range (inclusive).</param>
    /// <param name="to">End of the time range (inclusive).</param>
    /// <param name="scanIndexForward">True for ascending order, false for descending.</param>
    /// <param name="limit">Maximum number of records to return (0 = no limit).</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>List of audit entries within the specified range.</returns>
    public async Task<List<AuditEntry>> QueryByTimeRangeAsync(
        DateTime from,
        DateTime to,
        bool scanIndexForward = false,
        int limit = 0,
        CancellationToken cancellationToken = default)
    {
        // SK format is {timestamp}#{eventType}, so we use BETWEEN on the SK prefix
        var fromSk = from.ToUniversalTime().ToString("yyyy-MM-ddTHH:mm:ss.fffZ");
        var toSk = to.ToUniversalTime().ToString("yyyy-MM-ddTHH:mm:ss.fffZ") + "~"; // ~ sorts after all event types

        var items = await QueryAsync(
            keyConditionExpression: "#pk = :pk AND #sk BETWEEN :from AND :to",
            expressionAttributeValues: new Dictionary<string, AttributeValue>
            {
                [":pk"] = AttributeValueSerializer.ToS(KeyBuilder.AuditPk()),
                [":from"] = AttributeValueSerializer.ToS(fromSk),
                [":to"] = AttributeValueSerializer.ToS(toSk)
            },
            expressionAttributeNames: new Dictionary<string, string>
            {
                ["#pk"] = DynamoDbContext.PkAttribute,
                ["#sk"] = DynamoDbContext.SkAttribute
            },
            scanIndexForward: scanIndexForward,
            limit: limit,
            cancellationToken: cancellationToken);

        return items.Select(MapToAuditEntry).ToList();
    }

    /// <summary>
    /// Queries audit entries by event type within a time range.
    /// Uses a filter expression on the eventType attribute after the key condition query.
    /// </summary>
    /// <param name="eventType">The event type to filter by.</param>
    /// <param name="from">Start of the time range (inclusive).</param>
    /// <param name="to">End of the time range (inclusive).</param>
    /// <param name="scanIndexForward">True for ascending order, false for descending.</param>
    /// <param name="limit">Maximum number of records to return (0 = no limit).</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>List of audit entries matching the event type within the specified range.</returns>
    public async Task<List<AuditEntry>> QueryByEventTypeAsync(
        string eventType,
        DateTime from,
        DateTime to,
        bool scanIndexForward = false,
        int limit = 0,
        CancellationToken cancellationToken = default)
    {
        var fromSk = from.ToUniversalTime().ToString("yyyy-MM-ddTHH:mm:ss.fffZ");
        var toSk = to.ToUniversalTime().ToString("yyyy-MM-ddTHH:mm:ss.fffZ") + "~";

        var items = await QueryAsync(
            keyConditionExpression: "#pk = :pk AND #sk BETWEEN :from AND :to",
            expressionAttributeValues: new Dictionary<string, AttributeValue>
            {
                [":pk"] = AttributeValueSerializer.ToS(KeyBuilder.AuditPk()),
                [":from"] = AttributeValueSerializer.ToS(fromSk),
                [":to"] = AttributeValueSerializer.ToS(toSk),
                [":eventType"] = AttributeValueSerializer.ToS(eventType)
            },
            expressionAttributeNames: new Dictionary<string, string>
            {
                ["#pk"] = DynamoDbContext.PkAttribute,
                ["#sk"] = DynamoDbContext.SkAttribute,
                ["#eventType"] = "eventType"
            },
            filterExpression: "#eventType = :eventType",
            scanIndexForward: scanIndexForward,
            limit: limit,
            cancellationToken: cancellationToken);

        return items.Select(MapToAuditEntry).ToList();
    }

    private static AuditEntry MapToAuditEntry(Dictionary<string, AttributeValue> item) =>
        new(
            EventType: AttributeValueSerializer.GetRequiredString(item, "eventType"),
            ActorId: AttributeValueSerializer.GetRequiredString(item, "actorId"),
            EntityType: AttributeValueSerializer.GetRequiredString(item, "entityType"),
            EntityId: AttributeValueSerializer.GetRequiredString(item, "entityId"),
            Details: AttributeValueSerializer.GetStringMap(item, "details"),
            Timestamp: AttributeValueSerializer.GetDateTime(item, "timestamp")
        );
}
