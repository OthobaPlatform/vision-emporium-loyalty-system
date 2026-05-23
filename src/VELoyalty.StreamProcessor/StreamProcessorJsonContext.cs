using System.Text.Json.Serialization;
using Amazon.Lambda.DynamoDBEvents;

namespace VELoyalty.StreamProcessor;

/// <summary>
/// Source-generated JSON serializer context for Native AOT compatibility.
/// Includes all types that need to be serialized/deserialized by the Stream Processor Lambda.
/// </summary>
[JsonSerializable(typeof(DynamoDBEvent))]
[JsonSerializable(typeof(NotificationPayload))]
[JsonSourceGenerationOptions(PropertyNamingPolicy = JsonKnownNamingPolicy.CamelCase)]
internal partial class StreamProcessorJsonContext : JsonSerializerContext
{
}
