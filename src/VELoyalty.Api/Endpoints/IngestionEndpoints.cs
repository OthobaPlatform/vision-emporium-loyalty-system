using System.Globalization;
using Asp.Versioning;
using VELoyalty.Api.Services;
using VELoyalty.Auth;
using VELoyalty.Core;
using VELoyalty.Core.Validation;
using VELoyalty.Data.Repositories;

namespace VELoyalty.Api.Endpoints;

public static class IngestionEndpoints
{
    public static RouteGroupBuilder MapIngestionEndpoints(this RouteGroupBuilder group)
    {
        // POST /ingestion/upload
        group.MapPost("/ingestion/upload", async (
            HttpContext httpContext,
            ImportJobRepository importJobRepository,
            PurchaseRepository purchaseRepository,
            CustomerRepository customerRepository,
            AuditRepository auditRepository,
            CancellationToken cancellationToken) =>
        {
            var form = await httpContext.Request.ReadFormAsync(cancellationToken);
            var file = form.Files.GetFile("file");

            if (file is null || file.Length == 0)
                return Results.BadRequest(new { error = "ValidationError", message = "No file provided." });

            if (!file.FileName.EndsWith(".xlsx", StringComparison.OrdinalIgnoreCase) &&
                !file.FileName.EndsWith(".csv", StringComparison.OrdinalIgnoreCase))
                return Results.BadRequest(new { error = "ValidationError", message = "Only .xlsx and .csv files are supported." });

            if (file.Length > 10 * 1024 * 1024)
                return Results.BadRequest(new { error = "ValidationError", message = "File size must not exceed 10MB." });

            var jobId = Guid.NewGuid().ToString("N")[..12];
            var now = DateTime.UtcNow;
            var actorId = httpContext.GetUserId() ?? "admin";

            // Parse CSV and process rows
            var totalRows = 0;
            var recordsImported = 0;
            var recordsSkipped = 0;
            var recordsRejected = 0;
            var rejectedRows = new List<RejectedRow>();

            using var reader = new StreamReader(file.OpenReadStream());
            var headerLine = await reader.ReadLineAsync(cancellationToken);

            if (string.IsNullOrWhiteSpace(headerLine))
            {
                return Results.BadRequest(new { error = "ValidationError", message = "File is empty or has no header row." });
            }

            var headers = ParseCsvLine(headerLine);
            var columnValidation = ExcelSchemaValidator.ValidateColumns(headers);
            if (!columnValidation.IsValid)
            {
                return Results.BadRequest(new { error = "ValidationError", message = string.Join("; ", columnValidation.Errors) });
            }

            var seenChallans = new HashSet<string>();

            while (!reader.EndOfStream)
            {
                var line = await reader.ReadLineAsync(cancellationToken);
                if (string.IsNullOrWhiteSpace(line)) continue;

                totalRows++;
                var values = ParseCsvLine(line);
                var row = MapToRow(headers, values);

                // Validate row
                var rowValidation = ExcelSchemaValidator.ValidateRow(row, totalRows);
                if (!rowValidation.IsValid)
                {
                    recordsRejected++;
                    rejectedRows.Add(new RejectedRow(totalRows, string.Join("; ", rowValidation.Errors)));
                    continue;
                }

                // Extract fields
                var distId = GetVal(row, "DIST_ID") ?? "";
                var itemId = GetVal(row, "ITEM_ID") ?? "";
                var itemName = GetVal(row, "ITEM_NAME") ?? "";
                var challanNo = GetVal(row, "CHALLAN_NO") ?? "";
                var note = GetVal(row, "NOTE") ?? "";
                var netAmntStr = GetVal(row, "NET_AMNT") ?? "0";
                var dateStr = GetVal(row, "CHALLAN_DATE") ?? "";
                var qtyStr = GetVal(row, "OC_QTY") ?? "1";

                // Parse values
                decimal.TryParse(netAmntStr, NumberStyles.Number, CultureInfo.InvariantCulture, out var netAmnt);
                var amountBdt = ExcelSchemaValidator.ConvertFromThousands(netAmnt);
                ExcelSchemaValidator.TryParseChallanDate(dateStr, out var purchaseDate);
                int.TryParse(qtyStr, out var qty);
                if (qty < 1) qty = 1;

                // Extract customer identity from NOTE
                var phone = ExcelSchemaValidator.ExtractPhoneNumber(note);
                var customerName = ExcelSchemaValidator.ExtractCustomerName(note);
                var staffId = ExcelSchemaValidator.ExtractStaffId(note);
                var customerId = phone ?? (staffId != null ? $"STAFF-{staffId}" : $"UNKNOWN-{challanNo}");

                // Dedup by challan+item
                var dedupKey = $"{challanNo}#{itemId}";
                if (seenChallans.Contains(dedupKey))
                {
                    recordsSkipped++;
                    continue;
                }
                seenChallans.Add(dedupKey);

                // Store purchase record
                var purchase = new Purchase(
                    CustomerId: customerId,
                    OutletId: distId,
                    PurchaseDate: purchaseDate,
                    Amount: amountBdt,
                    ProductCategory: itemName,
                    ProcessedAt: now,
                    ChallanNo: challanNo,
                    ItemId: itemId,
                    Quantity: qty
                );

                var stored = await purchaseRepository.StorePurchaseAsync(purchase, cancellationToken);
                if (stored)
                    recordsImported++;
                else
                    recordsSkipped++; // Already exists in DB
            }

            // Save job result
            var completedJob = new ImportJobResult(
                JobId: jobId,
                Status: "Completed",
                FileName: file.FileName,
                TotalRows: totalRows,
                RecordsImported: recordsImported,
                RecordsRejected: recordsRejected,
                RecordsSkipped: recordsSkipped,
                RejectedRows: rejectedRows,
                StartedAt: now,
                CompletedAt: DateTime.UtcNow
            );

            await importJobRepository.CreateAsync(completedJob, cancellationToken);

            await auditRepository.AppendAsync(new AuditEntry(
                EventType: "IngestionJob",
                ActorId: actorId,
                EntityType: "ImportJob",
                EntityId: jobId,
                Details: new Dictionary<string, string>
                {
                    ["fileName"] = file.FileName,
                    ["totalRows"] = totalRows.ToString(),
                    ["imported"] = recordsImported.ToString(),
                    ["skipped"] = recordsSkipped.ToString(),
                    ["rejected"] = recordsRejected.ToString()
                },
                Timestamp: DateTime.UtcNow
            ), cancellationToken);

            return Results.Ok(new { jobId, status = "Completed", message = $"Processed {totalRows} rows: {recordsImported} imported, {recordsSkipped} skipped, {recordsRejected} rejected." });
        }).RequireAdmin().DisableAntiforgery().MapToApiVersion(1, 0);

        // GET /ingestion/jobs/{id}
        group.MapGet("/ingestion/jobs/{id}", async (
            string id,
            ImportJobRepository importJobRepository,
            CancellationToken cancellationToken) =>
        {
            var job = await importJobRepository.GetByIdAsync(id, cancellationToken);
            if (job is null)
                return Results.NotFound(new { error = "NotFound", message = "Import job not found." });

            return Results.Ok(job);
        }).RequireAdmin().MapToApiVersion(1, 0);

        // GET /ingestion/template
        group.MapGet("/ingestion/template", () =>
        {
            var csvContent = "DIST_ID,DIST_NAME,ITEM_ID,ITEM_NAME,OC_QTY,SR_QNTY,AMNT,CHALLAN_DATE,CHALLAN_NO,COMMP,NET_AMNT,NOTE\n" +
                             "20152,Vision Emporium-Uttar Badda,969121,CHAMPION DAY LIGHT BULB 13W B22(Pin),1,0,0.2650,22/05/2026 12:00:00 AM,OC20152-01-2605000267,0.0530,0.2120,\"Name: John Doe Mb No: 01712345678 Note:\"\n";

            return Results.File(
                System.Text.Encoding.UTF8.GetBytes(csvContent),
                "text/csv",
                "ve-loyalty-import-template.csv");
        }).RequireAdmin().MapToApiVersion(1, 0);

        // POST /ingestion/sync
        group.MapPost("/ingestion/sync", async (
            HttpContext httpContext,
            SyncJobRepository syncJobRepository,
            AuditRepository auditRepository,
            CancellationToken cancellationToken) =>
        {
            var jobId = Guid.NewGuid().ToString("N")[..12];
            var now = DateTime.UtcNow;
            var actorId = httpContext.GetUserId() ?? "admin";

            var syncJob = new SyncJobResult(
                JobId: jobId,
                Status: "InProgress",
                RecordsFetched: 0,
                RecordsStored: 0,
                RecordsSkipped: 0,
                RecordsRejected: 0,
                StartedAt: now,
                CompletedAt: now
            );

            await syncJobRepository.CreateAsync(syncJob, cancellationToken);

            await auditRepository.AppendAsync(new AuditEntry(
                EventType: "IngestionJob",
                ActorId: actorId,
                EntityType: "SyncJob",
                EntityId: jobId,
                Details: new Dictionary<string, string>
                {
                    ["action"] = "ManualTrigger",
                    ["jobType"] = "API"
                },
                Timestamp: now
            ), cancellationToken);

            return Results.Accepted($"/api/v1/ingestion/sync/status", new TriggerSyncResponse(
                JobId: jobId,
                Status: "InProgress",
                Message: "Sync job has been triggered successfully."
            ));
        }).RequireAdmin().MapToApiVersion(1, 0);

        // GET /ingestion/sync/status
        group.MapGet("/ingestion/sync/status", async (
            SyncJobRepository syncJobRepository,
            CancellationToken cancellationToken) =>
        {
            var jobs = await syncJobRepository.ListRecentAsync(limit: 20, cancellationToken: cancellationToken);

            var results = jobs.Select(j => new SyncJobHistoryResponse(
                JobId: j.JobId,
                Status: j.Status,
                RecordsFetched: j.RecordsFetched,
                RecordsStored: j.RecordsStored,
                RecordsSkipped: j.RecordsSkipped,
                RecordsRejected: j.RecordsRejected,
                StartedAt: j.StartedAt,
                CompletedAt: j.CompletedAt
            )).ToList();

            return Results.Ok(new { jobs = results });
        }).RequireAdmin().MapToApiVersion(1, 0);

        return group;
    }

    /// <summary>
    /// Parses a CSV line handling quoted fields with commas inside.
    /// </summary>
    private static List<string> ParseCsvLine(string line)
    {
        var fields = new List<string>();
        var current = "";
        var inQuotes = false;

        for (int i = 0; i < line.Length; i++)
        {
            var c = line[i];
            if (c == '"')
            {
                if (inQuotes && i + 1 < line.Length && line[i + 1] == '"')
                {
                    current += '"';
                    i++; // skip escaped quote
                }
                else
                {
                    inQuotes = !inQuotes;
                }
            }
            else if (c == ',' && !inQuotes)
            {
                fields.Add(current.Trim());
                current = "";
            }
            else
            {
                current += c;
            }
        }
        fields.Add(current.Trim());
        return fields;
    }

    /// <summary>
    /// Maps header names to row values as a dictionary.
    /// </summary>
    private static Dictionary<string, string?> MapToRow(List<string> headers, List<string> values)
    {
        var row = new Dictionary<string, string?>(StringComparer.OrdinalIgnoreCase);
        for (int i = 0; i < headers.Count; i++)
        {
            var value = i < values.Count ? values[i] : null;
            row[headers[i]] = string.IsNullOrWhiteSpace(value) ? null : value;
        }
        return row;
    }

    /// <summary>
    /// Gets a value from the row dictionary (case-insensitive).
    /// </summary>
    private static string? GetVal(Dictionary<string, string?> row, string key)
    {
        return row.TryGetValue(key, out var val) ? val : null;
    }
}
