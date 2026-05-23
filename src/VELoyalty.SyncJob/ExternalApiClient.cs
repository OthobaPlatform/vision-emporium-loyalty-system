using System.Net.Http.Json;
using System.Text.Json;
using System.Text.Json.Serialization;
using Microsoft.Extensions.Logging;

namespace VELoyalty.SyncJob;

/// <summary>
/// Represents a raw sales record returned by the external API.
/// </summary>
public sealed class ExternalSalesRecord
{
    [JsonPropertyName("customerId")]
    public string? CustomerId { get; set; }

    [JsonPropertyName("customerName")]
    public string? CustomerName { get; set; }

    [JsonPropertyName("customerPhone")]
    public string? CustomerPhone { get; set; }

    [JsonPropertyName("outletId")]
    public string? OutletId { get; set; }

    [JsonPropertyName("purchaseDate")]
    public string? PurchaseDate { get; set; }

    [JsonPropertyName("purchaseAmount")]
    public string? PurchaseAmount { get; set; }

    [JsonPropertyName("productCategory")]
    public string? ProductCategory { get; set; }
}

/// <summary>
/// Represents the response from the external sales API.
/// </summary>
public sealed class ExternalApiResponse
{
    [JsonPropertyName("records")]
    public List<ExternalSalesRecord> Records { get; set; } = new();

    [JsonPropertyName("cursor")]
    public string? Cursor { get; set; }
}

/// <summary>
/// Configuration for the external API connection, stored in DynamoDB config.
/// </summary>
public sealed class ExternalApiConfig
{
    public string Endpoint { get; set; } = string.Empty;
    public string ApiKey { get; set; } = string.Empty;
    public string? LastSyncCursor { get; set; }
}

/// <summary>
/// Client for fetching sales data from the external API with retry logic and exponential backoff.
/// Implements retry with delays of 5s, 10s, 20s and a 30-second HTTP timeout.
/// </summary>
public sealed class ExternalApiClient
{
    private readonly HttpClient _httpClient;
    private readonly ILogger<ExternalApiClient> _logger;

    /// <summary>
    /// Retry delays for exponential backoff: 5s, 10s, 20s.
    /// </summary>
    private static readonly TimeSpan[] RetryDelays =
    [
        TimeSpan.FromSeconds(5),
        TimeSpan.FromSeconds(10),
        TimeSpan.FromSeconds(20)
    ];

    public ExternalApiClient(HttpClient httpClient, ILogger<ExternalApiClient> logger)
    {
        _httpClient = httpClient ?? throw new ArgumentNullException(nameof(httpClient));
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));
    }

    /// <summary>
    /// Fetches sales records from the external API with retry logic.
    /// Retries up to 3 times with exponential backoff (5s, 10s, 20s) on failure.
    /// HTTP timeout is set to 30 seconds.
    /// </summary>
    /// <param name="config">The external API configuration (endpoint, credentials, cursor).</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>The API response with records and optional cursor, or null if all retries failed.</returns>
    /// <exception cref="ExternalApiException">Thrown when all retry attempts are exhausted.</exception>
    public async Task<ExternalApiResponse> FetchSalesDataAsync(
        ExternalApiConfig config,
        CancellationToken cancellationToken = default)
    {
        var requestUrl = BuildRequestUrl(config.Endpoint, config.LastSyncCursor);
        Exception? lastException = null;

        for (int attempt = 0; attempt <= RetryDelays.Length; attempt++)
        {
            try
            {
                if (attempt > 0)
                {
                    var delay = RetryDelays[attempt - 1];
                    _logger.LogWarning(
                        "Retry attempt {Attempt}/{MaxAttempts} after {Delay}s delay for endpoint {Endpoint}",
                        attempt, RetryDelays.Length, delay.TotalSeconds, config.Endpoint);
                    await Task.Delay(delay, cancellationToken);
                }

                using var request = new HttpRequestMessage(HttpMethod.Get, requestUrl);

                if (!string.IsNullOrWhiteSpace(config.ApiKey))
                {
                    request.Headers.Add("X-API-Key", config.ApiKey);
                }

                using var cts = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
                cts.CancelAfter(TimeSpan.FromSeconds(30));

                var response = await _httpClient.SendAsync(request, cts.Token);

                if (!response.IsSuccessStatusCode)
                {
                    var statusCode = (int)response.StatusCode;
                    var errorBody = await response.Content.ReadAsStringAsync(cts.Token);

                    _logger.LogError(
                        "External API returned HTTP {StatusCode} for endpoint {Endpoint}. Body: {ErrorBody}",
                        statusCode, config.Endpoint, errorBody);

                    lastException = new ExternalApiException(
                        $"External API returned HTTP {statusCode}: {errorBody}",
                        statusCode);

                    continue;
                }

                var apiResponse = await response.Content.ReadFromJsonAsync(
                    ExternalApiJsonContext.Default.ExternalApiResponse,
                    cts.Token);

                if (apiResponse is null)
                {
                    lastException = new ExternalApiException("External API returned null response body.", 200);
                    continue;
                }

                _logger.LogInformation(
                    "Successfully fetched {RecordCount} records from external API",
                    apiResponse.Records.Count);

                return apiResponse;
            }
            catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
            {
                throw; // Don't retry if the overall operation was cancelled
            }
            catch (OperationCanceledException ex)
            {
                _logger.LogError(ex,
                    "HTTP request timed out (30s) for endpoint {Endpoint} on attempt {Attempt}",
                    config.Endpoint, attempt + 1);
                lastException = new ExternalApiException(
                    $"HTTP request timed out after 30 seconds on attempt {attempt + 1}.", 0);
            }
            catch (HttpRequestException ex)
            {
                _logger.LogError(ex,
                    "HTTP request failed for endpoint {Endpoint} on attempt {Attempt}: {Message}",
                    config.Endpoint, attempt + 1, ex.Message);
                lastException = new ExternalApiException(
                    $"HTTP request failed: {ex.Message}", 0);
            }
        }

        throw lastException ?? new ExternalApiException("All retry attempts exhausted.", 0);
    }

    private static string BuildRequestUrl(string endpoint, string? cursor)
    {
        if (string.IsNullOrWhiteSpace(cursor))
            return endpoint;

        var separator = endpoint.Contains('?') ? "&" : "?";
        return $"{endpoint}{separator}cursor={Uri.EscapeDataString(cursor)}";
    }
}

/// <summary>
/// Exception thrown when the external API call fails after all retries.
/// </summary>
public sealed class ExternalApiException : Exception
{
    public int HttpStatusCode { get; }

    public ExternalApiException(string message, int httpStatusCode)
        : base(message)
    {
        HttpStatusCode = httpStatusCode;
    }
}

/// <summary>
/// Source-generated JSON serializer context for Native AOT compatibility.
/// </summary>
[JsonSerializable(typeof(ExternalApiResponse))]
[JsonSerializable(typeof(ExternalSalesRecord))]
[JsonSerializable(typeof(List<ExternalSalesRecord>))]
internal partial class ExternalApiJsonContext : JsonSerializerContext
{
}
