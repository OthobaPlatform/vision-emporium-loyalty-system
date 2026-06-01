using VELoyalty.Api.Services;
using VELoyalty.Auth;
using VELoyalty.Data.Repositories;

namespace VELoyalty.Api.Endpoints;

public static class DashboardEndpoints
{
    public static WebApplication MapDashboardEndpoints(this WebApplication app)
    {
        // GET /api/v1/dashboard
        app.MapGet("/api/v1/dashboard", async (
            DashboardService dashboardService,
            CancellationToken cancellationToken) =>
        {
            var summary = await dashboardService.GetDashboardSummaryAsync(cancellationToken);
            return Results.Ok(summary);
        }).RequireAdmin();

        // GET /api/v1/audit
        app.MapGet("/api/v1/audit", async (
            DateTime? startDate,
            DateTime? endDate,
            string? eventType,
            AuditRepository auditRepository,
            CancellationToken cancellationToken) =>
        {
            var from = startDate ?? DateTime.UtcNow.AddDays(-30);
            var to = endDate ?? DateTime.UtcNow;

            List<VELoyalty.Core.AuditEntry> entries;

            if (!string.IsNullOrWhiteSpace(eventType))
            {
                entries = await auditRepository.QueryByEventTypeAsync(
                    eventType, from, to, scanIndexForward: false, limit: 100, cancellationToken: cancellationToken);
            }
            else
            {
                entries = await auditRepository.QueryByTimeRangeAsync(
                    from, to, scanIndexForward: false, limit: 100, cancellationToken: cancellationToken);
            }

            var results = entries.Select(e => new AuditEntryResponse(
                Timestamp: e.Timestamp,
                EventType: e.EventType,
                ActorId: e.ActorId,
                EntityType: e.EntityType,
                EntityId: e.EntityId,
                Details: e.Details
            )).ToList();

            return Results.Ok(new { entries = results });
        }).RequireAdmin();

        return app;
    }
}
