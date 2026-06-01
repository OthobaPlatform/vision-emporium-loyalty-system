using Asp.Versioning;
using VELoyalty.Api.Services;
using VELoyalty.Auth;
using VELoyalty.Data.Repositories;

namespace VELoyalty.Api.Endpoints;

public static class DashboardEndpoints
{
    public static RouteGroupBuilder MapDashboardEndpoints(this RouteGroupBuilder group)
    {
        // GET /dashboard
        group.MapGet("/dashboard", async (
            DashboardService dashboardService,
            CancellationToken cancellationToken) =>
        {
            var summary = await dashboardService.GetDashboardSummaryAsync(cancellationToken);
            return Results.Ok(summary);
        }).RequireAdmin().MapToApiVersion(1, 0);

        // GET /audit
        group.MapGet("/audit", async (
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
        }).RequireAdmin().MapToApiVersion(1, 0);

        return group;
    }
}
