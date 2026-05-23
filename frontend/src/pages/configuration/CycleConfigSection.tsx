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

interface CycleFormErrors {
  startDate?: string;
  endDate?: string;
  general?: string;
}

export function validateCycleDates(
  startDate: string,
  endDate: string
): CycleFormErrors {
  const errors: CycleFormErrors = {};

  if (!startDate) {
    errors.startDate = 'Start date is required';
  }
  if (!endDate) {
    errors.endDate = 'End date is required';
  }

  if (startDate && endDate) {
    const start = new Date(startDate);
    const end = new Date(endDate);

    if (end <= start) {
      errors.endDate = 'End date must be after start date';
    } else {
      const diffMs = end.getTime() - start.getTime();
      const diffDays = Math.round(diffMs / (1000 * 60 * 60 * 24));
      if (diffDays < 30) {
        errors.general = `Cycle duration must be at least 30 days (current: ${diffDays} days)`;
      } else if (diffDays > 730) {
        errors.general = `Cycle duration must not exceed 730 days (current: ${diffDays} days)`;
      }
    }
  }

  return errors;
}

export function CycleConfigSection() {
  const { showToast } = useToast();
  const [loading, setLoading] = useState(true);
  const [saving, setSaving] = useState(false);
  const [cycle, setCycle] = useState<CycleConfig | null>(null);
  const [startDate, setStartDate] = useState('');
  const [endDate, setEndDate] = useState('');
  const [errors, setErrors] = useState<CycleFormErrors>({});

  useEffect(() => {
    loadCycleConfig();
  }, []);

  async function loadCycleConfig() {
    setLoading(true);
    try {
      const data = await apiClient.get<CycleConfig>('/config/cycle');
      setCycle(data);
      setStartDate(data.startDate);
      setEndDate(data.endDate);
    } catch (err) {
      const message =
        err instanceof ApiError ? 'Failed to load cycle configuration' : 'Network error';
      showToast('error', message);
    } finally {
      setLoading(false);
    }
  }

  function handleValidate(): boolean {
    const validationErrors = validateCycleDates(startDate, endDate);
    setErrors(validationErrors);
    return Object.keys(validationErrors).length === 0;
  }

  async function handleSave(e: React.FormEvent) {
    e.preventDefault();
    if (!handleValidate()) return;

    setSaving(true);
    try {
      const updated = await apiClient.put<CycleConfig>('/config/cycle', {
        startDate,
        endDate,
      });
      setCycle(updated);
      setErrors({});
      showToast('success', 'Loyalty cycle configuration saved successfully');
    } catch (err) {
      if (err instanceof ApiError && err.body) {
        const body = err.body as { message?: string };
        showToast('error', body.message || 'Failed to save cycle configuration');
      } else {
        showToast('error', 'Failed to save cycle configuration');
      }
    } finally {
      setSaving(false);
    }
  }

  function getDurationDays(): number | null {
    if (!startDate || !endDate) return null;
    const start = new Date(startDate);
    const end = new Date(endDate);
    if (end <= start) return null;
    return Math.round((end.getTime() - start.getTime()) / (1000 * 60 * 60 * 24));
  }

  if (loading) {
    return <LoadingIndicator isLoading={true} label="Loading cycle configuration..." />;
  }

  const duration = getDurationDays();

  return (
    <div className="rounded-lg bg-white p-6 shadow">
      <h2 className="text-lg font-semibold text-gray-900 mb-4">Loyalty Cycle Configuration</h2>

      {cycle && (
        <div className="mb-6 rounded-md bg-blue-50 border border-blue-200 p-4">
          <p className="text-sm text-blue-800">
            <span className="font-medium">Current Cycle:</span>{' '}
            {cycle.startDate} to {cycle.endDate}
            {cycle.isActive && (
              <span className="ml-2 inline-flex items-center rounded-full bg-green-100 px-2.5 py-0.5 text-xs font-medium text-green-800">
                Active — {cycle.daysRemaining} days remaining
              </span>
            )}
          </p>
        </div>
      )}

      <form onSubmit={handleSave} noValidate>
        <div className="grid grid-cols-1 md:grid-cols-2 gap-4 mb-4">
          <div>
            <label htmlFor="cycle-start" className="block text-sm font-medium text-gray-700 mb-1">
              Start Date
            </label>
            <input
              id="cycle-start"
              type="date"
              value={startDate}
              onChange={(e) => { setStartDate(e.target.value); setErrors({}); }}
              className={`w-full rounded-md border px-3 py-2 text-sm shadow-sm focus:outline-none focus:ring-2 focus:ring-blue-500 ${
                errors.startDate ? 'border-red-300' : 'border-gray-300'
              }`}
              aria-invalid={!!errors.startDate}
              aria-describedby={errors.startDate ? 'cycle-start-error' : undefined}
            />
            {errors.startDate && (
              <p id="cycle-start-error" className="mt-1 text-sm text-red-600">{errors.startDate}</p>
            )}
          </div>

          <div>
            <label htmlFor="cycle-end" className="block text-sm font-medium text-gray-700 mb-1">
              End Date
            </label>
            <input
              id="cycle-end"
              type="date"
              value={endDate}
              onChange={(e) => { setEndDate(e.target.value); setErrors({}); }}
              className={`w-full rounded-md border px-3 py-2 text-sm shadow-sm focus:outline-none focus:ring-2 focus:ring-blue-500 ${
                errors.endDate ? 'border-red-300' : 'border-gray-300'
              }`}
              aria-invalid={!!errors.endDate}
              aria-describedby={errors.endDate ? 'cycle-end-error' : undefined}
            />
            {errors.endDate && (
              <p id="cycle-end-error" className="mt-1 text-sm text-red-600">{errors.endDate}</p>
            )}
          </div>
        </div>

        {duration !== null && (
          <p className="text-sm text-gray-600 mb-2">
            Duration: <span className="font-medium">{duration} days</span>
            {duration >= 30 && duration <= 730 && (
              <span className="ml-1 text-green-600">✓</span>
            )}
          </p>
        )}

        {errors.general && (
          <p className="mt-1 mb-3 text-sm text-red-600" role="alert">{errors.general}</p>
        )}

        <button
          type="submit"
          disabled={saving}
          className="mt-4 rounded-md bg-blue-600 px-4 py-2 text-sm font-medium text-white shadow-sm hover:bg-blue-700 focus:outline-none focus:ring-2 focus:ring-blue-500 disabled:opacity-50 disabled:cursor-not-allowed"
        >
          {saving ? 'Saving...' : 'Save Cycle Configuration'}
        </button>
      </form>
    </div>
  );
}
