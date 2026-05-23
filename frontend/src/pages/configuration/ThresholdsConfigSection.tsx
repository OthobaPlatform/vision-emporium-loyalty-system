import { useState, useEffect } from 'react';
import { apiClient, ApiError } from '../../utils/api';
import { useToast } from '../../components/Toast';
import { LoadingIndicator } from '../../components/LoadingIndicator';

type GiftType = 'Cash_Return' | 'Gift_Item';
type GiftValueType = 'fixed' | 'percentage';

interface Threshold {
  tier: number;
  requiredPurchases: number;
  giftType: GiftType;
  giftDescription: string;
  giftValue: number;
  giftValueType: GiftValueType;
  isEnabled: boolean;
}

interface ThresholdFormErrors {
  [key: string]: string | undefined;
}

export function validateThreshold(
  threshold: Threshold,
  index: number,
  allThresholds: Threshold[]
): ThresholdFormErrors {
  const errors: ThresholdFormErrors = {};
  const prefix = `threshold-${index}`;

  if (
    !Number.isInteger(threshold.requiredPurchases) ||
    threshold.requiredPurchases < 1 ||
    threshold.requiredPurchases > 100
  ) {
    errors[`${prefix}-purchases`] =
      'Required purchases must be an integer between 1 and 100';
  }

  // Check for duplicates
  const duplicateIdx = allThresholds.findIndex(
    (t, i) => i !== index && t.requiredPurchases === threshold.requiredPurchases
  );
  if (duplicateIdx !== -1 && threshold.requiredPurchases > 0) {
    errors[`${prefix}-purchases`] =
      'Duplicate threshold value — each tier must have a unique purchase count';
  }

  if (!threshold.giftDescription.trim()) {
    errors[`${prefix}-description`] = 'Gift description is required';
  } else if (threshold.giftDescription.length > 200) {
    errors[`${prefix}-description`] = 'Gift description must be 200 characters or less';
  }

  if (threshold.giftType === 'Cash_Return') {
    if (threshold.giftValueType === 'percentage') {
      if (threshold.giftValue < 0.01 || threshold.giftValue > 100) {
        errors[`${prefix}-value`] = 'Percentage must be between 0.01% and 100%';
      }
    } else {
      if (threshold.giftValue < 0.01 || threshold.giftValue > 999999.99) {
        errors[`${prefix}-value`] = 'Fixed amount must be between 0.01 and 999,999.99 BDT';
      }
    }
  }

  return errors;
}

export function validateAllThresholds(thresholds: Threshold[]): ThresholdFormErrors {
  if (thresholds.length < 1) {
    return { general: 'At least 1 threshold is required' };
  }
  if (thresholds.length > 10) {
    return { general: 'Maximum 10 thresholds allowed' };
  }

  let allErrors: ThresholdFormErrors = {};
  thresholds.forEach((t, i) => {
    const errs = validateThreshold(t, i, thresholds);
    allErrors = { ...allErrors, ...errs };
  });
  return allErrors;
}

function createEmptyThreshold(tier: number): Threshold {
  return {
    tier,
    requiredPurchases: 0,
    giftType: 'Cash_Return',
    giftDescription: '',
    giftValue: 0,
    giftValueType: 'fixed',
    isEnabled: true,
  };
}

export function ThresholdsConfigSection() {
  const { showToast } = useToast();
  const [loading, setLoading] = useState(true);
  const [saving, setSaving] = useState(false);
  const [thresholds, setThresholds] = useState<Threshold[]>([]);
  const [errors, setErrors] = useState<ThresholdFormErrors>({});
  const [editingIndex, setEditingIndex] = useState<number | null>(null);

  useEffect(() => {
    loadThresholds();
  }, []);

  async function loadThresholds() {
    setLoading(true);
    try {
      const data = await apiClient.get<{ thresholds: Threshold[] }>('/config/thresholds');
      setThresholds(data.thresholds);
    } catch (err) {
      const message =
        err instanceof ApiError ? 'Failed to load thresholds' : 'Network error';
      showToast('error', message);
    } finally {
      setLoading(false);
    }
  }

  function handleAddThreshold() {
    if (thresholds.length >= 10) {
      showToast('error', 'Maximum 10 thresholds allowed');
      return;
    }
    const nextTier = thresholds.length > 0
      ? Math.max(...thresholds.map((t) => t.tier)) + 1
      : 1;
    setThresholds([...thresholds, createEmptyThreshold(nextTier)]);
    setEditingIndex(thresholds.length);
  }

  function handleRemoveThreshold(index: number) {
    if (thresholds.length <= 1) {
      showToast('error', 'At least 1 threshold is required');
      return;
    }
    setThresholds(thresholds.filter((_, i) => i !== index));
    setEditingIndex(null);
    setErrors({});
  }

  function handleToggleEnabled(index: number) {
    const updated = [...thresholds];
    updated[index] = { ...updated[index], isEnabled: !updated[index].isEnabled };
    setThresholds(updated);
  }

  function handleUpdateThreshold(index: number, field: keyof Threshold, value: unknown) {
    const updated = [...thresholds];
    updated[index] = { ...updated[index], [field]: value };
    setThresholds(updated);
    setErrors({});
  }

  async function handleSave() {
    const validationErrors = validateAllThresholds(thresholds);
    setErrors(validationErrors);
    if (Object.keys(validationErrors).length > 0) return;

    setSaving(true);
    try {
      const data = await apiClient.put<{ thresholds: Threshold[] }>('/config/thresholds', {
        thresholds,
      });
      setThresholds(data.thresholds);
      setEditingIndex(null);
      setErrors({});
      showToast('success', 'Purchase thresholds saved successfully');
    } catch (err) {
      if (err instanceof ApiError && err.body) {
        const body = err.body as { message?: string };
        showToast('error', body.message || 'Failed to save thresholds');
      } else {
        showToast('error', 'Failed to save thresholds');
      }
    } finally {
      setSaving(false);
    }
  }

  if (loading) {
    return <LoadingIndicator isLoading={true} label="Loading thresholds..." />;
  }

  return (
    <div className="rounded-lg bg-white p-6 shadow">
      <div className="flex items-center justify-between mb-4">
        <h2 className="text-lg font-semibold text-gray-900">Purchase Thresholds</h2>
        <button
          type="button"
          onClick={handleAddThreshold}
          disabled={thresholds.length >= 10}
          className="rounded-md bg-green-600 px-3 py-1.5 text-sm font-medium text-white shadow-sm hover:bg-green-700 focus:outline-none focus:ring-2 focus:ring-green-500 disabled:opacity-50 disabled:cursor-not-allowed"
        >
          + Add Threshold
        </button>
      </div>

      {errors.general && (
        <p className="mb-4 text-sm text-red-600" role="alert">{errors.general}</p>
      )}

      {thresholds.length === 0 ? (
        <p className="text-sm text-gray-500">No thresholds configured. Add one to get started.</p>
      ) : (
        <div className="space-y-4">
          {thresholds.map((threshold, index) => (
            <ThresholdCard
              key={index}
              threshold={threshold}
              index={index}
              isEditing={editingIndex === index}
              errors={errors}
              onEdit={() => setEditingIndex(index)}
              onToggleEnabled={() => handleToggleEnabled(index)}
              onUpdate={(field, value) => handleUpdateThreshold(index, field, value)}
              onRemove={() => handleRemoveThreshold(index)}
            />
          ))}
        </div>
      )}

      <button
        type="button"
        onClick={handleSave}
        disabled={saving}
        className="mt-6 rounded-md bg-[#E31E24] px-4 py-2 text-sm font-medium text-white shadow-sm hover:bg-[#c91a1f] focus:outline-none focus:ring-2 focus:ring-[#E31E24]/50 disabled:opacity-50 disabled:cursor-not-allowed transition-colors"
      >
        {saving ? 'Saving...' : 'Save All Thresholds'}
      </button>
    </div>
  );
}

interface ThresholdCardProps {
  threshold: Threshold;
  index: number;
  isEditing: boolean;
  errors: ThresholdFormErrors;
  onEdit: () => void;
  onToggleEnabled: () => void;
  onUpdate: (field: keyof Threshold, value: unknown) => void;
  onRemove: () => void;
}

function ThresholdCard({
  threshold,
  index,
  isEditing,
  errors,
  onEdit,
  onToggleEnabled,
  onUpdate,
  onRemove,
}: ThresholdCardProps) {
  const prefix = `threshold-${index}`;

  return (
    <div
      className={`rounded-md border p-4 ${
        threshold.isEnabled ? 'border-gray-200 bg-white' : 'border-gray-100 bg-gray-50'
      }`}
    >
      <div className="flex items-center justify-between mb-3">
        <div className="flex items-center gap-3">
          <span className="text-sm font-medium text-gray-700">Tier {threshold.tier}</span>
          <label className="relative inline-flex items-center cursor-pointer">
            <input
              type="checkbox"
              checked={threshold.isEnabled}
              onChange={onToggleEnabled}
              className="sr-only peer"
              aria-label={`Enable tier ${threshold.tier}`}
            />
            <div className="w-9 h-5 bg-gray-200 peer-focus:outline-none peer-focus:ring-2 peer-focus:ring-blue-300 rounded-full peer peer-checked:after:translate-x-full peer-checked:after:border-white after:content-[''] after:absolute after:top-[2px] after:left-[2px] after:bg-white after:border-gray-300 after:border after:rounded-full after:h-4 after:w-4 after:transition-all peer-checked:bg-blue-600"></div>
            <span className="ml-2 text-xs text-gray-500">
              {threshold.isEnabled ? 'Enabled' : 'Disabled'}
            </span>
          </label>
        </div>
        <div className="flex gap-2">
          {!isEditing && (
            <button
              type="button"
              onClick={onEdit}
              className="text-sm text-blue-600 hover:text-blue-800"
            >
              Edit
            </button>
          )}
          <button
            type="button"
            onClick={onRemove}
            className="text-sm text-red-600 hover:text-red-800"
          >
            Remove
          </button>
        </div>
      </div>

      {isEditing ? (
        <div className="grid grid-cols-1 md:grid-cols-2 gap-3">
          <div>
            <label htmlFor={`${prefix}-purchases`} className="block text-xs font-medium text-gray-600 mb-1">
              Required Purchases (1–100)
            </label>
            <input
              id={`${prefix}-purchases`}
              type="number"
              min={1}
              max={100}
              value={threshold.requiredPurchases || ''}
              onChange={(e) => onUpdate('requiredPurchases', parseInt(e.target.value) || 0)}
              className={`w-full rounded-md border px-3 py-1.5 text-sm ${
                errors[`${prefix}-purchases`] ? 'border-red-300' : 'border-gray-300'
              }`}
              aria-invalid={!!errors[`${prefix}-purchases`]}
              aria-describedby={errors[`${prefix}-purchases`] ? `${prefix}-purchases-error` : undefined}
            />
            {errors[`${prefix}-purchases`] && (
              <p id={`${prefix}-purchases-error`} className="mt-1 text-xs text-red-600">
                {errors[`${prefix}-purchases`]}
              </p>
            )}
          </div>

          <div>
            <label htmlFor={`${prefix}-type`} className="block text-xs font-medium text-gray-600 mb-1">
              Gift Type
            </label>
            <select
              id={`${prefix}-type`}
              value={threshold.giftType}
              onChange={(e) => onUpdate('giftType', e.target.value)}
              className="w-full rounded-md border border-gray-300 px-3 py-1.5 text-sm"
            >
              <option value="Cash_Return">Cash Return</option>
              <option value="Gift_Item">Gift Item</option>
            </select>
          </div>

          <div>
            <label htmlFor={`${prefix}-description`} className="block text-xs font-medium text-gray-600 mb-1">
              Gift Description
            </label>
            <input
              id={`${prefix}-description`}
              type="text"
              maxLength={200}
              value={threshold.giftDescription}
              onChange={(e) => onUpdate('giftDescription', e.target.value)}
              className={`w-full rounded-md border px-3 py-1.5 text-sm ${
                errors[`${prefix}-description`] ? 'border-red-300' : 'border-gray-300'
              }`}
              aria-invalid={!!errors[`${prefix}-description`]}
              aria-describedby={errors[`${prefix}-description`] ? `${prefix}-desc-error` : undefined}
            />
            {errors[`${prefix}-description`] && (
              <p id={`${prefix}-desc-error`} className="mt-1 text-xs text-red-600">
                {errors[`${prefix}-description`]}
              </p>
            )}
          </div>

          {threshold.giftType === 'Cash_Return' && (
            <>
              <div>
                <label htmlFor={`${prefix}-value-type`} className="block text-xs font-medium text-gray-600 mb-1">
                  Value Type
                </label>
                <select
                  id={`${prefix}-value-type`}
                  value={threshold.giftValueType || 'fixed'}
                  onChange={(e) => onUpdate('giftValueType', e.target.value)}
                  className="w-full rounded-md border border-gray-300 px-3 py-1.5 text-sm"
                >
                  <option value="fixed">Fixed Amount (BDT)</option>
                  <option value="percentage">Percentage (%)</option>
                </select>
              </div>

              <div>
                <label htmlFor={`${prefix}-value`} className="block text-xs font-medium text-gray-600 mb-1">
                  Gift Value {threshold.giftValueType === 'percentage' ? '(%)' : '(BDT)'}
                </label>
                <input
                  id={`${prefix}-value`}
                  type="number"
                  min={0.01}
                  max={threshold.giftValueType === 'percentage' ? 100 : 999999.99}
                  step={0.01}
                  value={threshold.giftValue || ''}
                  onChange={(e) => onUpdate('giftValue', parseFloat(e.target.value) || 0)}
                  className={`w-full rounded-md border px-3 py-1.5 text-sm ${
                    errors[`${prefix}-value`] ? 'border-red-300' : 'border-gray-300'
                  }`}
                  aria-invalid={!!errors[`${prefix}-value`]}
                  aria-describedby={errors[`${prefix}-value`] ? `${prefix}-value-error` : undefined}
                />
                {errors[`${prefix}-value`] && (
                  <p id={`${prefix}-value-error`} className="mt-1 text-xs text-red-600">
                    {errors[`${prefix}-value`]}
                  </p>
                )}
                <p className="mt-0.5 text-[10px] text-gray-400">
                  {threshold.giftValueType === 'percentage'
                    ? 'Percentage of purchase amount (0.01–100%)'
                    : 'Fixed amount in BDT (0.01–999,999.99)'}
                </p>
              </div>
            </>
          )}
        </div>
      ) : (
        <div className="grid grid-cols-2 md:grid-cols-4 gap-2 text-sm">
          <div>
            <span className="text-gray-500">Purchases:</span>{' '}
            <span className="font-medium">{threshold.requiredPurchases}</span>
          </div>
          <div>
            <span className="text-gray-500">Type:</span>{' '}
            <span className="font-medium">
              {threshold.giftType === 'Cash_Return' ? 'Cash Return' : 'Gift Item'}
            </span>
          </div>
          <div>
            <span className="text-gray-500">Value:</span>{' '}
            <span className="font-medium">
              {threshold.giftType === 'Gift_Item'
                ? '—'
                : threshold.giftValueType === 'percentage'
                  ? `${threshold.giftValue}%`
                  : `৳${threshold.giftValue.toFixed(2)}`}
            </span>
          </div>
          <div className="col-span-2 md:col-span-4">
            <span className="text-gray-500">Description:</span>{' '}
            <span className="font-medium">{threshold.giftDescription || '—'}</span>
          </div>
        </div>
      )}
    </div>
  );
}
