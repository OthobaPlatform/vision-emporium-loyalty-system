using Amazon.DynamoDBv2.Model;
using VELoyalty.Core;

namespace VELoyalty.Data.Repositories;

/// <summary>
/// Repository for managing SyncJob records in DynamoDB.
/// Supports creating/updating job records and querying by status via GSI2.
/// Key pattern: PK=SYNC, SK=JOB#{timestamp}, GSI2PK=JOBID#{jobId}, GSI2SK=SYNC#{status}
/// </summary>
public class SyncJobRepository : DynamoDbRepository
{
    public SyncJobRepository(DynamoDbContext context) : base(context) { }

    /// <summary>
    /// Creates a new sync job record.
    /// </summary>
    /// <param name="job">The sync job result to store.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    public async Task CreateAsync(SyncJobResult job, CancellationToken cancellationToken = default)
    {
        var item = BuildJobItem(job);
        await PutItemAsync(item, cancellationToken: cancellationToken);
    }

    /// <summary>
    /// Updates an existing sync job record (full replacement).
    /// Used to update status and counts when a job completes.
    /// </summary>
    /// <param name="job">The sync job result with updated values.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    public async Task UpdateAsync(SyncJobResult job, CancellationToken cancellationToken = default)
    {
        var item = BuildJobItem(job);
        await PutItemAsync(item, cancellationToken: cancellationToken);
    }

    /// <summary>
    /// Gets a sync job by its identifier using GSI2 (JOBID#{jobId}).
    /// </summary>
    /// <param name="jobId">The job identifier.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>The sync job result, or null if not found.</returns>
    public async Task<SyncJobResult?> GetByIdAsync(string jobId, CancellationToken cancellationToken = default)
    {
        var items = await QueryAsync(
            keyConditionExpression: "#gsi2pk = :gsi2pk",
            expressionAttributeValues: new Dictionary<string, AttributeValue>
            {
                [":gsi2pk"] = AttributeValueSerializer.ToS(KeyBuilder.SyncJobGsi2Pk(jobId))
            },
            expressionAttributeNames: new Dictionary<string, string>
            {
                ["#gsi2pk"] = DynamoDbContext.Gsi2Pk
            },
            indexName: DynamoDbContext.Gsi2IndexName,
            limit: 1,
            cancellationToken: cancellationToken);

        return items.Count == 0 ? null : MapToSyncJobResult(items[0]);
    }

    /// <summary>
    /// Queries sync jobs by status using GSI2.
    /// GSI2SK begins with "SYNC#{status}" for status-based filtering.
    /// </summary>
    /// <param name="status">The job status to filter by.</param>
    /// <param name="limit">Maximum number of records to return (0 = no limit).</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>List of sync jobs with the specified status.</returns>
    public async Task<List<SyncJobResult>> QueryByStatusAsync(
        string status,
        int limit = 0,
        CancellationToken cancellationToken = default)
    {
        // Query the main table by PK=SYNC with a filter on status attribute
        var items = await QueryAsync(
            keyConditionExpression: "#pk = :pk",
            expressionAttributeValues: new Dictionary<string, AttributeValue>
            {
                [":pk"] = AttributeValueSerializer.ToS(KeyBuilder.SyncJobPk()),
                [":status"] = AttributeValueSerializer.ToS(status)
            },
            expressionAttributeNames: new Dictionary<string, string>
            {
                ["#pk"] = DynamoDbContext.PkAttribute,
                ["#status"] = "status"
            },
            filterExpression: "#status = :status",
            scanIndexForward: false,
            limit: limit,
            cancellationToken: cancellationToken);

        return items.Select(MapToSyncJobResult).ToList();
    }

    /// <summary>
    /// Lists recent sync jobs ordered by timestamp (most recent first).
    /// </summary>
    /// <param name="limit">Maximum number of records to return.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>List of recent sync jobs.</returns>
    public async Task<List<SyncJobResult>> ListRecentAsync(
        int limit = 20,
        CancellationToken cancellationToken = default)
    {
        var items = await QueryAsync(
            keyConditionExpression: "#pk = :pk",
            expressionAttributeValues: new Dictionary<string, AttributeValue>
            {
                [":pk"] = AttributeValueSerializer.ToS(KeyBuilder.SyncJobPk())
            },
            expressionAttributeNames: new Dictionary<string, string>
            {
                ["#pk"] = DynamoDbContext.PkAttribute
            },
            scanIndexForward: false,
            limit: limit,
            cancellationToken: cancellationToken);

        return items.Select(MapToSyncJobResult).ToList();
    }

    private static Dictionary<string, AttributeValue> BuildJobItem(SyncJobResult job)
    {
        return AttributeValueSerializer.NewItem(
                KeyBuilder.SyncJobPk(),
                KeyBuilder.SyncJobSk(job.StartedAt))
            .WithGsi2(KeyBuilder.SyncJobGsi2Pk(job.JobId), KeyBuilder.SyncJobGsi2Sk(job.Status))
            .WithString("jobId", job.JobId)
            .WithString("status", job.Status)
            .WithInt("recordsFetched", job.RecordsFetched)
            .WithInt("recordsStored", job.RecordsStored)
            .WithInt("recordsSkipped", job.RecordsSkipped)
            .WithInt("recordsRejected", job.RecordsRejected)
            .WithDateTime("startedAt", job.StartedAt)
            .WithDateTime("completedAt", job.CompletedAt)
            .Build();
    }

    private static SyncJobResult MapToSyncJobResult(Dictionary<string, AttributeValue> item) =>
        new(
            JobId: AttributeValueSerializer.GetRequiredString(item, "jobId"),
            Status: AttributeValueSerializer.GetRequiredString(item, "status"),
            RecordsFetched: AttributeValueSerializer.GetInt(item, "recordsFetched"),
            RecordsStored: AttributeValueSerializer.GetInt(item, "recordsStored"),
            RecordsSkipped: AttributeValueSerializer.GetInt(item, "recordsSkipped"),
            RecordsRejected: AttributeValueSerializer.GetInt(item, "recordsRejected"),
            StartedAt: AttributeValueSerializer.GetDateTime(item, "startedAt"),
            CompletedAt: AttributeValueSerializer.GetDateTime(item, "completedAt")
        );
}
