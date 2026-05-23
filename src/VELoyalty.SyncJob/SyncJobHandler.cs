using System.Globalization;
using Microsoft.Extensions.Logging;
using VELoyalty.Core;
using VELoyalty.Core.Validation;
using VELoyalty.Data.Repositories;

namespace VELoyalty.SyncJob;

/// <summary>
/// Orchestrates the sync job: fetches data from external API, validates, deduplicates,
/// stores valid records, and records the sync job result.
/// </summary>
public sealed class SyncJobHandler
{
    private readonly ExternalApiClient _apiClient;
    private readonly PurchaseRepository _purchaseRepository;
    private readonly CustomerRepository _customerRepository;
    private readonly SyncJobRepository _syncJobRepository;
    private readonly ConfigRepository _configRepository;
    private readonly ILogger<SyncJobHandler> _logger;

    public SyncJobHandler(
        ExternalApiClient apiClient,
        PurchaseRepository purchaseRepository,
        CustomerRepository customerRepository,
        SyncJobRepository syncJobRepository,
        ConfigRepository configRepository,
        ILogger<SyncJobHandler> logger)
    {
        _apiClient = apiClient ?? throw new ArgumentNullException(nameof(apiClient));
        _purchaseRepository = purchaseRepository ?? throw new ArgumentNullException(nameof(purchaseRepository));
        _customerRepository = customerRepository ?? throw new ArgumentNullException(nameof(customerRepository));
        _syncJobRepository = syncJobRepository ?? throw new ArgumentNullException(nameof(syncJobRepository));
        _configRepository = configRepository ?? throw new ArgumentNullException(nameof(configRepository));
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));
    }

    /// <summary>
    /// Executes the sync job: fetches from external API, validates, deduplicates, stores, and records results.
    /// </summary>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>The sync job result.</returns>
    public async Task<SyncJobResult> ExecuteAsync(CancellationToken cancellationToken = default)
    {
        var jobId = Guid.NewGuid().ToString("N");
        var startedAt = DateTime.UtcNow;

        _logger.LogInformation("Starting sync job {JobId}", jobId);

        // Create initial job record as InProgress
        var jobResult = new SyncJobResult(
            JobId: jobId,
            Status: JobStatus.InProgress.ToString(),
            RecordsFetched: 0,
            RecordsStored: 0,
            RecordsSkipped: 0,
            RecordsRejected: 0,
            StartedAt: startedAt,
            CompletedAt: startedAt);

        await _syncJobRepository.CreateAsync(jobResult, cancellationToken);

        try
        {
            // Get API configuration from DynamoDB
            var apiConfig = await GetApiConfigAsync(cancellationToken);

            if (apiConfig is null)
            {
                _logger.LogError("External API configuration not found in DynamoDB");
                jobResult = jobResult with
                {
                    Status = JobStatus.Failed.ToString(),
                    CompletedAt = DateTime.UtcNow
                };
                await _syncJobRepository.UpdateAsync(jobResult, cancellationToken);
                return jobResult;
            }

            // Fetch data from external API (with retry logic built into the client)
            ExternalApiResponse apiResponse;
            try
            {
                apiResponse = await _apiClient.FetchSalesDataAsync(apiConfig, cancellationToken);
            }
            catch (ExternalApiException ex)
            {
                _logger.LogError(ex, "External API fetch failed after all retries for job {JobId}", jobId);
                jobResult = jobResult with
                {
                    Status = JobStatus.Failed.ToString(),
                    CompletedAt = DateTime.UtcNow
                };
                await _syncJobRepository.UpdateAsync(jobResult, cancellationToken);
                return jobResult;
            }

            var recordsFetched = apiResponse.Records.Count;
            _logger.LogInformation("Fetched {RecordCount} records from external API for job {JobId}",
                recordsFetched, jobId);

            // Process records: validate, deduplicate, store
            var (recordsStored, recordsSkipped, recordsRejected) =
                await ProcessRecordsAsync(apiResponse.Records, cancellationToken);

            // Determine final status
            var status = DetermineJobStatus(recordsFetched, recordsStored, recordsRejected, recordsSkipped);

            jobResult = new SyncJobResult(
                JobId: jobId,
                Status: status,
                RecordsFetched: recordsFetched,
                RecordsStored: recordsStored,
                RecordsSkipped: recordsSkipped,
                RecordsRejected: recordsRejected,
                StartedAt: startedAt,
                CompletedAt: DateTime.UtcNow);

            await _syncJobRepository.UpdateAsync(jobResult, cancellationToken);

            _logger.LogInformation(
                "Sync job {JobId} completed with status {Status}: fetched={Fetched}, stored={Stored}, skipped={Skipped}, rejected={Rejected}",
                jobId, status, recordsFetched, recordsStored, recordsSkipped, recordsRejected);

            return jobResult;
        }
        catch (Exception ex) when (ex is not OperationCanceledException)
        {
            _logger.LogError(ex, "Unexpected error during sync job {JobId}", jobId);
            jobResult = jobResult with
            {
                Status = JobStatus.Failed.ToString(),
                CompletedAt = DateTime.UtcNow
            };

            try
            {
                await _syncJobRepository.UpdateAsync(jobResult, cancellationToken);
            }
            catch (Exception updateEx)
            {
                _logger.LogError(updateEx, "Failed to update job status for failed job {JobId}", jobId);
            }

            return jobResult;
        }
    }

    /// <summary>
    /// Processes a batch of records: validates each, deduplicates, and stores valid ones.
    /// </summary>
    private async Task<(int Stored, int Skipped, int Rejected)> ProcessRecordsAsync(
        List<ExternalSalesRecord> records,
        CancellationToken cancellationToken)
    {
        int stored = 0;
        int skipped = 0;
        int rejected = 0;

        // Track seen composite keys within this batch for in-batch deduplication
        var seenKeys = new HashSet<string>(StringComparer.OrdinalIgnoreCase);

        foreach (var record in records)
        {
            cancellationToken.ThrowIfCancellationRequested();

            // Step 1: Validate the record
            var rawTransaction = new RawTransaction(
                CustomerId: record.CustomerId,
                CustomerPhone: record.CustomerPhone,
                OutletId: record.OutletId,
                PurchaseDate: record.PurchaseDate,
                PurchaseAmount: record.PurchaseAmount);

            var validationResult = TransactionValidator.Validate(rawTransaction);

            if (!validationResult.IsValid)
            {
                _logger.LogWarning(
                    "Record rejected for customer {CustomerId}: {Errors}",
                    record.CustomerId ?? "unknown",
                    string.Join("; ", validationResult.Errors));
                rejected++;
                continue;
            }

            // Step 2: Parse validated fields
            var purchaseDate = ParseDate(record.PurchaseDate!);
            var purchaseAmount = decimal.Parse(record.PurchaseAmount!, NumberStyles.Number, CultureInfo.InvariantCulture);

            // Step 3: In-batch deduplication using composite key
            var compositeKey = BuildCompositeKey(record.CustomerId!, record.OutletId!, purchaseDate, purchaseAmount);

            if (!seenKeys.Add(compositeKey))
            {
                _logger.LogInformation(
                    "Duplicate record skipped within batch: {CompositeKey}", compositeKey);
                skipped++;
                continue;
            }

            // Step 4: Check for existing record in DynamoDB (cross-batch deduplication)
            var exists = await _purchaseRepository.ExistsAsync(
                record.CustomerId!, record.OutletId!, purchaseDate, purchaseAmount, cancellationToken);

            if (exists)
            {
                _logger.LogInformation(
                    "Duplicate record skipped (already in DB): {CompositeKey}", compositeKey);
                skipped++;
                continue;
            }

            // Step 5: Store the valid, non-duplicate record
            var purchase = new Purchase(
                CustomerId: record.CustomerId!,
                OutletId: record.OutletId!,
                PurchaseDate: purchaseDate,
                Amount: purchaseAmount,
                ProductCategory: record.ProductCategory ?? "Unknown",
                ProcessedAt: DateTime.UtcNow);

            var wasStored = await _purchaseRepository.StorePurchaseAsync(purchase, cancellationToken);

            if (wasStored)
            {
                stored++;

                // Upsert customer profile if we have customer info
                await UpsertCustomerIfNeededAsync(record, cancellationToken);
            }
            else
            {
                // StorePurchaseAsync returned false means it was a duplicate (race condition)
                skipped++;
            }
        }

        return (stored, skipped, rejected);
    }

    /// <summary>
    /// Creates or updates the customer profile when a new purchase is stored.
    /// </summary>
    private async Task UpsertCustomerIfNeededAsync(
        ExternalSalesRecord record,
        CancellationToken cancellationToken)
    {
        try
        {
            var (phoneResult, normalizedPhone) = PhoneNumberValidator.ValidateAndNormalize(record.CustomerPhone);
            var phone = phoneResult.IsValid ? normalizedPhone! : record.CustomerPhone ?? "";

            var existingCustomer = await _customerRepository.GetByIdAsync(record.CustomerId!, cancellationToken);

            if (existingCustomer is null)
            {
                // Get active cycle for the customer record
                var activeCycle = await _configRepository.GetActiveCycleAsync(cancellationToken);
                var cycleId = activeCycle?.CycleId ?? "default";

                var customer = new Customer(
                    CustomerId: record.CustomerId!,
                    Name: record.CustomerName ?? "Unknown",
                    PhoneNumber: phone,
                    QualifyingPurchases: 0,
                    CurrentCycleId: cycleId);

                await _customerRepository.UpsertAsync(customer, cancellationToken);
            }
        }
        catch (Exception ex)
        {
            // Don't fail the entire sync if customer upsert fails
            _logger.LogWarning(ex,
                "Failed to upsert customer {CustomerId} during sync",
                record.CustomerId);
        }
    }

    /// <summary>
    /// Gets the external API configuration from DynamoDB.
    /// </summary>
    private async Task<ExternalApiConfig?> GetApiConfigAsync(CancellationToken cancellationToken)
    {
        var generalConfig = await _configRepository.GetGeneralConfigAsync(cancellationToken);

        // The API endpoint and credentials are stored as a separate config item
        // For now, we read from environment variables as a fallback
        var endpoint = Environment.GetEnvironmentVariable("EXTERNAL_API_ENDPOINT");
        var apiKey = Environment.GetEnvironmentVariable("EXTERNAL_API_KEY");
        var lastCursor = Environment.GetEnvironmentVariable("EXTERNAL_API_LAST_CURSOR");

        if (string.IsNullOrWhiteSpace(endpoint))
        {
            return null;
        }

        return new ExternalApiConfig
        {
            Endpoint = endpoint,
            ApiKey = apiKey ?? string.Empty,
            LastSyncCursor = lastCursor
        };
    }

    /// <summary>
    /// Determines the final job status based on processing counts.
    /// </summary>
    internal static string DetermineJobStatus(int fetched, int stored, int rejected, int skipped)
    {
        if (fetched == 0)
            return JobStatus.Success.ToString();

        if (stored == 0 && rejected > 0)
            return JobStatus.Failed.ToString();

        if (rejected > 0)
            return JobStatus.Partial.ToString();

        return JobStatus.Success.ToString();
    }

    /// <summary>
    /// Builds a composite key for deduplication: customerId|outletId|date|amount.
    /// </summary>
    internal static string BuildCompositeKey(string customerId, string outletId, DateOnly date, decimal amount)
    {
        return $"{customerId}|{outletId}|{date:yyyy-MM-dd}|{amount:F2}";
    }

    /// <summary>
    /// Parses a date string to DateOnly. Supports ISO 8601 and common formats.
    /// </summary>
    private static DateOnly ParseDate(string dateString)
    {
        if (DateOnly.TryParse(dateString, CultureInfo.InvariantCulture, DateTimeStyles.None, out var dateOnly))
            return dateOnly;

        if (DateTime.TryParse(dateString, CultureInfo.InvariantCulture, DateTimeStyles.None, out var dateTime))
            return DateOnly.FromDateTime(dateTime);

        // This shouldn't happen since we validated the date earlier
        throw new FormatException($"Unable to parse date: {dateString}");
    }
}
