namespace VELoyalty.Api.Services;

/// <summary>
/// Response model for audit log entries.
/// </summary>
public record AuditEntryResponse(
    DateTime Timestamp,
    string EventType,
    string ActorId,
    string EntityType,
    string EntityId,
    Dictionary<string, string> Details
);

/// <summary>
/// Response model for sync job history entries.
/// </summary>
public record SyncJobHistoryResponse(
    string JobId,
    string Status,
    int RecordsFetched,
    int RecordsStored,
    int RecordsSkipped,
    int RecordsRejected,
    DateTime StartedAt,
    DateTime CompletedAt
);

/// <summary>
/// Response model for triggering a manual sync job.
/// </summary>
public record TriggerSyncResponse(
    string JobId,
    string Status,
    string Message
);
