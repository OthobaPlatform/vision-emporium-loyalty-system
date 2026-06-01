import { useEffect, useState, useCallback } from 'react';
import { Helmet } from 'react-helmet-async';
import { apiClient, ApiError } from '../utils/api';
import { useToast } from '../components/Toast';
import { LoadingIndicator } from '../components/LoadingIndicator';
import { DataTable, type Column } from '../components/DataTable';

interface SyncJob {
  jobId: string;
  status: string;
  recordsFetched: number;
  recordsStored: number;
  recordsSkipped: number;
  recordsRejected: number;
  startedAt: string;
  completedAt: string | null;
}

export function SyncStatusPage() {
  const { showToast } = useToast();
  const [jobs, setJobs] = useState<SyncJob[]>([]);
  const [isLoading, setIsLoading] = useState(true);
  const [error, setError] = useState<string | null>(null);
  const [isSyncing, setIsSyncing] = useState(false);

  // Pagination
  const [currentPage, setCurrentPage] = useState(1);
  const [pageSize, setPageSize] = useState(10);

  const fetchJobs = useCallback(async () => {
    try {
      setIsLoading(true);
      setError(null);
      const result = await apiClient.get<{ jobs: SyncJob[] }>('/ingestion/sync/status');
      setJobs(result.jobs);
    } catch (err) {
      if (err instanceof ApiError) {
        setError(err.message);
      } else {
        setError('Failed to load sync history');
      }
    } finally {
      setIsLoading(false);
    }
  }, []);

  useEffect(() => {
    fetchJobs();
  }, [fetchJobs]);

  async function handleManualSync() {
    setIsSyncing(true);
    try {
      const result = await apiClient.post<{ jobId: string; status: string; message: string }>(
        '/ingestion/sync'
      );
      showToast('success', result.message || 'Sync job triggered successfully');
      // Refresh the job list
      await fetchJobs();
    } catch (err) {
      if (err instanceof ApiError) {
        showToast('error', (err.body as { message?: string })?.message || err.message);
      } else {
        showToast('error', 'Failed to trigger sync');
      }
    } finally {
      setIsSyncing(false);
    }
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

  function getStatusColor(status: string): string {
    switch (status.toLowerCase()) {
      case 'success':
        return 'bg-green-100 text-green-800';
      case 'failed':
        return 'bg-red-100 text-red-800';
      case 'partial':
        return 'bg-yellow-100 text-yellow-800';
      case 'running':
      case 'in_progress':
        return 'bg-blue-100 text-blue-800';
      default:
        return 'bg-gray-100 text-gray-800';
    }
  }

  // Paginated data
  const paginatedJobs = jobs.slice(
    (currentPage - 1) * pageSize,
    currentPage * pageSize
  );

  const columns: Column<SyncJob>[] = [
    {
      key: 'jobId',
      header: 'Job ID',
      render: (job) => (
        <span className="font-mono text-xs text-gray-700">{job.jobId}</span>
      ),
    },
    {
      key: 'status',
      header: 'Status',
      render: (job) => (
        <span
          className={`inline-flex items-center rounded-full px-2.5 py-0.5 text-xs font-medium capitalize ${getStatusColor(job.status)}`}
        >
          {job.status.toLowerCase()}
        </span>
      ),
    },
    {
      key: 'fetched',
      header: 'Fetched',
      render: (job) => job.recordsFetched,
    },
    {
      key: 'stored',
      header: 'Stored',
      render: (job) => (
        <span className="text-green-700">{job.recordsStored}</span>
      ),
    },
    {
      key: 'skipped',
      header: 'Skipped',
      render: (job) => (
        <span className="text-yellow-700">{job.recordsSkipped}</span>
      ),
    },
    {
      key: 'rejected',
      header: 'Rejected',
      render: (job) => (
        <span className="text-red-700">{job.recordsRejected}</span>
      ),
    },
    {
      key: 'startedAt',
      header: 'Started',
      render: (job) => (
        <span className="text-xs">{formatDateTime(job.startedAt)}</span>
      ),
    },
    {
      key: 'completedAt',
      header: 'Completed',
      render: (job) => (
        <span className="text-xs">
          {job.completedAt ? formatDateTime(job.completedAt) : '—'}
        </span>
      ),
    },
  ];

  return (
    <div>
      <Helmet>
        <title>Sync Status | Vision Emporium Loyalty</title>
      </Helmet>

      <div className="flex items-center justify-between mb-6">
        <h1 className="text-2xl font-bold text-gray-900">Sync Status</h1>
        <button
          onClick={handleManualSync}
          disabled={isSyncing}
          className="rounded-md bg-blue-600 px-4 py-2 text-sm font-medium text-white hover:bg-blue-700 disabled:opacity-50 disabled:cursor-not-allowed transition-colors"
        >
          {isSyncing ? 'Triggering...' : 'Trigger Manual Sync'}
        </button>
      </div>

      <LoadingIndicator isLoading={isLoading} />

      {error && (
        <div className="rounded-md bg-red-50 border border-red-200 p-4" role="alert">
          <p className="text-sm text-red-800">{error}</p>
        </div>
      )}

      {!isLoading && !error && (
        <DataTable
          columns={columns}
          data={paginatedJobs}
          getRowKey={(job) => job.jobId}
          currentPage={currentPage}
          pageSize={pageSize}
          totalItems={jobs.length}
          onPageChange={setCurrentPage}
          onPageSizeChange={(size) => {
            setPageSize(size);
            setCurrentPage(1);
          }}
          emptyMessage="No sync jobs found. Click 'Trigger Manual Sync' to start one."
        />
      )}
    </div>
  );
}
