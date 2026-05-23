using System.Text.Json.Serialization;
using Amazon.DynamoDBv2;
using Amazon.Lambda.Core;
using Amazon.Lambda.RuntimeSupport;
using Amazon.Lambda.S3Events;
using Amazon.Lambda.Serialization.SystemTextJson;
using Amazon.S3;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using VELoyalty.Data;
using VELoyalty.Data.Repositories;
using VELoyalty.ExcelProcessor;

// Configure services
var services = new ServiceCollection();

services.AddLogging(builder =>
{
    builder.AddConsole();
    builder.SetMinimumLevel(Microsoft.Extensions.Logging.LogLevel.Information);
});

// AWS SDK clients
services.AddSingleton<IAmazonS3, AmazonS3Client>();
services.AddSingleton<IAmazonDynamoDB, AmazonDynamoDBClient>();

// DynamoDB context and repositories
services.AddSingleton(sp =>
{
    var client = sp.GetRequiredService<IAmazonDynamoDB>();
    var tableName = Environment.GetEnvironmentVariable("DYNAMODB_TABLE_NAME") ?? DynamoDbContext.TableName;
    return new DynamoDbContext(client, tableName);
});

services.AddSingleton<PurchaseRepository>();
services.AddSingleton<CustomerRepository>();
services.AddSingleton<ImportJobRepository>();

// Function handler
services.AddSingleton<Function>();

var serviceProvider = services.BuildServiceProvider();
var function = serviceProvider.GetRequiredService<Function>();

// Set up the Lambda runtime with S3 event serialization using source-generated serializer
var serializer = new SourceGeneratorLambdaJsonSerializer<ExcelProcessorJsonContext>();

var handler = async (S3Event s3Event, ILambdaContext context) =>
{
    await function.FunctionHandler(s3Event, context);
};

await LambdaBootstrapBuilder
    .Create<S3Event>(handler, serializer)
    .Build()
    .RunAsync();

/// <summary>
/// Source-generated JSON serializer context for Native AOT compatibility.
/// </summary>
[JsonSerializable(typeof(S3Event))]
[JsonSerializable(typeof(string))]
internal partial class ExcelProcessorJsonContext : JsonSerializerContext
{
}
