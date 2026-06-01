import { useEffect, useState } from 'react';
import { useSearchParams } from 'react-router-dom';
import { Helmet } from 'react-helmet-async';
import { apiClient, ApiError } from '../utils/api';
import { LoadingIndicator } from '../components/LoadingIndicator';

interface CycleStatus {
  cycleId: string;
  startDate: string;
  endDate: string;
  daysRemaining: number;
  isActive: boolean;
}

interface RecentSyncStatus {
  jobId: string;
  status: string;
  recordsFetched: number;
  recordsStored: number;
  recordsSkipped: number;
  recordsRejected: number;
  startedAt: string;
  completedAt: string | null;
}

interface DashboardData {
  activeCustomers: number;
  pendingRedemptions: number;
  cycleStatus: CycleStatus;
  recentSyncStatus: RecentSyncStatus | null;
}

export function DashboardPage() {
  const [searchParams, setSearchParams] = useSearchParams();
  const [showUnauthorized, setShowUnauthorized] = useState(false);
  const [data, setData] = useState<DashboardData | null>(null);
  const [isLoading, setIsLoading] = useState(true);
  const [error, setError] = useState<string | null>(null);

  useEffect(() => {
    if (searchParams.get('unauthorized') === 'true') {
      setShowUnauthorized(true);
      setSearchParams({}, { replace: true });
      const timer = setTimeout(() => setShowUnauthorized(false), 5000);
      return () => clearTimeout(timer);
    }
  }, [searchParams, setSearchParams]);

  useEffect(() => {
    async function fetchDashboard() {
      try {
        setIsLoading(true);
        setError(null);
        const result = await apiClient.get<DashboardData>('/dashboard');
        setData(result);
      } catch (err) {
        if (err instanceof ApiError) {
          setError(err.message);
        } else {
          setError('Failed to load dashboard data');
        }
      } finally {
        setIsLoading(false);
      }
    }
    fetchDashboard();
  }, []);

  function formatDate(dateStr: string): string {
    return new Date(dateStr).toLocaleDateString('en-US', {
      year: 'numeric',
      month: 'short',
      day: 'numeric',
    });
  }

  function formatDateTime(dateStr: string): string {
    return new Date(dateStr).toLocaleString('en-US', {
      year: 'numeric',
      month: 'short',
      day: 'numeric',
      hour: '2-digit',
      minute: '2-digit',
    });
  }

  return (
    <div>
      <Helmet>
        <title>Dashboard | Vision Emporium Loyalty</title>
      </Helmet>

      {showUnauthorized && (
        <div
          className="mb-6 rounded-md bg-yellow-50 border border-yellow-200 p-4"
          role="alert"
          aria-live="polite"
        >
          <p className="text-sm text-yellow-800">
            You do not have permission to access the requested page. You have been redirected to your landing page.
          </p>
        </div>
      )}

      <h1 className="text-2xl font-bold text-gray-900 mb-6">Dashboard</h1>

      <LoadingIndicator isLoading={isLoading} />

      {error && (
        <div className="rounded-md bg-red-50 border border-red-200 p-4" role="alert">
          <p className="text-sm text-red-800">{error}</p>
        </div>
      )}

      {data && !isLoading && (
        <div className="space-y-6">
          {/* Stats Cards */}
          <div className="grid grid-cols-1 gap-4 sm:grid-cols-2 lg:grid-cols-4">
            {/* Cycle Status */}
            <div className="rounded-lg bg-white p-6 shadow border border-gray-200">
              <div className="flex items-center justify-between">
                <h3 className="text-sm font-medium text-gray-500">Cycle Status</h3>
                <span
                  className={`inline-flex items-center rounded-full px-2.5 py-0.5 text-xs font-medium ${
                    data.cycleStatus.isActive
                      ? 'bg-green-100 text-green-800'
                      : 'bg-gray-100 text-gray-800'
                  }`}
                >
                  {data.cycleStatus.isActive ? 'Active' : 'Inactive'}
                </span>
              </div>
              <p className="mt-2 text-2xl font-bold text-gray-900">
                {data.cycleStatus.daysRemaining} days
              </p>
              <p className="mt-1 text-xs text-gray-500">remaining in cycle</p>
            </div>

            {/* Active Customers */}
            <div className="rounded-lg bg-white p-6 shadow border border-gray-200">
              <h3 className="text-sm font-medium text-gray-500">Active Customers</h3>
              <p className="mt-2 text-2xl font-bold text-gray-900">
                {data.activeCustomers.toLocaleString()}
              </p>
              <p className="mt-1 text-xs text-gray-500">in current cycle</p>
            </div>

            {/* Pending Redemptions */}
            <div className="rounded-lg bg-white p-6 shadow border border-gray-200">
              <h3 className="text-sm font-medium text-gray-500">Pending Redemptions</h3>
              <p className="mt-2 text-2xl font-bold text-gray-900">
                {data.pendingRedemptions.toLocaleString()}
              </p>
              <p className="mt-1 text-xs text-gray-500">awaiting claim</p>
            </div>

            {/* Recent Sync */}
            <div className="rounded-lg bg-white p-6 shadow border border-gray-200">
              <h3 className="text-sm font-medium text-gray-500">Last Sync</h3>
              {data.recentSyncStatus ? (
                <>
                  <p className="mt-2 text-2xl font-bold text-gray-900 capitalize">
                    {data.recentSyncStatus.status.toLowerCase()}
                  </p>
                  <p className="mt-1 text-xs text-gray-500">
                    {data.recentSyncStatus.completedAt
                      ? formatDateTime(data.recentSyncStatus.completedAt)
                      : 'In progress'}
                  </p>
                </>
              ) : (
                <>
                  <p className="mt-2 text-2xl font-bold text-gray-400">—</p>
                  <p className="mt-1 text-xs text-gray-500">No sync data</p>
                </>
              )}
            </div>
          </div>

          {/* Cycle Details */}
          <div className="rounded-lg bg-white p-6 shadow border border-gray-200">
            <h2 className="text-lg font-semibold text-gray-900 mb-4">Loyalty Cycle Details</h2>
            <div className="grid grid-cols-1 gap-4 sm:grid-cols-3">
              <div>
                <p className="text-sm text-gray-500">Start Date</p>
                <p className="text-sm font-medium text-gray-900">
                  {formatDate(data.cycleStatus.startDate)}
                </p>
              </div>
              <div>
                <p className="text-sm text-gray-500">End Date</p>
                <p className="text-sm font-medium text-gray-900">
                  {formatDate(data.cycleStatus.endDate)}
                </p>
              </div>
              <div>
                <p className="text-sm text-gray-500">Cycle ID</p>
                <p className="text-sm font-medium text-gray-900">{data.cycleStatus.cycleId}</p>
              </div>
            </div>
          </div>

          {/* Recent Sync Details */}
          {data.recentSyncStatus && (
            <div className="rounded-lg bg-white p-6 shadow border border-gray-200">
              <h2 className="text-lg font-semibold text-gray-900 mb-4">Recent Sync Summary</h2>
              <div className="grid grid-cols-2 gap-4 sm:grid-cols-4">
                <div>
                  <p className="text-sm text-gray-500">Records Fetched</p>
                  <p className="text-lg font-semibold text-gray-900">
                    {data.recentSyncStatus.recordsFetched}
                  </p>
                </div>
                <div>
                  <p className="text-sm text-gray-500">Records Stored</p>
                  <p className="text-lg font-semibold text-green-700">
                    {data.recentSyncStatus.recordsStored}
                  </p>
                </div>
                <div>
                  <p className="text-sm text-gray-500">Records Skipped</p>
                  <p className="text-lg font-semibold text-yellow-700">
                    {data.recentSyncStatus.recordsSkipped}
                  </p>
                </div>
                <div>
                  <p className="text-sm text-gray-500">Records Rejected</p>
                  <p className="text-lg font-semibold text-red-700">
                    {data.recentSyncStatus.recordsRejected}
                  </p>
                </div>
              </div>
              <div className="mt-4 pt-4 border-t border-gray-100">
                <p className="text-xs text-gray-500">
                  Started: {formatDateTime(data.recentSyncStatus.startedAt)}
                  {data.recentSyncStatus.completedAt &&
                    ` • Completed: ${formatDateTime(data.recentSyncStatus.completedAt)}`}
                </p>
              </div>
            </div>
          )}
        </div>
      )}
    </div>
  );
}
