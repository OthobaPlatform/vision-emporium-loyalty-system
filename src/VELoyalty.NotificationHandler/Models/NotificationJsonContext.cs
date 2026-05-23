using System.Text.Json.Serialization;

namespace VELoyalty.NotificationHandler.Models;

/// <summary>
/// Source-generated JSON serializer context for Native AOT compatibility.
/// </summary>
[JsonSerializable(typeof(NotificationEvent))]
[JsonSerializable(typeof(ReminderCheckEvent))]
[JsonSerializable(typeof(NotificationResponse))]
internal partial class NotificationJsonContext : JsonSerializerContext
{
}

/// <summary>
/// Response model for notification processing results.
/// </summary>
public class NotificationResponse
{
    [JsonPropertyName("status")]
    public string Status { get; set; } = string.Empty;

    [JsonPropertyName("message")]
    public string Message { get; set; } = string.Empty;

    [JsonPropertyName("processedAt")]
    public string ProcessedAt { get; set; } = string.Empty;
}
