using VELoyalty.Core;
using VELoyalty.Data.Repositories;

namespace VELoyalty.Api.Services;

/// <summary>
/// Service for computing admin dashboard summary data including active customers,
/// pending redemptions, cycle status, and recent sync status.
/// </summary>
public class DashboardService
{
    private readonly CustomerRepository _customerRepository;
    private readonly VerificationCodeRepository _verificationCodeRepository;
    private readonly CycleRepository _cycleRepository;
    private readonly SyncJobRepository _syncJobRepository;

    public DashboardService(
        CustomerRepository customerRepository,
        VerificationCodeRepository verificationCodeRepository,
        CycleRepository cycleRepository,
        SyncJobRepository syncJobRepository)
    {
        _customerRepository = customerRepository;
        _verificationCodeRepository = verificationCodeRepository;
        _cycleRepository = cycleRepository;
        _syncJobRepository = syncJobRepository;
    }

    /// <summary>
    /// Gets the admin dashboard summary including active customers, pending redemptions,
    /// cycle status with days remaining, and recent sync status.
    /// </summary>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>Dashboard summary response.</returns>
    public async Task<DashboardSummaryResponse> GetDashboardSummaryAsync(CancellationToken cancellationToken = default)
    {
        var activeCycle = await _cycleRepository.GetActiveCycleAsync(cancellationToken);

        // Get cycle status
        CycleStatusResponse? cycleStatus = null;
        if (activeCycle is not null)
        {
            var today = DateOnly.FromDateTime(DateTime.UtcNow);
            var daysRemaining = activeCycle.EndDate.DayNumber - today.DayNumber;
            if (daysRemaining < 0) daysRemaining = 0;

            cycleStatus = new CycleStatusResponse(
                CycleId: activeCycle.CycleId,
                StartDate: activeCycle.StartDate,
                EndDate: activeCycle.EndDate,
                DaysRemaining: daysRemaining,
                IsActive: activeCycle.IsActive
            );
        }

        // Get recent sync status (last sync job)
        var recentSyncJobs = await _syncJobRepository.ListRecentAsync(limit: 1, cancellationToken: cancellationToken);
        SyncStatusResponse? recentSyncStatus = null;
        if (recentSyncJobs.Count > 0)
        {
            var lastJob = recentSyncJobs[0];
            recentSyncStatus = new SyncStatusResponse(
                JobId: lastJob.JobId,
                Status: lastJob.Status,
                RecordsFetched: lastJob.RecordsFetched,
                RecordsStored: lastJob.RecordsStored,
                RecordsSkipped: lastJob.RecordsSkipped,
                RecordsRejected: lastJob.RecordsRejected,
                StartedAt: lastJob.StartedAt,
                CompletedAt: lastJob.CompletedAt
            );
        }

        // Note: For active customers and pending redemptions, we use scan-based counts.
        // In a production system with large datasets, these would be maintained as counters.
        // For MVP, we query recent data to provide approximate counts.
        var activeCustomersCount = await GetActiveCustomersCountAsync(activeCycle, cancellationToken);
        var pendingRedemptionsCount = await GetPendingRedemptionsCountAsync(activeCycle, cancellationToken);

        return new DashboardSummaryResponse(
            ActiveCustomers: activeCustomersCount,
            PendingRedemptions: pendingRedemptionsCount,
            CycleStatus: cycleStatus,
            RecentSyncStatus: recentSyncStatus
        );
    }

    /// <summary>
    /// Gets the count of active customers (customers with qualifying purchases > 0 in current cycle).
    /// For MVP, this uses a scan with filter. In production, maintain a counter.
    /// </summary>
    private async Task<int> GetActiveCustomersCountAsync(LoyaltyCycle? activeCycle, CancellationToken cancellationToken)
    {
        if (activeCycle is null) return 0;

        // Query all customers and count those with qualifying purchases > 0
        // This is a simplified approach for MVP
        var customers = await _customerRepository.GetActiveCustomersInCycleAsync(
            activeCycle.CycleId, cancellationToken);
        return customers;
    }

    /// <summary>
    /// Gets the count of pending redemptions (active verification codes not yet redeemed).
    /// </summary>
    private async Task<int> GetPendingRedemptionsCountAsync(LoyaltyCycle? activeCycle, CancellationToken cancellationToken)
    {
        if (activeCycle is null) return 0;

        return await _verificationCodeRepository.CountActiveCodesAsync(cancellationToken);
    }
}

// ─── Dashboard Response DTOs ────────────────────────────────────────────────────

public record DashboardSummaryResponse(
    int ActiveCustomers,
    int PendingRedemptions,
    CycleStatusResponse? CycleStatus,
    SyncStatusResponse? RecentSyncStatus
);

public record CycleStatusResponse(
    string CycleId,
    DateOnly StartDate,
    DateOnly EndDate,
    int DaysRemaining,
    bool IsActive
);

public record SyncStatusResponse(
    string JobId,
    string Status,
    int RecordsFetched,
    int RecordsStored,
    int RecordsSkipped,
    int RecordsRejected,
    DateTime StartedAt,
    DateTime CompletedAt
);
