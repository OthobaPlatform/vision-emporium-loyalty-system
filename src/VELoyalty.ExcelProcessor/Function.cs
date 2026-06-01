using System.Globalization;
using Amazon.Lambda.Core;
using Amazon.Lambda.S3Events;
using Amazon.S3;
using Amazon.S3.Model;
using ClosedXML.Excel;
using Microsoft.Extensions.Logging;
using VELoyalty.Core;
using VELoyalty.Core.Validation;
using VELoyalty.Data.Repositories;

namespace VELoyalty.ExcelProcessor;

/// <summary>
/// Lambda function handler for processing uploaded Excel (.xlsx) files from S3.
/// Triggered by S3 PutObject events on the uploads bucket.
/// Validates file size, row count, schema, and individual rows.
/// Deduplicates against existing records and writes valid records to DynamoDB.
/// Generates an import summary with rejected row details.
/// </summary>
public class Function
{
    private readonly IAmazonS3 _s3Client;
    private readonly PurchaseRepository _purchaseRepository;
    private readonly CustomerRepository _customerRepository;
    private readonly ImportJobRepository _importJobRepository;
    private readonly ILogger<Function> _logger;

    public Function(
        IAmazonS3 s3Client,
        PurchaseRepository purchaseRepository,
        CustomerRepository customerRepository,
        ImportJobRepository importJobRepository,
        ILogger<Function> logger)
    {
        _s3Client = s3Client;
        _purchaseRepository = purchaseRepository;
        _customerRepository = customerRepository;
        _importJobRepository = importJobRepository;
        _logger = logger;
    }

    /// <summary>
    /// Handles S3 PutObject events for uploaded .xlsx files.
    /// </summary>
    public async Task FunctionHandler(S3Event s3Event, ILambdaContext context)
    {
        foreach (var record in s3Event.Records)
        {
            var bucket = record.S3.Bucket.Name;
            var key = record.S3.Object.Key;

            _logger.LogInformation("Processing file: {Bucket}/{Key}", bucket, key);

            // Only process .xlsx files
            if (!key.EndsWith(".xlsx", StringComparison.OrdinalIgnoreCase))
            {
                _logger.LogWarning("Skipping non-xlsx file: {Key}", key);
                continue;
            }

            await ProcessFileAsync(bucket, key, record.S3.Object.Size, context.RemainingTime);
        }
    }

    private async Task ProcessFileAsync(string bucket, string key, long fileSize, TimeSpan remainingTime)
    {
        // Extract job ID from the S3 key (expected format: imports/{jobId}/{filename}.xlsx)
        var jobId = ExtractJobId(key);
        var fileName = Path.GetFileName(key);
        var startedAt = DateTime.UtcNow;

        // Create initial job record
        var job = new ImportJobResult(
            JobId: jobId,
            Status: JobStatus.InProgress.ToString(),
            FileName: fileName,
            TotalRows: 0,
            RecordsImported: 0,
            RecordsRejected: 0,
            RecordsSkipped: 0,
            RejectedRows: new List<RejectedRow>(),
            StartedAt: startedAt,
            CompletedAt: DateTime.UtcNow
        );

        try
        {
            await _importJobRepository.CreateAsync(job);

            // Validate file size (max 10MB)
            if (fileSize > Constants.MaxExcelFileSizeBytes)
            {
                _logger.LogWarning("File {Key} exceeds maximum size of {MaxSize} bytes. Actual: {ActualSize}",
                    key, Constants.MaxExcelFileSizeBytes, fileSize);

                await UpdateJobAsFailed(job, $"File exceeds maximum size of 10MB. Actual size: {fileSize / (1024.0 * 1024.0):F2}MB");
                return;
            }

            // Download file from S3
            using var response = await _s3Client.GetObjectAsync(new GetObjectRequest
            {
                BucketName = bucket,
                Key = key
            });

            using var memoryStream = new MemoryStream();
            await response.ResponseStream.CopyToAsync(memoryStream);
            memoryStream.Position = 0;

            // Parse and process the Excel file
            await ProcessExcelAsync(memoryStream, job);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error processing file {Bucket}/{Key}", bucket, key);
            await UpdateJobAsFailed(job, $"Unexpected error: {ex.Message}");
        }
    }

    private async Task ProcessExcelAsync(MemoryStream fileStream, ImportJobResult initialJob)
    {
        using var workbook = new XLWorkbook(fileStream);
        var worksheet = workbook.Worksheets.First();

        // Get the used range
        var lastRow = worksheet.LastRowUsed()?.RowNumber() ?? 0;
        var lastCol = worksheet.LastColumnUsed()?.ColumnNumber() ?? 0;

        if (lastRow <= 1)
        {
            await UpdateJobAsFailed(initialJob, "File contains no data rows (only header or empty).");
            return;
        }

        var dataRowCount = lastRow - 1; // Subtract header row

        // Validate row count (max 100,000)
        if (dataRowCount > Constants.MaxExcelRowCount)
        {
            _logger.LogWarning("File exceeds maximum row count of {MaxRows}. Actual: {ActualRows}",
                Constants.MaxExcelRowCount, dataRowCount);

            await UpdateJobAsFailed(initialJob,
                $"File exceeds maximum row count of {Constants.MaxExcelRowCount:N0}. Actual: {dataRowCount:N0} rows.");
            return;
        }

        // Read and validate column headers (row 1)
        var headers = new List<string>();
        for (int col = 1; col <= lastCol; col++)
        {
            var cellValue = worksheet.Cell(1, col).GetString()?.Trim() ?? string.Empty;
            headers.Add(cellValue);
        }

        // Validate schema (required columns)
        var schemaResult = ExcelSchemaValidator.ValidateColumns(headers);
        if (!schemaResult.IsValid)
        {
            var errorMessage = string.Join("; ", schemaResult.Errors);
            await UpdateJobAsFailed(initialJob, $"Schema validation failed: {errorMessage}");
            return;
        }

        // Build column index map (case-insensitive)
        var columnMap = BuildColumnMap(headers);

        // Process data rows
        var recordsImported = 0;
        var recordsRejected = 0;
        var recordsSkipped = 0;
        var rejectedRows = new List<RejectedRow>();

        for (int rowNum = 2; rowNum <= lastRow; rowNum++)
        {
            var rowData = ReadRow(worksheet, rowNum, columnMap);

            // Validate row against schema
            var rowValidation = ExcelSchemaValidator.ValidateRow(rowData, rowNum);
            if (!rowValidation.IsValid)
            {
                recordsRejected++;
                var reason = string.Join("; ", rowValidation.Errors);
                rejectedRows.Add(new RejectedRow(rowNum, reason));
                continue;
            }

            // Extract values for deduplication and storage
            var customerId = rowData["customer identifier"]!.Trim();
            var customerName = rowData["customer name"]!.Trim();
            var phoneRaw = rowData["customer phone number"]!.Trim();
            var outletId = rowData["outlet identifier"]!.Trim();
            var dateStr = rowData["purchase date"]!.Trim();
            var amountStr = rowData["purchase amount"]!.Trim();
            var category = rowData["product category"]!.Trim();
            var challanNo = rowData["challan_no"]?.Trim() ?? rowData["challan no"]?.Trim() ?? "";
            var itemId = rowData["item_id"]?.Trim() ?? rowData["item id"]?.Trim();

            // Parse date and amount (already validated by ExcelSchemaValidator)
            var purchaseDate = ParseDate(dateStr);
            var amount = decimal.Parse(amountStr, NumberStyles.Number, CultureInfo.InvariantCulture);

            // Normalize phone number
            var normalizedPhone = PhoneNumberValidator.Normalize(phoneRaw);

            // Generate synthetic challan if not provided
            if (string.IsNullOrWhiteSpace(challanNo))
            {
                challanNo = $"IMPORT-{customerId}-{outletId}-{purchaseDate:yyyyMMdd}-{amount:F2}";
            }

            // Deduplication check: challan + item
            var isDuplicate = await _purchaseRepository.ExistsByChallanAsync(
                customerId, challanNo, itemId);

            if (isDuplicate)
            {
                recordsSkipped++;
                continue;
            }

            // Store the purchase record
            var purchase = new Purchase(
                CustomerId: customerId,
                OutletId: outletId,
                PurchaseDate: purchaseDate,
                Amount: amount,
                ProductCategory: category,
                ProcessedAt: DateTime.UtcNow,
                ChallanNo: challanNo,
                ItemId: itemId
            );

            var stored = await _purchaseRepository.StorePurchaseAsync(purchase);
            if (!stored)
            {
                // Race condition: another process stored it between our check and write
                recordsSkipped++;
                continue;
            }

            recordsImported++;

            // Upsert customer profile
            await UpsertCustomerAsync(customerId, customerName, normalizedPhone);
        }

        // Generate final import summary
        var completedJob = initialJob with
        {
            Status = recordsRejected > 0 ? JobStatus.Partial.ToString() : JobStatus.Success.ToString(),
            TotalRows = dataRowCount,
            RecordsImported = recordsImported,
            RecordsRejected = recordsRejected,
            RecordsSkipped = recordsSkipped,
            RejectedRows = rejectedRows,
            CompletedAt = DateTime.UtcNow
        };

        await _importJobRepository.UpdateAsync(completedJob);

        _logger.LogInformation(
            "Import complete for job {JobId}: Total={Total}, Imported={Imported}, Rejected={Rejected}, Skipped={Skipped}",
            completedJob.JobId, completedJob.TotalRows, completedJob.RecordsImported,
            completedJob.RecordsRejected, completedJob.RecordsSkipped);
    }

    private async Task UpsertCustomerAsync(string customerId, string customerName, string normalizedPhone)
    {
        try
        {
            var existingCustomer = await _customerRepository.GetByIdAsync(customerId);
            if (existingCustomer is null)
            {
                // Create new customer profile
                var customer = new Customer(
                    CustomerId: customerId,
                    Name: customerName,
                    PhoneNumber: normalizedPhone,
                    QualifyingPurchases: 0,
                    CurrentCycleId: string.Empty
                );
                await _customerRepository.UpsertAsync(customer);
            }
            else
            {
                // Update name and phone if changed
                if (existingCustomer.Name != customerName || existingCustomer.PhoneNumber != normalizedPhone)
                {
                    var updated = existingCustomer with
                    {
                        Name = customerName,
                        PhoneNumber = normalizedPhone
                    };
                    await _customerRepository.UpsertAsync(updated);
                }
            }
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Failed to upsert customer {CustomerId}, continuing with import", customerId);
        }
    }

    private async Task UpdateJobAsFailed(ImportJobResult job, string reason)
    {
        var failedJob = job with
        {
            Status = JobStatus.Failed.ToString(),
            RejectedRows = new List<RejectedRow> { new(0, reason) },
            CompletedAt = DateTime.UtcNow
        };

        await _importJobRepository.UpdateAsync(failedJob);
        _logger.LogWarning("Import job {JobId} failed: {Reason}", job.JobId, reason);
    }

    private static string ExtractJobId(string key)
    {
        // Expected format: imports/{jobId}/{filename}.xlsx
        var parts = key.Split('/');
        if (parts.Length >= 2)
            return parts[1];

        // Fallback: use a generated ID based on the key
        return Guid.NewGuid().ToString("N")[..12];
    }

    private static Dictionary<string, int> BuildColumnMap(List<string> headers)
    {
        var map = new Dictionary<string, int>(StringComparer.OrdinalIgnoreCase);
        for (int i = 0; i < headers.Count; i++)
        {
            var normalized = headers[i].Trim().ToLowerInvariant();
            if (!string.IsNullOrEmpty(normalized))
                map[normalized] = i + 1; // 1-based column index for ClosedXML
        }
        return map;
    }

    private static IReadOnlyDictionary<string, string?> ReadRow(
        IXLWorksheet worksheet, int rowNumber, Dictionary<string, int> columnMap)
    {
        var row = new Dictionary<string, string?>(StringComparer.OrdinalIgnoreCase);

        foreach (var (columnName, colIndex) in columnMap)
        {
            var cell = worksheet.Cell(rowNumber, colIndex);
            var value = cell.IsEmpty() ? null : cell.GetString()?.Trim();
            row[columnName] = string.IsNullOrWhiteSpace(value) ? null : value;
        }

        return row;
    }

    private static DateOnly ParseDate(string dateStr)
    {
        if (DateOnly.TryParse(dateStr, CultureInfo.InvariantCulture, DateTimeStyles.None, out var dateOnly))
            return dateOnly;

        if (DateTime.TryParse(dateStr, CultureInfo.InvariantCulture, DateTimeStyles.None, out var dateTime))
            return DateOnly.FromDateTime(dateTime);

        // Should not reach here since ExcelSchemaValidator already validated the date
        return DateOnly.FromDateTime(DateTime.UtcNow);
    }
}
