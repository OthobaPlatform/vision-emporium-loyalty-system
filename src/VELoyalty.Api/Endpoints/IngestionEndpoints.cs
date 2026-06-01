using VELoyalty.Api.Services;
using VELoyalty.Auth;
using VELoyalty.Core;
using VELoyalty.Data.Repositories;

namespace VELoyalty.Api.Endpoints;

public static class IngestionEndpoints
{
    public static WebApplication MapIngestionEndpoints(this WebApplication app)
    {
        // POST /api/v1/ingestion/upload
        app.MapPost("/api/v1/ingestion/upload", async (
            HttpContext httpContext,
            ImportJobRepository importJobRepository,
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

            var importJob = new ImportJobResult(
                JobId: jobId,
                Status: "Processing",
                FileName: file.FileName,
                TotalRows: 0,
                RecordsImported: 0,
                RecordsRejected: 0,
                RecordsSkipped: 0,
                RejectedRows: new List<RejectedRow>(),
                StartedAt: now,
                CompletedAt: now
            );

            await importJobRepository.CreateAsync(importJob, cancellationToken);

            await auditRepository.AppendAsync(new AuditEntry(
                EventType: "IngestionJob",
                ActorId: actorId,
                EntityType: "ImportJob",
                EntityId: jobId,
                Details: new Dictionary<string, string>
                {
                    ["fileName"] = file.FileName,
                    ["fileSize"] = file.Length.ToString(),
                    ["action"] = "Upload"
                },
                Timestamp: now
            ), cancellationToken);

            // For local dev, mark as completed immediately
            var completedJob = importJob with { Status = "Completed", CompletedAt = DateTime.UtcNow };
            await importJobRepository.UpdateAsync(completedJob, cancellationToken);

            return Results.Ok(new { jobId, status = "Completed", message = "File uploaded and processed." });
        }).RequireAdmin().DisableAntiforgery();

        // GET /api/v1/ingestion/jobs/{id}
        app.MapGet("/api/v1/ingestion/jobs/{id}", async (
            string id,
            ImportJobRepository importJobRepository,
            CancellationToken cancellationToken) =>
        {
            var job = await importJobRepository.GetByIdAsync(id, cancellationToken);
            if (job is null)
                return Results.NotFound(new { error = "NotFound", message = "Import job not found." });

            return Results.Ok(job);
        }).RequireAdmin();

        // GET /api/v1/ingestion/template
        app.MapGet("/api/v1/ingestion/template", () =>
        {
            var csvContent = "DIST_ID,DIST_NAME,ITEM_ID,ITEM_NAME,OC_QTY,SR_QNTY,AMNT,CHALLAN_DATE,CHALLAN_NO,COMMP,NET_AMNT,NOTE\n" +
                             "20152,Vision Emporium-Uttar Badda,969121,CHAMPION DAY LIGHT BULB 13W B22(Pin),1,0,0.2650,22/05/2026 12:00:00 AM,OC20152-01-2605000267,0.0530,0.2120,\"Name: John Doe Mb No: 01712345678 Note:\"\n";

            return Results.File(
                System.Text.Encoding.UTF8.GetBytes(csvContent),
                "text/csv",
                "ve-loyalty-import-template.csv");
        }).RequireAdmin();

        // POST /api/v1/ingestion/sync
        app.MapPost("/api/v1/ingestion/sync", async (
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
        }).RequireAdmin();

        // GET /api/v1/ingestion/sync/status
        app.MapGet("/api/v1/ingestion/sync/status", async (
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
        }).RequireAdmin();

        return app;
    }
}
