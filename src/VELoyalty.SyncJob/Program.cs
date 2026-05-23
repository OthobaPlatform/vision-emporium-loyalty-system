using System.Text.Json.Serialization;
using Amazon.DynamoDBv2;
using Amazon.Lambda.Core;
using Amazon.Lambda.Serialization.SystemTextJson;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using VELoyalty.Data;
using VELoyalty.Data.Repositories;
using VELoyalty.SyncJob;

// Configure services
var services = new ServiceCollection();

// Logging
services.AddLogging(builder =>
{
    builder.AddConsole();
    builder.SetMinimumLevel(Microsoft.Extensions.Logging.LogLevel.Information);
});

// AWS DynamoDB client
services.AddSingleton<IAmazonDynamoDB>(sp =>
{
    var config = new AmazonDynamoDBConfig
    {
        RegionEndpoint = Amazon.RegionEndpoint.APSouth1
    };
    return new AmazonDynamoDBClient(config);
});

// DynamoDB context
services.AddSingleton<DynamoDbContext>(sp =>
{
    var client = sp.GetRequiredService<IAmazonDynamoDB>();
    var tableName = Environment.GetEnvironmentVariable("DYNAMODB_TABLE_NAME") ?? DynamoDbContext.TableName;
    return new DynamoDbContext(client, tableName);
});

// Repositories
services.AddSingleton<PurchaseRepository>();
services.AddSingleton<CustomerRepository>();
services.AddSingleton<SyncJobRepository>();
services.AddSingleton<ConfigRepository>();

// HTTP client for external API with 30-second timeout
services.AddHttpClient<ExternalApiClient>(client =>
{
    client.Timeout = TimeSpan.FromSeconds(30);
});

// Sync job handler
services.AddSingleton<SyncJobHandler>();

var serviceProvider = services.BuildServiceProvider();

// Lambda function handler for EventBridge Scheduler trigger
var handler = async (object scheduledEvent, ILambdaContext context) =>
{
    var logger = serviceProvider.GetRequiredService<ILogger<Program>>();
    logger.LogInformation("Sync job triggered by EventBridge Scheduler at {Time}", DateTime.UtcNow);

    var syncHandler = serviceProvider.GetRequiredService<SyncJobHandler>();

    using var cts = new CancellationTokenSource(TimeSpan.FromSeconds(25)); // Leave 5s buffer before Lambda timeout
    var result = await syncHandler.ExecuteAsync(cts.Token);

    logger.LogInformation(
        "Sync job {JobId} completed: Status={Status}, Fetched={Fetched}, Stored={Stored}, Skipped={Skipped}, Rejected={Rejected}",
        result.JobId, result.Status, result.RecordsFetched, result.RecordsStored,
        result.RecordsSkipped, result.RecordsRejected);

    return new SyncJobResponse
    {
        JobId = result.JobId,
        Status = result.Status,
        RecordsFetched = result.RecordsFetched,
        RecordsStored = result.RecordsStored,
        RecordsSkipped = result.RecordsSkipped,
        RecordsRejected = result.RecordsRejected
    };
};

// Register the Lambda handler
await Amazon.Lambda.RuntimeSupport.LambdaBootstrapBuilder
    .Create(handler, new SourceGeneratorLambdaJsonSerializer<SyncJobSerializerContext>())
    .Build()
    .RunAsync();

/// <summary>
/// Response returned by the sync job Lambda function.
/// </summary>
public sealed class SyncJobResponse
{
    [JsonPropertyName("jobId")]
    public string JobId { get; set; } = string.Empty;

    [JsonPropertyName("status")]
    public string Status { get; set; } = string.Empty;

    [JsonPropertyName("recordsFetched")]
    public int RecordsFetched { get; set; }

    [JsonPropertyName("recordsStored")]
    public int RecordsStored { get; set; }

    [JsonPropertyName("recordsSkipped")]
    public int RecordsSkipped { get; set; }

    [JsonPropertyName("recordsRejected")]
    public int RecordsRejected { get; set; }
}

/// <summary>
/// Source-generated JSON serializer context for Native AOT compatibility.
/// </summary>
[JsonSerializable(typeof(SyncJobResponse))]
[JsonSerializable(typeof(object))]
internal partial class SyncJobSerializerContext : JsonSerializerContext
{
}
