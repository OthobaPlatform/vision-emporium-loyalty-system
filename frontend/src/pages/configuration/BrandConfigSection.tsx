import { useState, useEffect } from 'react';
import { apiClient, ApiError } from '../../utils/api';
import { useToast } from '../../components/Toast';
import { LoadingIndicator } from '../../components/LoadingIndicator';
import { useBrand, type BrandConfig } from '../../contexts/BrandContext';

export function BrandConfigSection() {
  const { showToast } = useToast();
  const { refreshBrand } = useBrand();
  const [loading, setLoading] = useState(true);
  const [saving, setSaving] = useState(false);
  const [companyName, setCompanyName] = useState('');
  const [primaryColor, setPrimaryColor] = useState('#E31E24');
  const [secondaryColor, setSecondaryColor] = useState('#1a1a1a');
  const [accentColor, setAccentColor] = useState('#D6E4F0');
  const [logoUrl, setLogoUrl] = useState('');
  const [faviconUrl, setFaviconUrl] = useState('');

  useEffect(() => {
    loadBrandConfig();
  }, []);

  async function loadBrandConfig() {
    setLoading(true);
    try {
      const data = await apiClient.get<BrandConfig>('/config/brand');
      setCompanyName(data.companyName);
      setPrimaryColor(data.primaryColor);
      setSecondaryColor(data.secondaryColor);
      setAccentColor(data.accentColor);
      setLogoUrl(data.logoUrl);
      setFaviconUrl(data.faviconUrl);
    } catch (err) {
      const message =
        err instanceof ApiError ? 'Failed to load brand settings' : 'Network error';
      showToast('error', message);
    } finally {
      setLoading(false);
    }
  }

  async function handleSave(e: React.FormEvent) {
    e.preventDefault();

    if (!companyName.trim()) {
      showToast('error', 'Company name is required');
      return;
    }

    setSaving(true);
    try {
      const data = await apiClient.put<BrandConfig>('/config/brand', {
        companyName: companyName.trim(),
        primaryColor: primaryColor.trim(),
        secondaryColor: secondaryColor.trim(),
        accentColor: accentColor.trim(),
        logoUrl: logoUrl.trim(),
        faviconUrl: faviconUrl.trim(),
      });
      setCompanyName(data.companyName);
      setPrimaryColor(data.primaryColor);
      setSecondaryColor(data.secondaryColor);
      setAccentColor(data.accentColor);
      setLogoUrl(data.logoUrl);
      setFaviconUrl(data.faviconUrl);
      await refreshBrand();
      showToast('success', 'Brand settings saved successfully');
    } catch (err) {
      if (err instanceof ApiError && err.body) {
        const body = err.body as { message?: string; details?: string[] };
        showToast('error', body.details?.[0] || body.message || 'Failed to save brand settings');
      } else {
        showToast('error', 'Failed to save brand settings');
      }
    } finally {
      setSaving(false);
    }
  }

  if (loading) {
    return <LoadingIndicator isLoading={true} label="Loading brand settings..." />;
  }

  return (
    <div className="rounded-lg bg-white p-6 shadow">
      <h2 className="text-lg font-semibold text-gray-900 mb-4">Brand Configuration</h2>
      <p className="text-sm text-gray-500 mb-6">
        Customize the look and feel of the application with your brand colors and logo.
      </p>

      <form onSubmit={handleSave} noValidate>
        {/* Company Name */}
        <div className="mb-6">
          <label htmlFor="brand-company-name" className="block text-sm font-medium text-gray-700 mb-1">
            Company Name
          </label>
          <input
            id="brand-company-name"
            type="text"
            value={companyName}
            onChange={(e) => setCompanyName(e.target.value)}
            placeholder="Vision Emporium"
            className="w-full max-w-md rounded-md border border-gray-300 px-3 py-2 text-sm shadow-sm focus:outline-none focus:ring-2 focus:ring-blue-500"
          />
        </div>

        {/* Colors */}
        <div className="grid grid-cols-1 md:grid-cols-3 gap-4 mb-6">
          {/* Primary Color */}
          <div>
            <label htmlFor="brand-primary-color" className="block text-sm font-medium text-gray-700 mb-1">
              Primary Color
            </label>
            <div className="flex items-center gap-2">
              <input
                type="color"
                value={primaryColor}
                onChange={(e) => setPrimaryColor(e.target.value)}
                className="h-9 w-12 rounded border border-gray-300 cursor-pointer"
                aria-label="Primary color picker"
              />
              <input
                id="brand-primary-color"
                type="text"
                value={primaryColor}
                onChange={(e) => setPrimaryColor(e.target.value)}
                placeholder="#E31E24"
                className="flex-1 rounded-md border border-gray-300 px-3 py-2 text-sm shadow-sm focus:outline-none focus:ring-2 focus:ring-blue-500 font-mono"
              />
            </div>
            <p className="mt-1 text-xs text-gray-500">Buttons, active nav, accents</p>
          </div>

          {/* Secondary Color */}
          <div>
            <label htmlFor="brand-secondary-color" className="block text-sm font-medium text-gray-700 mb-1">
              Secondary Color
            </label>
            <div className="flex items-center gap-2">
              <input
                type="color"
                value={secondaryColor}
                onChange={(e) => setSecondaryColor(e.target.value)}
                className="h-9 w-12 rounded border border-gray-300 cursor-pointer"
                aria-label="Secondary color picker"
              />
              <input
                id="brand-secondary-color"
                type="text"
                value={secondaryColor}
                onChange={(e) => setSecondaryColor(e.target.value)}
                placeholder="#1a1a1a"
                className="flex-1 rounded-md border border-gray-300 px-3 py-2 text-sm shadow-sm focus:outline-none focus:ring-2 focus:ring-blue-500 font-mono"
              />
            </div>
            <p className="mt-1 text-xs text-gray-500">Text, dark surfaces</p>
          </div>

          {/* Accent Color */}
          <div>
            <label htmlFor="brand-accent-color" className="block text-sm font-medium text-gray-700 mb-1">
              Accent Color
            </label>
            <div className="flex items-center gap-2">
              <input
                type="color"
                value={accentColor}
                onChange={(e) => setAccentColor(e.target.value)}
                className="h-9 w-12 rounded border border-gray-300 cursor-pointer"
                aria-label="Accent color picker"
              />
              <input
                id="brand-accent-color"
                type="text"
                value={accentColor}
                onChange={(e) => setAccentColor(e.target.value)}
                placeholder="#D6E4F0"
                className="flex-1 rounded-md border border-gray-300 px-3 py-2 text-sm shadow-sm focus:outline-none focus:ring-2 focus:ring-blue-500 font-mono"
              />
            </div>
            <p className="mt-1 text-xs text-gray-500">Backgrounds, highlights</p>
          </div>
        </div>

        {/* Logo and Favicon URLs */}
        <div className="grid grid-cols-1 md:grid-cols-2 gap-4 mb-6">
          <div>
            <label htmlFor="brand-logo-url" className="block text-sm font-medium text-gray-700 mb-1">
              Logo URL
            </label>
            <input
              id="brand-logo-url"
              type="text"
              value={logoUrl}
              onChange={(e) => setLogoUrl(e.target.value)}
              placeholder="https://example.com/logo.png"
              className="w-full rounded-md border border-gray-300 px-3 py-2 text-sm shadow-sm focus:outline-none focus:ring-2 focus:ring-blue-500"
            />
            <p className="mt-1 text-xs text-gray-500">URL to your company logo (optional)</p>
          </div>

          <div>
            <label htmlFor="brand-favicon-url" className="block text-sm font-medium text-gray-700 mb-1">
              Favicon URL
            </label>
            <input
              id="brand-favicon-url"
              type="text"
              value={faviconUrl}
              onChange={(e) => setFaviconUrl(e.target.value)}
              placeholder="https://example.com/favicon.ico"
              className="w-full rounded-md border border-gray-300 px-3 py-2 text-sm shadow-sm focus:outline-none focus:ring-2 focus:ring-blue-500"
            />
            <p className="mt-1 text-xs text-gray-500">URL to your favicon (optional)</p>
          </div>
        </div>

        {/* Live Preview */}
        <div className="mb-6 rounded-lg border border-gray-200 p-4">
          <h3 className="text-sm font-medium text-gray-700 mb-3">Live Preview</h3>
          <div className="flex items-center gap-4">
            {/* Simulated button */}
            <button
              type="button"
              className="rounded-lg px-4 py-2 text-sm font-semibold text-white shadow-sm"
              style={{ backgroundColor: primaryColor }}
            >
              Primary Button
            </button>
            {/* Simulated nav item */}
            <span
              className="rounded-lg px-3 py-1.5 text-sm font-medium"
              style={{ backgroundColor: `${primaryColor}1A`, color: primaryColor }}
            >
              Active Nav Item
            </span>
            {/* Simulated background */}
            <div
              className="rounded-lg px-4 py-2 text-sm"
              style={{ backgroundColor: accentColor, color: secondaryColor }}
            >
              Background
            </div>
            {/* Simulated sidebar header */}
            <div
              className="rounded-lg px-4 py-2 text-sm text-white font-bold"
              style={{ background: `linear-gradient(to right, ${primaryColor}, ${secondaryColor})` }}
            >
              {companyName || 'Company'}
            </div>
          </div>
        </div>

        <button
          type="submit"
          disabled={saving}
          className="rounded-md bg-blue-600 px-4 py-2 text-sm font-medium text-white shadow-sm hover:bg-blue-700 focus:outline-none focus:ring-2 focus:ring-blue-500 disabled:opacity-50 disabled:cursor-not-allowed"
        >
          {saving ? 'Saving...' : 'Save Brand Settings'}
        </button>
      </form>
    </div>
  );
}
