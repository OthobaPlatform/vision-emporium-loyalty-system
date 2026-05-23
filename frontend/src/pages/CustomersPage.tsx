import { useState } from 'react';
import { apiClient, ApiError } from '../utils/api';
import { LoadingIndicator, useToast } from '../components';

interface CustomerProgress {
  current: number;
  target: number;
  nextThreshold: number | null;
}

interface VerificationCodeInfo {
  code: string;
  tier: number;
  giftType: string;
  giftDescription: string;
  status: string;
  designatedOutlet: string;
  issuedAt: string;
  redeemedAt?: string;
  expiresAt: string;
}

interface CustomerProfile {
  customerId: string;
  name: string;
  phoneNumber: string;
  qualifyingPurchases: number;
  progress: CustomerProgress;
  codes: VerificationCodeInfo[];
}

function validatePhoneNumber(phone: string): string | null {
  if (!phone.trim()) {
    return 'Phone number is required';
  }
  if (!/^\+880\d{10}$/.test(phone.trim())) {
    return 'Phone number must be in E.164 format with +880 prefix (e.g., +8801712345678)';
  }
  return null;
}

export function CustomersPage() {
  const { showToast } = useToast();

  // Search state
  const [phone, setPhone] = useState('');
  const [phoneError, setPhoneError] = useState('');
  const [isLoading, setIsLoading] = useState(false);
  const [customer, setCustomer] = useState<CustomerProfile | null>(null);
  const [hasSearched, setHasSearched] = useState(false);
  const [notFound, setNotFound] = useState(false);

  const handleSearch = async (e: React.FormEvent) => {
    e.preventDefault();
    setCustomer(null);
    setNotFound(false);

    const error = validatePhoneNumber(phone);
    if (error) {
      setPhoneError(error);
      return;
    }

    setPhoneError('');
    setIsLoading(true);
    setHasSearched(true);

    try {
      const encodedPhone = encodeURIComponent(phone.trim());
      const profile = await apiClient.get<CustomerProfile>(
        `/customers/${encodedPhone}`
      );
      setCustomer(profile);
    } catch (err) {
      if (err instanceof ApiError) {
        if (err.status === 404) {
          setNotFound(true);
        } else {
          const body = err.body as { message?: string } | undefined;
          setPhoneError(body?.message || 'Failed to look up customer. Please try again.');
          showToast('error', 'Customer lookup failed.');
        }
      } else {
        setPhoneError('An unexpected error occurred. Please try again.');
      }
    } finally {
      setIsLoading(false);
    }
  };

  return (
    <div>
      <h1 className="text-2xl font-bold text-gray-900 mb-6">Customer Lookup</h1>

      {/* Search Form */}
      <div className="rounded-lg bg-white p-6 shadow mb-6">
        <h2 className="text-lg font-semibold text-gray-900 mb-4">
          Search by Phone Number
        </h2>
        <p className="text-sm text-gray-600 mb-4">
          Enter a customer's phone number to view their loyalty profile and purchase progress.
        </p>

        <form onSubmit={handleSearch} noValidate className="space-y-4">
          <div>
            <label
              htmlFor="customer-phone"
              className="block text-sm font-medium text-gray-700"
            >
              Phone Number
            </label>
            <input
              id="customer-phone"
              type="tel"
              value={phone}
              onChange={(e) => {
                setPhone(e.target.value);
                if (phoneError) setPhoneError('');
              }}
              className={`mt-1 block w-full max-w-sm rounded-md border px-3 py-2 text-gray-900 placeholder-gray-400 focus:outline-none focus:ring-1 sm:text-sm ${
                phoneError
                  ? 'border-red-300 focus:border-red-500 focus:ring-red-500'
                  : 'border-gray-300 focus:border-blue-500 focus:ring-blue-500'
              }`}
              placeholder="+8801712345678"
              disabled={isLoading}
              aria-describedby={phoneError ? 'customer-phone-error' : undefined}
              aria-invalid={!!phoneError}
            />
            {phoneError && (
              <p
                id="customer-phone-error"
                className="mt-1 text-sm text-red-600"
                role="alert"
              >
                {phoneError}
              </p>
            )}
          </div>

          <button
            type="submit"
            disabled={isLoading}
            className="rounded-md bg-blue-600 px-4 py-2 text-sm font-semibold text-white shadow-sm hover:bg-blue-500 focus-visible:outline-2 focus-visible:outline-offset-2 focus-visible:outline-blue-600 disabled:opacity-50 disabled:cursor-not-allowed"
          >
            {isLoading ? 'Searching...' : 'Look Up Customer'}
          </button>
        </form>

        <LoadingIndicator isLoading={isLoading} label="Looking up customer..." />
      </div>

      {/* Not Found Message */}
      {hasSearched && notFound && !isLoading && (
        <div
          className="rounded-lg bg-yellow-50 border border-yellow-200 p-6 mb-6"
          role="status"
        >
          <p className="text-sm text-yellow-800">
            No customer found with this phone number. Please verify the number and try again.
          </p>
        </div>
      )}

      {/* Customer Profile */}
      {customer && (
        <div className="space-y-6">
          {/* Profile Card */}
          <div className="rounded-lg bg-white p-6 shadow">
            <h2 className="text-lg font-semibold text-gray-900 mb-4">
              Customer Profile
            </h2>
            <dl className="grid grid-cols-1 gap-x-6 gap-y-4 sm:grid-cols-2 lg:grid-cols-3">
              <div>
                <dt className="text-sm font-medium text-gray-500">Name</dt>
                <dd className="mt-1 text-sm text-gray-900">{customer.name}</dd>
              </div>
              <div>
                <dt className="text-sm font-medium text-gray-500">Phone Number</dt>
                <dd className="mt-1 text-sm text-gray-900">{customer.phoneNumber}</dd>
              </div>
              <div>
                <dt className="text-sm font-medium text-gray-500">
                  Qualifying Purchases (Current Cycle)
                </dt>
                <dd className="mt-1 text-sm text-gray-900">
                  {customer.qualifyingPurchases}
                </dd>
              </div>
            </dl>
          </div>

          {/* Progress Card */}
          <div className="rounded-lg bg-white p-6 shadow">
            <h2 className="text-lg font-semibold text-gray-900 mb-4">
              Purchase Progress
            </h2>
            {customer.progress.nextThreshold === null ? (
              <div className="rounded-md bg-green-50 border border-green-200 p-4">
                <p className="text-sm font-medium text-green-800">
                  All reward tiers achieved! This customer has completed all configured thresholds.
                </p>
              </div>
            ) : (
              <div>
                <p className="text-sm text-gray-700 mb-3">
                  <span className="font-semibold">{customer.progress.current}</span> of{' '}
                  <span className="font-semibold">{customer.progress.target}</span> purchases
                  toward next reward
                </p>
                <div className="w-full bg-gray-200 rounded-full h-3">
                  <div
                    className="bg-blue-600 h-3 rounded-full transition-all"
                    style={{
                      width: `${Math.min(
                        100,
                        (customer.progress.current / customer.progress.target) * 100
                      )}%`,
                    }}
                    role="progressbar"
                    aria-valuenow={customer.progress.current}
                    aria-valuemin={0}
                    aria-valuemax={customer.progress.target}
                    aria-label={`${customer.progress.current} of ${customer.progress.target} purchases`}
                  />
                </div>
              </div>
            )}
          </div>

          {/* Verification Codes */}
          <div className="rounded-lg bg-white p-6 shadow">
            <h2 className="text-lg font-semibold text-gray-900 mb-4">
              Verification Codes (Current Cycle)
            </h2>
            {customer.codes.length === 0 ? (
              <p className="text-sm text-gray-500">
                No verification codes issued for this customer in the current cycle.
              </p>
            ) : (
              <div className="overflow-x-auto">
                <table className="w-full text-left text-sm">
                  <thead className="border-b border-gray-200 bg-gray-50">
                    <tr>
                      <th className="px-4 py-3 text-xs font-semibold uppercase tracking-wider text-gray-600">
                        Code
                      </th>
                      <th className="px-4 py-3 text-xs font-semibold uppercase tracking-wider text-gray-600">
                        Tier
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
                      <th className="px-4 py-3 text-xs font-semibold uppercase tracking-wider text-gray-600">
                        Expires
                      </th>
                    </tr>
                  </thead>
                  <tbody className="divide-y divide-gray-100">
                    {customer.codes.map((code) => (
                      <tr key={code.code} className="hover:bg-gray-50">
                        <td className="px-4 py-3 font-mono text-gray-900">
                          {code.code}
                        </td>
                        <td className="px-4 py-3 text-gray-700">
                          Tier {code.tier}
                        </td>
                        <td className="px-4 py-3 text-gray-700">
                          <div>{code.giftType}</div>
                          <div className="text-xs text-gray-500">
                            {code.giftDescription}
                          </div>
                        </td>
                        <td className="px-4 py-3">
                          <CodeStatusBadge status={code.status} />
                        </td>
                        <td className="px-4 py-3 text-gray-700">
                          {code.designatedOutlet}
                        </td>
                        <td className="px-4 py-3 text-gray-700">
                          {new Date(code.issuedAt).toLocaleDateString('en-BD', {
                            timeZone: 'Asia/Dhaka',
                          })}
                        </td>
                        <td className="px-4 py-3 text-gray-700">
                          {new Date(code.expiresAt).toLocaleDateString('en-BD', {
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
        </div>
      )}
    </div>
  );
}

function CodeStatusBadge({ status }: { status: string }) {
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
