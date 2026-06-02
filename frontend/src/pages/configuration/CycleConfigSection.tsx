import { useState, useEffect } from 'react';
import { apiClient, ApiError } from '../../utils/api';
import { useToast } from '../../components/Toast';
import { LoadingIndicator } from '../../components/LoadingIndicator';

interface CycleConfig {
  cycleId: string;
  startDate: string;
  endDate: string;
  isActive: boolean;
  daysRemaining: number;
}

export function CycleConfigSection() {
  const { showToast } = useToast();
  const [loading, setLoading] = useState(true);
  const [cycle, setCycle] = useState<CycleConfig | null>(null);

  useEffect(() => {
    loadCycleConfig();
  }, []);

  async function loadCycleConfig() {
    setLoading(true);
    try {
      const data = await apiClient.get<CycleConfig>('/config/cycle');
      setCycle(data);
    } catch (err) {
      const message =
        err instanceof ApiError ? 'Failed to load cycle configuration' : 'Network error';
      showToast('error', message);
    } finally {
      setLoading(false);
    }
  }

  if (loading) {
    return <LoadingIndicator isLoading={true} label="Loading cycle configuration..." />;
  }

  if (!cycle) {
    return <p className="text-sm text-gray-500">Unable to load cycle information.</p>;
  }

  return (
    <div className="rounded-lg bg-white p-6 shadow">
      <h2 className="text-lg font-semibold text-gray-900 mb-4">Loyalty Cycle</h2>

      <div className="rounded-md bg-blue-50 border border-blue-200 p-4 mb-6">
        <p className="text-sm text-blue-800">
          The loyalty cycle runs automatically from <strong>June 1</strong> to <strong>May 31</strong> every year.
          Customer purchase counts reset at the start of each new cycle.
        </p>
      </div>

      <div className="grid grid-cols-1 md:grid-cols-2 lg:grid-cols-4 gap-6">
        <div>
          <p className="text-sm font-medium text-gray-500">Cycle ID</p>
          <p className="mt-1 text-lg font-semibold text-gray-900">{cycle.cycleId}</p>
        </div>
        <div>
          <p className="text-sm font-medium text-gray-500">Start Date</p>
          <p className="mt-1 text-lg font-semibold text-gray-900">{cycle.startDate}</p>
        </div>
        <div>
          <p className="text-sm font-medium text-gray-500">End Date</p>
          <p className="mt-1 text-lg font-semibold text-gray-900">{cycle.endDate}</p>
        </div>
        <div>
          <p className="text-sm font-medium text-gray-500">Days Remaining</p>
          <p className="mt-1 text-lg font-semibold text-gray-900">
            {cycle.daysRemaining} days
            <span className="ml-2 inline-flex items-center rounded-full bg-green-100 px-2.5 py-0.5 text-xs font-medium text-green-800">
              Active
            </span>
          </p>
        </div>
      </div>
    </div>
  );
}
