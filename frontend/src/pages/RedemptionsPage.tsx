import { useState, useEffect } from 'react';
import { useSearchParams } from 'react-router-dom';
import { Helmet } from 'react-helmet-async';
import { apiClient, ApiError } from '../utils/api';
import { LoadingIndicator, useToast } from '../components';

interface RedemptionResult {
  code: string;
  giftType: string;
  giftDescription: string;
  redeemedAt: string;
}

interface RedemptionSearchResult {
  code: string;
  customerName: string;
  phoneNumber: string;
  giftType: string;
  giftDescription: string;
  status: string;
  designatedOutlet: string;
  issuedAt: string;
  redeemedAt?: string;
}

interface VerifyResponse {
  message: string;
  redemption: RedemptionResult;
}

interface SearchResponse {
  results: RedemptionSearchResult[];
}

function validateCode(code: string): string | null {
  if (!code.trim()) {
    return 'Verification code is required';
  }
  if (!/^\d{6}$/.test(code.trim())) {
    return 'Code must be exactly 6 digits';
  }
  return null;
}

function validateSearchInput(phone: string, code: string): string | null {
  if (!phone.trim() && !code.trim()) {
    return 'Enter a phone number or verification code to search';
  }
  if (phone.trim() && !/^\+880\d{10}$/.test(phone.trim())) {
    return 'Phone number must be in E.164 format with +880 prefix (e.g., +8801712345678)';
  }
  if (code.trim() && !/^\d{6}$/.test(code.trim())) {
    return 'Verification code must be exactly 6 digits';
  }
  return null;
}

export function RedemptionsPage() {
  const [searchParams, setSearchParams] = useSearchParams();
  const [showUnauthorized, setShowUnauthorized] = useState(false);
  const { showToast } = useToast();

  // Verification form state
  const [verifyCode, setVerifyCode] = useState('');
  const [verifyError, setVerifyError] = useState('');
  const [verifyLoading, setVerifyLoading] = useState(false);
  const [verifyResult, setVerifyResult] = useState<RedemptionResult | null>(null);
  const [verifySuccessMessage, setVerifySuccessMessage] = useState('');

  // Search form state
  const [searchPhone, setSearchPhone] = useState('');
  const [searchCode, setSearchCode] = useState('');
  const [searchError, setSearchError] = useState('');
  const [searchLoading, setSearchLoading] = useState(false);
  const [searchResults, setSearchResults] = useState<RedemptionSearchResult[]>([]);
  const [hasSearched, setHasSearched] = useState(false);

  useEffect(() => {
    if (searchParams.get('unauthorized') === 'true') {
      setShowUnauthorized(true);
      setSearchParams({}, { replace: true });
      const timer = setTimeout(() => setShowUnauthorized(false), 5000);
      return () => clearTimeout(timer);
    }
  }, [searchParams, setSearchParams]);

  const handleVerifySubmit = async (e: React.FormEvent) => {
    e.preventDefault();
    setVerifyResult(null);
    setVerifySuccessMessage('');

    const error = validateCode(verifyCode);
    if (error) {
      setVerifyError(error);
      return;
    }

    setVerifyError('');
    setVerifyLoading(true);

    try {
      const response = await apiClient.post<VerifyResponse>('/redemptions/verify', {
        code: verifyCode.trim(),
      });
      setVerifyResult(response.redemption);
      setVerifySuccessMessage(response.message);
      showToast('success', response.message);
      setVerifyCode('');
    } catch (err) {
      if (err instanceof ApiError) {
        const body = err.body as { error?: string; message?: string } | undefined;
        const message = body?.message || getErrorMessageForStatus(err.status);
        setVerifyError(message);
      } else {
        setVerifyError('An unexpected error occurred. Please try again.');
      }
    } finally {
      setVerifyLoading(false);
    }
  };

  const handleSearchSubmit = async (e: React.FormEvent) => {
    e.preventDefault();
    setSearchResults([]);

    const error = validateSearchInput(searchPhone, searchCode);
    if (error) {
      setSearchError(error);
      return;
    }

    setSearchError('');
    setSearchLoading(true);
    setHasSearched(true);

    try {
      const params = new URLSearchParams();
      if (searchPhone.trim()) params.set('phone', searchPhone.trim());
      if (searchCode.trim()) params.set('code', searchCode.trim());

      const response = await apiClient.get<SearchResponse>(
        `/redemptions/search?${params.toString()}`
      );
      setSearchResults(response.results);
      if (response.results.length === 0) {
        showToast('error', 'No redemption records found matching your search.');
      }
    } catch (err) {
      if (err instanceof ApiError) {
        const body = err.body as { message?: string } | undefined;
        setSearchError(body?.message || 'Search failed. Please try again.');
      } else {
        setSearchError('An unexpected error occurred. Please try again.');
      }
    } finally {
      setSearchLoading(false);
    }
  };

  return (
    <div>
      <Helmet>
        <title>Redemptions | Vision Emporium Loyalty</title>
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

      <h1 className="text-2xl font-bold text-gray-900 mb-6">Redemptions</h1>

      {/* Verification Form */}
      <div className="rounded-lg bg-white p-6 shadow mb-6">
        <h2 className="text-lg font-semibold text-gray-900 mb-4">
          Verify Redemption Code
        </h2>
        <p className="text-sm text-gray-600 mb-4">
          Enter the customer's 6-digit verification code to process their gift redemption.
        </p>

        <form onSubmit={handleVerifySubmit} noValidate className="space-y-4">
          <div>
            <label
              htmlFor="verify-code"
              className="block text-sm font-medium text-gray-700"
            >
              Verification Code
            </label>
            <input
              id="verify-code"
              type="text"
              inputMode="numeric"
              maxLength={6}
              value={verifyCode}
              onChange={(e) => {
                const val = e.target.value.replace(/\D/g, '').slice(0, 6);
                setVerifyCode(val);
                if (verifyError) setVerifyError('');
              }}
              className={`mt-1 block w-full max-w-xs rounded-md border px-3 py-2 text-gray-900 placeholder-gray-400 focus:outline-none focus:ring-1 sm:text-sm ${
                verifyError
                  ? 'border-red-300 focus:border-red-500 focus:ring-red-500'
                  : 'border-gray-300 focus:border-blue-500 focus:ring-blue-500'
              }`}
              placeholder="000000"
              disabled={verifyLoading}
              aria-describedby={verifyError ? 'verify-code-error' : undefined}
              aria-invalid={!!verifyError}
            />
            {verifyError && (
              <p
                id="verify-code-error"
                className="mt-1 text-sm text-red-600"
                role="alert"
              >
                {verifyError}
              </p>
            )}
          </div>

          <button
            type="submit"
            disabled={verifyLoading}
            className="rounded-md bg-blue-600 px-4 py-2 text-sm font-semibold text-white shadow-sm hover:bg-blue-500 focus-visible:outline-2 focus-visible:outline-offset-2 focus-visible:outline-blue-600 disabled:opacity-50 disabled:cursor-not-allowed"
          >
            {verifyLoading ? 'Verifying...' : 'Verify & Redeem'}
          </button>
        </form>

        <LoadingIndicator isLoading={verifyLoading} label="Verifying code..." />

        {/* Success Result */}
        {verifyResult && (
          <div className="mt-4 rounded-md bg-green-50 border border-green-200 p-4" role="status">
            <h3 className="text-sm font-semibold text-green-800 mb-2">
              {verifySuccessMessage}
            </h3>
            <dl className="grid grid-cols-1 gap-x-4 gap-y-2 sm:grid-cols-2 text-sm">
              <div>
                <dt className="font-medium text-green-700">Code</dt>
                <dd className="text-green-900">{verifyResult.code}</dd>
              </div>
              <div>
                <dt className="font-medium text-green-700">Gift Type</dt>
                <dd className="text-green-900">{verifyResult.giftType}</dd>
              </div>
              <div>
                <dt className="font-medium text-green-700">Description</dt>
                <dd className="text-green-900">{verifyResult.giftDescription}</dd>
              </div>
              <div>
                <dt className="font-medium text-green-700">Redeemed At</dt>
                <dd className="text-green-900">
                  {new Date(verifyResult.redeemedAt).toLocaleString('en-BD', {
                    timeZone: 'Asia/Dhaka',
                  })}
                </dd>
              </div>
            </dl>
          </div>
        )}
      </div>

      {/* Search Section */}
      <div className="rounded-lg bg-white p-6 shadow">
        <h2 className="text-lg font-semibold text-gray-900 mb-4">
          Search Redemptions
        </h2>
        <p className="text-sm text-gray-600 mb-4">
          Look up redemption records by customer phone number or verification code.
        </p>

        <form onSubmit={handleSearchSubmit} noValidate className="space-y-4">
          <div className="grid grid-cols-1 gap-4 sm:grid-cols-2">
            <div>
              <label
                htmlFor="search-phone"
                className="block text-sm font-medium text-gray-700"
              >
                Phone Number
              </label>
              <input
                id="search-phone"
                type="tel"
                value={searchPhone}
                onChange={(e) => {
                  setSearchPhone(e.target.value);
                  if (searchError) setSearchError('');
                }}
                className="mt-1 block w-full rounded-md border border-gray-300 px-3 py-2 text-gray-900 placeholder-gray-400 focus:border-blue-500 focus:outline-none focus:ring-1 focus:ring-blue-500 sm:text-sm"
                placeholder="+8801712345678"
                disabled={searchLoading}
              />
            </div>
            <div>
              <label
                htmlFor="search-code"
                className="block text-sm font-medium text-gray-700"
              >
                Verification Code
              </label>
              <input
                id="search-code"
                type="text"
                inputMode="numeric"
                maxLength={6}
                value={searchCode}
                onChange={(e) => {
                  const val = e.target.value.replace(/\D/g, '').slice(0, 6);
                  setSearchCode(val);
                  if (searchError) setSearchError('');
                }}
                className="mt-1 block w-full rounded-md border border-gray-300 px-3 py-2 text-gray-900 placeholder-gray-400 focus:border-blue-500 focus:outline-none focus:ring-1 focus:ring-blue-500 sm:text-sm"
                placeholder="000000"
                disabled={searchLoading}
              />
            </div>
          </div>

          {searchError && (
            <p className="text-sm text-red-600" role="alert">
              {searchError}
            </p>
          )}

          <button
            type="submit"
            disabled={searchLoading}
            className="rounded-md bg-blue-600 px-4 py-2 text-sm font-semibold text-white shadow-sm hover:bg-blue-500 focus-visible:outline-2 focus-visible:outline-offset-2 focus-visible:outline-blue-600 disabled:opacity-50 disabled:cursor-not-allowed"
          >
            {searchLoading ? 'Searching...' : 'Search'}
          </button>
        </form>

        <LoadingIndicator isLoading={searchLoading} label="Searching..." />

        {/* Search Results */}
        {hasSearched && !searchLoading && (
          <div className="mt-4">
            {searchResults.length === 0 ? (
              <p className="text-sm text-gray-500">No redemption records found.</p>
            ) : (
              <div className="overflow-x-auto">
                <table className="w-full text-left text-sm">
                  <thead className="border-b border-gray-200 bg-gray-50">
                    <tr>
                      <th className="px-4 py-3 text-xs font-semibold uppercase tracking-wider text-gray-600">
                        Code
                      </th>
                      <th className="px-4 py-3 text-xs font-semibold uppercase tracking-wider text-gray-600">
                        Customer
                      </th>
                      <th className="px-4 py-3 text-xs font-semibold uppercase tracking-wider text-gray-600">
                        Gift
                      </th>
                      <th className="px-4 py-3 text-xs font-semibold uppercase tracking-wider text-gray-600">
                        Status
                      </th>
                      <th className="px-4 py-3 text-xs font-semibold uppercase tracking-wider text-gray-600">
                        Outlet
                      </th>
                      <th className="px-4 py-3 text-xs font-semibold uppercase tracking-wider text-gray-600">
                        Issued
                      </th>
                    </tr>
                  </thead>
                  <tbody className="divide-y divide-gray-100">
                    {searchResults.map((result) => (
                      <tr key={result.code} className="hover:bg-gray-50">
                        <td className="px-4 py-3 font-mono text-gray-900">
                          {result.code}
                        </td>
                        <td className="px-4 py-3 text-gray-700">
                          <div>{result.customerName}</div>
                          <div className="text-xs text-gray-500">
                            {result.phoneNumber}
                          </div>
                        </td>
                        <td className="px-4 py-3 text-gray-700">
                          <div>{result.giftType}</div>
                          <div className="text-xs text-gray-500">
                            {result.giftDescription}
                          </div>
                        </td>
                        <td className="px-4 py-3">
                          <StatusBadge status={result.status} />
                        </td>
                        <td className="px-4 py-3 text-gray-700">
                          {result.designatedOutlet}
                        </td>
                        <td className="px-4 py-3 text-gray-700">
                          {new Date(result.issuedAt).toLocaleDateString('en-BD', {
                            timeZone: 'Asia/Dhaka',
                          })}
                        </td>
                      </tr>
                    ))}
                  </tbody>
                </table>
              </div>
            )}
          </div>
        )}
      </div>
    </div>
  );
}

function StatusBadge({ status }: { status: string }) {
  const styles: Record<string, string> = {
    Active: 'bg-green-100 text-green-800',
    Redeemed: 'bg-blue-100 text-blue-800',
    Expired: 'bg-gray-100 text-gray-800',
  };

  return (
    <span
      className={`inline-flex items-center rounded-full px-2.5 py-0.5 text-xs font-medium ${
        styles[status] || 'bg-gray-100 text-gray-800'
      }`}
    >
      {status}
    </span>
  );
}

function getErrorMessageForStatus(status: number): string {
  switch (status) {
    case 400:
      return 'Invalid verification code format.';
    case 404:
      return 'Verification code not found. Please check and try again.';
    case 429:
      return 'Too many failed attempts. Please wait before trying again.';
    default:
      return 'An error occurred while verifying the code.';
  }
}
