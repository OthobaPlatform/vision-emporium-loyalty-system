using Amazon.DynamoDBv2;
using Amazon.Lambda.Serialization.SystemTextJson;
using Amazon.SQS;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Options;
using VELoyalty.Notifications;
using VELoyalty.NotificationHandler.Models;
using VELoyalty.NotificationHandler.Services;

var builder = WebApplication.CreateBuilder(args);

// Add AWS Lambda hosting support for Native AOT with source-generated JSON serializer
builder.Services.AddAWSLambdaHosting(LambdaEventSource.HttpApi,
    new SourceGeneratorLambdaJsonSerializer<AppJsonSerializerContext>());

// Configure notification handler options
builder.Services.Configure<NotificationHandlerOptions>(options =>
{
    options.RetryQueueUrl = builder.Configuration["NotificationHandler:RetryQueueUrl"] ?? "";
    options.TableName = builder.Configuration["NotificationHandler:TableName"] ?? "VELoyalty";
});

// Configure SMS gateway options
builder.Services.Configure<SmsGatewayOptions>(options =>
{
    options.BaseUrl = builder.Configuration["SmsGateway:BaseUrl"] ?? "";
    options.ApiKey = builder.Configuration["SmsGateway:ApiKey"] ?? "";
    options.SenderId = builder.Configuration["SmsGateway:SenderId"] ?? "VisionEmporium";
});

// Register AWS services
builder.Services.AddSingleton<IAmazonDynamoDB>(sp =>
    new AmazonDynamoDBClient(Amazon.RegionEndpoint.APSouth1));
builder.Services.AddSingleton<IAmazonSQS>(sp =>
    new AmazonSQSClient(Amazon.RegionEndpoint.APSouth1));

// Register SMS gateway client with HttpClient
builder.Services.AddHttpClient<ISmsGatewayClient, SmsGatewayClient>((sp, client) =>
{
    var options = sp.GetRequiredService<IOptions<SmsGatewayOptions>>().Value;
    if (!string.IsNullOrWhiteSpace(options.BaseUrl))
    {
        client.BaseAddress = new Uri(options.BaseUrl);
    }
    client.Timeout = TimeSpan.FromSeconds(30);
});

// Register notification service
builder.Services.AddScoped<NotificationService>();

var app = builder.Build();

// Health check endpoint
app.MapGet("/health", () => Results.Ok(new { Status = "Healthy", Service = "VELoyalty.NotificationHandler" }));

// Process a single notification event (invoked by Stream Processor or SQS retry)
app.MapPost("/api/v1/notifications/send", async (
    NotificationEvent notification,
    NotificationService notificationService,
    CancellationToken cancellationToken) =>
{
    try
    {
        await notificationService.ProcessNotificationAsync(notification, cancellationToken);

        return Results.Ok(new NotificationResponse
        {
            Status = "Processed",
            Message = $"Notification processed for customer {notification.CustomerId}",
            ProcessedAt = DateTime.UtcNow.ToString("O")
        });
    }
    catch (Exception ex)
    {
        return Results.Problem(
            detail: ex.Message,
            statusCode: 500,
            title: "Notification processing failed");
    }
});

// Process reminder check (invoked by EventBridge scheduler)
app.MapPost("/api/v1/notifications/reminder-check", async (
    NotificationService notificationService,
    CancellationToken cancellationToken) =>
{
    try
    {
        await notificationService.ProcessReminderCheckAsync(cancellationToken);

        return Results.Ok(new NotificationResponse
        {
            Status = "Completed",
            Message = "Reminder check completed",
            ProcessedAt = DateTime.UtcNow.ToString("O")
        });
    }
    catch (Exception ex)
    {
        return Results.Problem(
            detail: ex.Message,
            statusCode: 500,
            title: "Reminder check failed");
    }
});

app.Run();

[System.Text.Json.Serialization.JsonSerializable(typeof(object))]
[System.Text.Json.Serialization.JsonSerializable(typeof(NotificationEvent))]
[System.Text.Json.Serialization.JsonSerializable(typeof(ReminderCheckEvent))]
[System.Text.Json.Serialization.JsonSerializable(typeof(NotificationResponse))]
internal partial class AppJsonSerializerContext : System.Text.Json.Serialization.JsonSerializerContext
{
}
