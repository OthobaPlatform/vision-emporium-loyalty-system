import { useState, useEffect } from 'react';
import { apiClient, ApiError } from '../../utils/api';
import { useToast } from '../../components/Toast';
import { LoadingIndicator } from '../../components/LoadingIndicator';

interface SmsConfig {
  enabled: boolean;
  baseUrl: string;
  apiKey: string;
  senderId: string;
}

export function SmsConfigSection() {
  const { showToast } = useToast();
  const [loading, setLoading] = useState(true);
  const [saving, setSaving] = useState(false);
  const [enabled, setEnabled] = useState(false);
  const [baseUrl, setBaseUrl] = useState('');
  const [apiKey, setApiKey] = useState('');
  const [senderId, setSenderId] = useState('');

  useEffect(() => {
    loadSmsConfig();
  }, []);

  async function loadSmsConfig() {
    setLoading(true);
    try {
      const data = await apiClient.get<SmsConfig>('/config/sms');
      setEnabled(data.enabled);
      setBaseUrl(data.baseUrl);
      setApiKey(data.apiKey);
      setSenderId(data.senderId);
    } catch (err) {
      const message =
        err instanceof ApiError ? 'Failed to load SMS settings' : 'Network error';
      showToast('error', message);
    } finally {
      setLoading(false);
    }
  }

  async function handleSave(e: React.FormEvent) {
    e.preventDefault();

    if (enabled) {
      if (!baseUrl.trim()) {
        showToast('error', 'Base URL is required when SMS is enabled');
        return;
      }
      if (!apiKey.trim()) {
        showToast('error', 'API Key is required when SMS is enabled');
        return;
      }
      if (!senderId.trim()) {
        showToast('error', 'Sender ID is required when SMS is enabled');
        return;
      }
    }

    setSaving(true);
    try {
      const data = await apiClient.put<SmsConfig>('/config/sms', {
        enabled,
        baseUrl: baseUrl.trim(),
        apiKey: apiKey.trim(),
        senderId: senderId.trim(),
      });
      setEnabled(data.enabled);
      setBaseUrl(data.baseUrl);
      setApiKey(data.apiKey);
      setSenderId(data.senderId);
      showToast('success', 'SMS settings saved successfully');
    } catch (err) {
      if (err instanceof ApiError && err.body) {
        const body = err.body as { message?: string; details?: string[] };
        showToast('error', body.details?.[0] || body.message || 'Failed to save SMS settings');
      } else {
        showToast('error', 'Failed to save SMS settings');
      }
    } finally {
      setSaving(false);
    }
  }

  if (loading) {
    return <LoadingIndicator isLoading={true} label="Loading SMS settings..." />;
  }

  return (
    <div className="rounded-lg bg-white p-6 shadow">
      <h2 className="text-lg font-semibold text-gray-900 mb-4">SMS Gateway Settings</h2>
      <p className="text-sm text-gray-500 mb-6">
        Configure the SMS gateway for sending loyalty notifications to customers.
      </p>

      <form onSubmit={handleSave} noValidate>
        {/* Enabled Toggle */}
        <div className="mb-6">
          <label className="flex items-center gap-3 cursor-pointer">
            <div className="relative">
              <input
                type="checkbox"
                checked={enabled}
                onChange={(e) => setEnabled(e.target.checked)}
                className="sr-only peer"
                aria-label="Enable SMS sending"
              />
              <div className="w-11 h-6 bg-gray-200 rounded-full peer peer-checked:bg-blue-600 transition-colors"></div>
              <div className="absolute left-[2px] top-[2px] w-5 h-5 bg-white rounded-full shadow peer-checked:translate-x-5 transition-transform"></div>
            </div>
            <span className="text-sm font-medium text-gray-700">
              {enabled ? 'SMS Enabled' : 'SMS Disabled'}
            </span>
          </label>
          <p className="mt-1 text-xs text-gray-500 ml-14">
            When disabled, SMS messages will be logged to console instead of being sent.
          </p>
        </div>

        <div className="grid grid-cols-1 md:grid-cols-2 gap-4 mb-6">
          {/* Base URL */}
          <div>
            <label htmlFor="sms-base-url" className="block text-sm font-medium text-gray-700 mb-1">
              Base URL
            </label>
            <input
              id="sms-base-url"
              type="text"
              value={baseUrl}
              onChange={(e) => setBaseUrl(e.target.value)}
              placeholder="https://sms-gateway.example.com"
              className="w-full rounded-md border border-gray-300 px-3 py-2 text-sm shadow-sm focus:outline-none focus:ring-2 focus:ring-blue-500"
              disabled={!enabled}
            />
            <p className="mt-1 text-xs text-gray-500">The SMS gateway API base URL</p>
          </div>

          {/* API Key */}
          <div>
            <label htmlFor="sms-api-key" className="block text-sm font-medium text-gray-700 mb-1">
              API Key
            </label>
            <input
              id="sms-api-key"
              type="password"
              value={apiKey}
              onChange={(e) => setApiKey(e.target.value)}
              placeholder="Enter API key"
              className="w-full rounded-md border border-gray-300 px-3 py-2 text-sm shadow-sm focus:outline-none focus:ring-2 focus:ring-blue-500"
              disabled={!enabled}
              autoComplete="off"
            />
            <p className="mt-1 text-xs text-gray-500">Authentication key for the SMS gateway</p>
          </div>

          {/* Sender ID */}
          <div>
            <label htmlFor="sms-sender-id" className="block text-sm font-medium text-gray-700 mb-1">
              Sender ID
            </label>
            <input
              id="sms-sender-id"
              type="text"
              value={senderId}
              onChange={(e) => setSenderId(e.target.value)}
              placeholder="VisionEmporium"
              className="w-full rounded-md border border-gray-300 px-3 py-2 text-sm shadow-sm focus:outline-none focus:ring-2 focus:ring-blue-500"
              disabled={!enabled}
            />
            <p className="mt-1 text-xs text-gray-500">Name displayed as the SMS sender</p>
          </div>
        </div>

        <button
          type="submit"
          disabled={saving}
          className="rounded-md bg-blue-600 px-4 py-2 text-sm font-medium text-white shadow-sm hover:bg-blue-700 focus:outline-none focus:ring-2 focus:ring-blue-500 disabled:opacity-50 disabled:cursor-not-allowed"
        >
          {saving ? 'Saving...' : 'Save SMS Settings'}
        </button>
      </form>
    </div>
  );
}
