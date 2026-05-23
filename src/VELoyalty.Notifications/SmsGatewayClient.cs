using System.Net.Http.Json;
using System.Text.Json.Serialization;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace VELoyalty.Notifications;

/// <summary>
/// Configuration options for the SMS gateway client.
/// </summary>
public class SmsGatewayOptions
{
    /// <summary>
    /// Base URL of the third-party SMS gateway API.
    /// </summary>
    public string BaseUrl { get; set; } = string.Empty;

    /// <summary>
    /// API key for authenticating with the SMS gateway.
    /// </summary>
    public string ApiKey { get; set; } = string.Empty;

    /// <summary>
    /// Sender ID or name displayed on the SMS.
    /// </summary>
    public string SenderId { get; set; } = "VisionEmporium";
}

/// <summary>
/// Concrete implementation of ISmsGatewayClient that calls a third-party SMS API via HttpClient.
/// </summary>
public class SmsGatewayClient : ISmsGatewayClient
{
    private readonly HttpClient _httpClient;
    private readonly SmsGatewayOptions _options;
    private readonly ILogger<SmsGatewayClient> _logger;

    public SmsGatewayClient(
        HttpClient httpClient,
        IOptions<SmsGatewayOptions> options,
        ILogger<SmsGatewayClient> logger)
    {
        _httpClient = httpClient ?? throw new ArgumentNullException(nameof(httpClient));
        _options = options?.Value ?? throw new ArgumentNullException(nameof(options));
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));

        if (!string.IsNullOrWhiteSpace(_options.BaseUrl))
        {
            _httpClient.BaseAddress = new Uri(_options.BaseUrl);
        }
    }

    /// <inheritdoc />
    public async Task<SmsDeliveryResult> SendSmsAsync(
        string phoneNumber,
        string message,
        CancellationToken cancellationToken = default)
    {
        try
        {
            var request = new SmsRequest
            {
                To = phoneNumber,
                Message = message,
                SenderId = _options.SenderId,
                ApiKey = _options.ApiKey
            };

            var response = await _httpClient.PostAsJsonAsync(
                "api/sms/send",
                request,
                SmsJsonContext.Default.SmsRequest,
                cancellationToken);

            if (response.IsSuccessStatusCode)
            {
                var result = await response.Content.ReadFromJsonAsync(
                    SmsJsonContext.Default.SmsResponse,
                    cancellationToken);

                _logger.LogInformation(
                    "SMS sent successfully to {PhoneNumber}. MessageId: {MessageId}",
                    phoneNumber,
                    result?.MessageId);

                return SmsDeliveryResult.Success(result?.MessageId);
            }

            var errorContent = await response.Content.ReadAsStringAsync(cancellationToken);
            _logger.LogWarning(
                "SMS gateway returned {StatusCode} for {PhoneNumber}: {Error}",
                (int)response.StatusCode,
                phoneNumber,
                errorContent);

            return SmsDeliveryResult.Failure(
                $"Gateway returned HTTP {(int)response.StatusCode}: {errorContent}");
        }
        catch (HttpRequestException ex)
        {
            _logger.LogError(ex, "HTTP error sending SMS to {PhoneNumber}", phoneNumber);
            return SmsDeliveryResult.Failure($"HTTP error: {ex.Message}");
        }
        catch (TaskCanceledException ex) when (!cancellationToken.IsCancellationRequested)
        {
            _logger.LogError(ex, "Timeout sending SMS to {PhoneNumber}", phoneNumber);
            return SmsDeliveryResult.Failure("Request timed out");
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Unexpected error sending SMS to {PhoneNumber}", phoneNumber);
            return SmsDeliveryResult.Failure($"Unexpected error: {ex.Message}");
        }
    }
}

/// <summary>
/// Request payload for the SMS gateway API.
/// </summary>
internal class SmsRequest
{
    [JsonPropertyName("to")]
    public string To { get; set; } = string.Empty;

    [JsonPropertyName("message")]
    public string Message { get; set; } = string.Empty;

    [JsonPropertyName("sender_id")]
    public string SenderId { get; set; } = string.Empty;

    [JsonPropertyName("api_key")]
    public string ApiKey { get; set; } = string.Empty;
}

/// <summary>
/// Response payload from the SMS gateway API.
/// </summary>
internal class SmsResponse
{
    [JsonPropertyName("message_id")]
    public string? MessageId { get; set; }

    [JsonPropertyName("status")]
    public string? Status { get; set; }
}

/// <summary>
/// Source-generated JSON serializer context for AOT compatibility.
/// </summary>
[JsonSerializable(typeof(SmsRequest))]
[JsonSerializable(typeof(SmsResponse))]
internal partial class SmsJsonContext : JsonSerializerContext
{
}
