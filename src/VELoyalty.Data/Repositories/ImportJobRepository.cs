using Amazon.DynamoDBv2.Model;
using VELoyalty.Core;

namespace VELoyalty.Data.Repositories;

/// <summary>
/// Repository for managing ImportJob records in DynamoDB.
/// Supports creating/updating job records and querying by status via GSI2.
/// Key pattern: PK=IMPORT, SK=JOB#{timestamp}, GSI2PK=JOBID#{jobId}, GSI2SK=IMPORT#{status}
/// </summary>
public class ImportJobRepository : DynamoDbRepository
{
    public ImportJobRepository(DynamoDbContext context) : base(context) { }

    /// <summary>
    /// Creates a new import job record.
    /// </summary>
    /// <param name="job">The import job result to store.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    public async Task CreateAsync(ImportJobResult job, CancellationToken cancellationToken = default)
    {
        var item = BuildJobItem(job);
        await PutItemAsync(item, cancellationToken: cancellationToken);
    }

    /// <summary>
    /// Updates an existing import job record (full replacement).
    /// Used to update status and counts when a job completes.
    /// </summary>
    /// <param name="job">The import job result with updated values.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    public async Task UpdateAsync(ImportJobResult job, CancellationToken cancellationToken = default)
    {
        var item = BuildJobItem(job);
        await PutItemAsync(item, cancellationToken: cancellationToken);
    }

    /// <summary>
    /// Gets an import job by its identifier using GSI2 (JOBID#{jobId}).
    /// </summary>
    /// <param name="jobId">The job identifier.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>The import job result, or null if not found.</returns>
    public async Task<ImportJobResult?> GetByIdAsync(string jobId, CancellationToken cancellationToken = default)
    {
        var items = await QueryAsync(
            keyConditionExpression: "#gsi2pk = :gsi2pk",
            expressionAttributeValues: new Dictionary<string, AttributeValue>
            {
                [":gsi2pk"] = AttributeValueSerializer.ToS(KeyBuilder.ImportJobGsi2Pk(jobId))
            },
            expressionAttributeNames: new Dictionary<string, string>
            {
                ["#gsi2pk"] = DynamoDbContext.Gsi2Pk
            },
            indexName: DynamoDbContext.Gsi2IndexName,
            limit: 1,
            cancellationToken: cancellationToken);

        return items.Count == 0 ? null : MapToImportJobResult(items[0]);
    }

    /// <summary>
    /// Queries import jobs by status using a filter on the main table partition.
    /// </summary>
    /// <param name="status">The job status to filter by.</param>
    /// <param name="limit">Maximum number of records to return (0 = no limit).</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>List of import jobs with the specified status.</returns>
    public async Task<List<ImportJobResult>> QueryByStatusAsync(
        string status,
        int limit = 0,
        CancellationToken cancellationToken = default)
    {
        var items = await QueryAsync(
            keyConditionExpression: "#pk = :pk",
            expressionAttributeValues: new Dictionary<string, AttributeValue>
            {
                [":pk"] = AttributeValueSerializer.ToS(KeyBuilder.ImportJobPk()),
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

        return items.Select(MapToImportJobResult).ToList();
    }

    /// <summary>
    /// Lists recent import jobs ordered by timestamp (most recent first).
    /// </summary>
    /// <param name="limit">Maximum number of records to return.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>List of recent import jobs.</returns>
    public async Task<List<ImportJobResult>> ListRecentAsync(
        int limit = 20,
        CancellationToken cancellationToken = default)
    {
        var items = await QueryAsync(
            keyConditionExpression: "#pk = :pk",
            expressionAttributeValues: new Dictionary<string, AttributeValue>
            {
                [":pk"] = AttributeValueSerializer.ToS(KeyBuilder.ImportJobPk())
            },
            expressionAttributeNames: new Dictionary<string, string>
            {
                ["#pk"] = DynamoDbContext.PkAttribute
            },
            scanIndexForward: false,
            limit: limit,
            cancellationToken: cancellationToken);

        return items.Select(MapToImportJobResult).ToList();
    }

    private static Dictionary<string, AttributeValue> BuildJobItem(ImportJobResult job)
    {
        var builder = AttributeValueSerializer.NewItem(
                KeyBuilder.ImportJobPk(),
                KeyBuilder.ImportJobSk(job.StartedAt))
            .WithGsi2(KeyBuilder.ImportJobGsi2Pk(job.JobId), KeyBuilder.ImportJobGsi2Sk(job.Status))
            .WithString("jobId", job.JobId)
            .WithString("status", job.Status)
            .WithString("fileName", job.FileName)
            .WithInt("totalRows", job.TotalRows)
            .WithInt("recordsImported", job.RecordsImported)
            .WithInt("recordsRejected", job.RecordsRejected)
            .WithInt("recordsSkipped", job.RecordsSkipped)
            .WithDateTime("startedAt", job.StartedAt)
            .WithDateTime("completedAt", job.CompletedAt);

        var item = builder.Build();

        // Store rejected rows as a list of maps
        if (job.RejectedRows.Count > 0)
        {
            item["rejectedRows"] = new Amazon.DynamoDBv2.Model.AttributeValue
            {
                L = job.RejectedRows.Select(r => new Amazon.DynamoDBv2.Model.AttributeValue
                {
                    M = new Dictionary<string, Amazon.DynamoDBv2.Model.AttributeValue>
                    {
                        ["rowNumber"] = AttributeValueSerializer.ToN(r.RowNumber),
                        ["reason"] = AttributeValueSerializer.ToS(r.Reason)
                    }
                }).ToList()
            };
        }
        else
        {
            item["rejectedRows"] = new Amazon.DynamoDBv2.Model.AttributeValue { L = [] };
        }

        return item;
    }

    private static ImportJobResult MapToImportJobResult(Dictionary<string, AttributeValue> item)
    {
        var rejectedRows = new List<RejectedRow>();

        if (item.TryGetValue("rejectedRows", out var rejectedRowsAttr) && rejectedRowsAttr.L is not null)
        {
            foreach (var rowAttr in rejectedRowsAttr.L)
            {
                if (rowAttr.M is not null)
                {
                    var rowNumber = rowAttr.M.TryGetValue("rowNumber", out var rn) && rn.N is not null
                        ? int.Parse(rn.N, System.Globalization.CultureInfo.InvariantCulture)
                        : 0;
                    var reason = rowAttr.M.TryGetValue("reason", out var rs) && rs.S is not null
                        ? rs.S
                        : string.Empty;

                    rejectedRows.Add(new RejectedRow(rowNumber, reason));
                }
            }
        }

        return new ImportJobResult(
            JobId: AttributeValueSerializer.GetRequiredString(item, "jobId"),
            Status: AttributeValueSerializer.GetRequiredString(item, "status"),
            FileName: AttributeValueSerializer.GetRequiredString(item, "fileName"),
            TotalRows: AttributeValueSerializer.GetInt(item, "totalRows"),
            RecordsImported: AttributeValueSerializer.GetInt(item, "recordsImported"),
            RecordsRejected: AttributeValueSerializer.GetInt(item, "recordsRejected"),
            RecordsSkipped: AttributeValueSerializer.GetInt(item, "recordsSkipped"),
            RejectedRows: rejectedRows,
            StartedAt: AttributeValueSerializer.GetDateTime(item, "startedAt"),
            CompletedAt: AttributeValueSerializer.GetDateTime(item, "completedAt")
        );
    }
}
