import { useState, useEffect } from 'react';
import { apiClient, ApiError } from '../../utils/api';
import { useToast } from '../../components/Toast';
import { LoadingIndicator } from '../../components/LoadingIndicator';

interface GeneralConfig {
  syncIntervalMinutes: number;
  codeExpiryDays: number;
  minPurchaseAmount: number;
  excludedCategories: string[];
}

interface GeneralFormErrors {
  syncIntervalMinutes?: string;
  codeExpiryDays?: string;
  minPurchaseAmount?: string;
  excludedCategories?: string;
}

export function validateGeneralConfig(config: GeneralConfig): GeneralFormErrors {
  const errors: GeneralFormErrors = {};

  if (!Number.isFinite(config.syncIntervalMinutes) || config.syncIntervalMinutes < 15) {
    errors.syncIntervalMinutes = 'Sync interval must be at least 15 minutes';
  }

  if (
    !Number.isInteger(config.codeExpiryDays) ||
    config.codeExpiryDays < 7 ||
    config.codeExpiryDays > 90
  ) {
    errors.codeExpiryDays = 'Code expiry must be between 7 and 90 days';
  }

  if (
    !Number.isFinite(config.minPurchaseAmount) ||
    config.minPurchaseAmount < 0.01 ||
    config.minPurchaseAmount > 999999.99
  ) {
    errors.minPurchaseAmount = 'Minimum purchase amount must be between 0.01 and 999,999.99';
  }

  return errors;
}

export function GeneralConfigSection() {
  const { showToast } = useToast();
  const [loading, setLoading] = useState(true);
  const [saving, setSaving] = useState(false);
  const [syncIntervalMinutes, setSyncIntervalMinutes] = useState(60);
  const [codeExpiryDays, setCodeExpiryDays] = useState(30);
  const [minPurchaseAmount, setMinPurchaseAmount] = useState(0);
  const [excludedCategories, setExcludedCategories] = useState<string[]>([]);
  const [newCategory, setNewCategory] = useState('');
  const [errors, setErrors] = useState<GeneralFormErrors>({});

  useEffect(() => {
    loadGeneralConfig();
  }, []);

  async function loadGeneralConfig() {
    setLoading(true);
    try {
      const data = await apiClient.get<GeneralConfig>('/config/general');
      setSyncIntervalMinutes(data.syncIntervalMinutes);
      setCodeExpiryDays(data.codeExpiryDays);
      setMinPurchaseAmount(data.minPurchaseAmount);
      setExcludedCategories(data.excludedCategories);
    } catch (err) {
      const message =
        err instanceof ApiError ? 'Failed to load general settings' : 'Network error';
      showToast('error', message);
    } finally {
      setLoading(false);
    }
  }

  function handleAddCategory() {
    const trimmed = newCategory.trim();
    if (!trimmed) return;
    if (excludedCategories.includes(trimmed)) {
      showToast('error', 'Category already excluded');
      return;
    }
    setExcludedCategories([...excludedCategories, trimmed]);
    setNewCategory('');
  }

  function handleRemoveCategory(category: string) {
    setExcludedCategories(excludedCategories.filter((c) => c !== category));
  }

  async function handleSave(e: React.FormEvent) {
    e.preventDefault();

    const config: GeneralConfig = {
      syncIntervalMinutes,
      codeExpiryDays,
      minPurchaseAmount,
      excludedCategories,
    };

    const validationErrors = validateGeneralConfig(config);
    setErrors(validationErrors);
    if (Object.keys(validationErrors).length > 0) return;

    setSaving(true);
    try {
      const data = await apiClient.put<GeneralConfig>('/config/general', config);
      setSyncIntervalMinutes(data.syncIntervalMinutes);
      setCodeExpiryDays(data.codeExpiryDays);
      setMinPurchaseAmount(data.minPurchaseAmount);
      setExcludedCategories(data.excludedCategories);
      setErrors({});
      showToast('success', 'General settings saved successfully');
    } catch (err) {
      if (err instanceof ApiError && err.body) {
        const body = err.body as { message?: string };
        showToast('error', body.message || 'Failed to save general settings');
      } else {
        showToast('error', 'Failed to save general settings');
      }
    } finally {
      setSaving(false);
    }
  }

  if (loading) {
    return <LoadingIndicator isLoading={true} label="Loading general settings..." />;
  }

  return (
    <div className="rounded-lg bg-white p-6 shadow">
      <h2 className="text-lg font-semibold text-gray-900 mb-4">General Settings</h2>

      <form onSubmit={handleSave} noValidate>
        <div className="grid grid-cols-1 md:grid-cols-2 gap-4 mb-6">
          {/* Sync Interval */}
          <div>
            <label htmlFor="sync-interval" className="block text-sm font-medium text-gray-700 mb-1">
              Sync Interval (minutes)
            </label>
            <input
              id="sync-interval"
              type="number"
              min={15}
              value={syncIntervalMinutes}
              onChange={(e) => { setSyncIntervalMinutes(parseInt(e.target.value) || 0); setErrors({}); }}
              className={`w-full rounded-md border px-3 py-2 text-sm shadow-sm focus:outline-none focus:ring-2 focus:ring-blue-500 ${
                errors.syncIntervalMinutes ? 'border-red-300' : 'border-gray-300'
              }`}
              aria-invalid={!!errors.syncIntervalMinutes}
              aria-describedby={errors.syncIntervalMinutes ? 'sync-interval-error' : undefined}
            />
            {errors.syncIntervalMinutes && (
              <p id="sync-interval-error" className="mt-1 text-sm text-red-600">
                {errors.syncIntervalMinutes}
              </p>
            )}
            <p className="mt-1 text-xs text-gray-500">Minimum: 15 minutes</p>
          </div>

          {/* Code Expiry */}
          <div>
            <label htmlFor="code-expiry" className="block text-sm font-medium text-gray-700 mb-1">
              Verification Code Expiry (days)
            </label>
            <input
              id="code-expiry"
              type="number"
              min={7}
              max={90}
              value={codeExpiryDays}
              onChange={(e) => { setCodeExpiryDays(parseInt(e.target.value) || 0); setErrors({}); }}
              className={`w-full rounded-md border px-3 py-2 text-sm shadow-sm focus:outline-none focus:ring-2 focus:ring-blue-500 ${
                errors.codeExpiryDays ? 'border-red-300' : 'border-gray-300'
              }`}
              aria-invalid={!!errors.codeExpiryDays}
              aria-describedby={errors.codeExpiryDays ? 'code-expiry-error' : undefined}
            />
            {errors.codeExpiryDays && (
              <p id="code-expiry-error" className="mt-1 text-sm text-red-600">
                {errors.codeExpiryDays}
              </p>
            )}
            <p className="mt-1 text-xs text-gray-500">Range: 7–90 days</p>
          </div>

          {/* Min Purchase Amount */}
          <div>
            <label htmlFor="min-purchase" className="block text-sm font-medium text-gray-700 mb-1">
              Minimum Purchase Amount (BDT)
            </label>
            <input
              id="min-purchase"
              type="number"
              min={0.01}
              max={999999.99}
              step={0.01}
              value={minPurchaseAmount || ''}
              onChange={(e) => { setMinPurchaseAmount(parseFloat(e.target.value) || 0); setErrors({}); }}
              className={`w-full rounded-md border px-3 py-2 text-sm shadow-sm focus:outline-none focus:ring-2 focus:ring-blue-500 ${
                errors.minPurchaseAmount ? 'border-red-300' : 'border-gray-300'
              }`}
              aria-invalid={!!errors.minPurchaseAmount}
              aria-describedby={errors.minPurchaseAmount ? 'min-purchase-error' : undefined}
            />
            {errors.minPurchaseAmount && (
              <p id="min-purchase-error" className="mt-1 text-sm text-red-600">
                {errors.minPurchaseAmount}
              </p>
            )}
            <p className="mt-1 text-xs text-gray-500">Range: 0.01–999,999.99 BDT</p>
          </div>
        </div>

        {/* Excluded Categories */}
        <div className="mb-6">
          <label className="block text-sm font-medium text-gray-700 mb-2">
            Excluded Product Categories
          </label>
          <p className="text-xs text-gray-500 mb-2">
            Purchases in these categories will not count toward threshold progression.
          </p>

          <div className="flex gap-2 mb-3">
            <input
              type="text"
              value={newCategory}
              onChange={(e) => setNewCategory(e.target.value)}
              onKeyDown={(e) => { if (e.key === 'Enter') { e.preventDefault(); handleAddCategory(); } }}
              placeholder="Enter category name"
              className="flex-1 rounded-md border border-gray-300 px-3 py-2 text-sm shadow-sm focus:outline-none focus:ring-2 focus:ring-blue-500"
              aria-label="New excluded category"
            />
            <button
              type="button"
              onClick={handleAddCategory}
              className="rounded-md bg-gray-600 px-3 py-2 text-sm font-medium text-white shadow-sm hover:bg-gray-700"
            >
              Add
            </button>
          </div>

          {excludedCategories.length > 0 ? (
            <div className="flex flex-wrap gap-2">
              {excludedCategories.map((category) => (
                <span
                  key={category}
                  className="inline-flex items-center gap-1 rounded-full bg-gray-100 px-3 py-1 text-sm text-gray-700"
                >
                  {category}
                  <button
                    type="button"
                    onClick={() => handleRemoveCategory(category)}
                    className="ml-1 text-gray-400 hover:text-red-600"
                    aria-label={`Remove ${category}`}
                  >
                    ×
                  </button>
                </span>
              ))}
            </div>
          ) : (
            <p className="text-sm text-gray-400">No categories excluded.</p>
          )}
        </div>

        <button
          type="submit"
          disabled={saving}
          className="rounded-md bg-blue-600 px-4 py-2 text-sm font-medium text-white shadow-sm hover:bg-blue-700 focus:outline-none focus:ring-2 focus:ring-blue-500 disabled:opacity-50 disabled:cursor-not-allowed"
        >
          {saving ? 'Saving...' : 'Save General Settings'}
        </button>
      </form>
    </div>
  );
}
