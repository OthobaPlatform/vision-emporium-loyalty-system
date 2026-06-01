import { useState, useEffect, useMemo } from 'react';
import { Helmet } from 'react-helmet-async';
import { apiClient, ApiError } from '../utils/api';
import { DataTable, LoadingIndicator, useToast, type Column } from '../components';

interface CustomerListItem {
  customerId: string;
  name: string;
  phoneNumber: string;
  qualifyingPurchases: number;
}

interface ProgressInfo {
  currentPurchases: number;
  nextThreshold: number | null;
  nextThresholdTier: number | null;
  isComplete: boolean;
  description: string;
}

interface CustomerProfileResponse {
  customerId: string;
  name: string;
  phoneNumber: string;
  qualifyingPurchases: number;
  currentCycleId: string;
  progress: ProgressInfo;
}

interface VerificationCodeResponse {
  code: string;
  status: string;
  giftTier: number;
  giftType: string;
  giftDescription: string;
  giftValue: number;
  designatedOutlet: string;
  issuedAt: string;
}

interface CustomerCodesResponse {
  customerId: string;
  name: string;
  phoneNumber: string;
  codes: VerificationCodeResponse[];
}

export function CustomersPage() {
  const { showToast } = useToast();

  // List state
  const [customers, setCustomers] = useState<CustomerListItem[]>([]);
  const [isLoading, setIsLoading] = useState(true);
  const [search, setSearch] = useState('');

  // Pagination state
  const [currentPage, setCurrentPage] = useState(1);
  const [pageSize, setPageSize] = useState(25);

  // Detail panel state
  const [selectedCustomer, setSelectedCustomer] = useState<CustomerListItem | null>(null);
  const [customerProfile, setCustomerProfile] = useState<CustomerProfileResponse | null>(null);
  const [customerCodes, setCustomerCodes] = useState<CustomerCodesResponse | null>(null);
  const [isLoadingDetail, setIsLoadingDetail] = useState(false);

  // Fetch all customers on mount
  useEffect(() => {
    fetchCustomers();
  }, []);

  const fetchCustomers = async () => {
    setIsLoading(true);
    try {
      const data = await apiClient.get<CustomerListItem[]>('/customers');
      setCustomers(data);
    } catch (err) {
      if (err instanceof ApiError) {
        showToast('error', 'Failed to load customers.');
      }
    } finally {
      setIsLoading(false);
    }
  };

  // Client-side filtering by phone number
  const filteredCustomers = useMemo(() => {
    if (!search.trim()) return customers;
    return customers.filter((c) =>
      c.phoneNumber.toLowerCase().includes(search.toLowerCase())
    );
  }, [customers, search]);

  // Reset to page 1 when search changes
  useEffect(() => {
    setCurrentPage(1);
  }, [search]);

  // Paginated data
  const paginatedData = useMemo(() => {
    const start = (currentPage - 1) * pageSize;
    return filteredCustomers.slice(start, start + pageSize);
  }, [filteredCustomers, currentPage, pageSize]);

  // Handle row click - load detail
  const handleRowClick = async (customer: CustomerListItem) => {
    setSelectedCustomer(customer);
    setCustomerProfile(null);
    setCustomerCodes(null);

    // Use phoneNumber or customerId for the lookup
    const lookupId = customer.phoneNumber || customer.customerId;
    if (!lookupId) {
      setIsLoadingDetail(false);
      return;
    }

    setIsLoadingDetail(true);

    try {
      const encodedId = encodeURIComponent(lookupId);
      const [profile, codes] = await Promise.all([
        apiClient.get<CustomerProfileResponse>(`/customers/${encodedId}`).catch(() => null),
        apiClient.get<CustomerCodesResponse>(`/customers/${encodedId}/codes`).catch(() => null),
      ]);
      if (profile) setCustomerProfile(profile);
      if (codes) setCustomerCodes(codes);
    } catch (err) {
      if (err instanceof ApiError) {
        showToast('error', 'Failed to load customer details.');
      }
    } finally {
      setIsLoadingDetail(false);
    }
  };

  const columns: Column<CustomerListItem>[] = [
    {
      key: 'name',
      header: 'Name',
      render: (row) => (
        <button
          type="button"
          className="text-left font-medium text-blue-700 hover:text-blue-900 hover:underline"
          onClick={() => handleRowClick(row)}
        >
          {row.name || row.phoneNumber}
        </button>
      ),
    },
    {
      key: 'phone',
      header: 'Phone',
      render: (row) => row.phoneNumber,
    },
    {
      key: 'purchases',
      header: 'Purchases',
      render: (row) => row.qualifyingPurchases,
    },
  ];

  return (
    <div>
      <Helmet>
        <title>Customers | Vision Emporium Loyalty</title>
      </Helmet>

      <h1 className="text-2xl font-bold text-gray-900 mb-6">Customers</h1>

      {/* Search Box */}
      <div className="mb-4">
        <input
          type="text"
          value={search}
          onChange={(e) => setSearch(e.target.value)}
          placeholder="Search by phone number..."
          className="block w-full max-w-sm rounded-md border border-gray-300 px-3 py-2 text-gray-900 placeholder-gray-400 focus:border-blue-500 focus:outline-none focus:ring-1 focus:ring-blue-500 sm:text-sm"
          aria-label="Search customers by phone number"
        />
      </div>

      {/* Customer List Table */}
      <div className="mb-6">
        <DataTable
          columns={columns}
          data={paginatedData}
          getRowKey={(row) => row.customerId}
          currentPage={currentPage}
          pageSize={pageSize}
          totalItems={filteredCustomers.length}
          onPageChange={setCurrentPage}
          onPageSizeChange={(size) => {
            setPageSize(size);
            setCurrentPage(1);
          }}
          isLoading={isLoading}
          emptyMessage={search ? 'No customers match your search.' : 'No customers found.'}
        />
      </div>

      {/* Detail Panel */}
      {selectedCustomer && (
        <CustomerDetailPanel
          customer={selectedCustomer}
          profile={customerProfile}
          codes={customerCodes}
          isLoading={isLoadingDetail}
          onClose={() => setSelectedCustomer(null)}
        />
      )}
    </div>
  );
}

// ─── Detail Panel ───────────────────────────────────────────────────────────────

interface CustomerDetailPanelProps {
  customer: CustomerListItem;
  profile: CustomerProfileResponse | null;
  codes: CustomerCodesResponse | null;
  isLoading: boolean;
  onClose: () => void;
}

function CustomerDetailPanel({
  customer,
  profile,
  codes,
  isLoading,
  onClose,
}: CustomerDetailPanelProps) {
  return (
    <div className="fixed inset-0 z-50 flex items-center justify-center bg-black/40">
      <div
        className="relative w-full max-w-2xl max-h-[90vh] overflow-y-auto rounded-lg bg-white p-6 shadow-xl"
        role="dialog"
        aria-modal="true"
        aria-label={`Customer details for ${customer.name || customer.phoneNumber}`}
      >
        {/* Close button */}
        <button
          type="button"
          onClick={onClose}
          className="absolute top-4 right-4 text-gray-400 hover:text-gray-600"
          aria-label="Close detail panel"
        >
          <svg className="h-5 w-5" fill="none" viewBox="0 0 24 24" stroke="currentColor">
            <path strokeLinecap="round" strokeLinejoin="round" strokeWidth={2} d="M6 18L18 6M6 6l12 12" />
          </svg>
        </button>

        <h2 className="text-xl font-bold text-gray-900 mb-4">Customer Details</h2>

        {isLoading ? (
          <LoadingIndicator isLoading={true} label="Loading customer details..." />
        ) : (
          <div className="space-y-6">
            {/* Profile Info */}
            <div>
              <dl className="grid grid-cols-1 gap-x-6 gap-y-3 sm:grid-cols-2">
                <div>
                  <dt className="text-sm font-medium text-gray-500">Name</dt>
                  <dd className="mt-1 text-sm text-gray-900">
                    {profile?.name || customer.name || '—'}
                  </dd>
                </div>
                <div>
                  <dt className="text-sm font-medium text-gray-500">Phone</dt>
                  <dd className="mt-1 text-sm text-gray-900">{customer.phoneNumber}</dd>
                </div>
                <div>
                  <dt className="text-sm font-medium text-gray-500">Qualifying Purchases</dt>
                  <dd className="mt-1 text-sm text-gray-900">
                    {profile?.qualifyingPurchases ?? customer.qualifyingPurchases}
                  </dd>
                </div>
              </dl>
            </div>

            {/* Progress */}
            {profile?.progress && (
              <div>
                <h3 className="text-sm font-semibold text-gray-700 mb-2">
                  Progress Toward Next Reward
                </h3>
                {profile.progress.isComplete ? (
                  <div className="rounded-md bg-green-50 border border-green-200 p-3">
                    <p className="text-sm font-medium text-green-800">
                      All reward tiers achieved!
                    </p>
                  </div>
                ) : profile.progress.nextThreshold !== null ? (
                  <div>
                    <p className="text-sm text-gray-700 mb-2">
                      <span className="font-semibold">{profile.progress.currentPurchases}</span>
                      {' '}of{' '}
                      <span className="font-semibold">{profile.progress.nextThreshold}</span>
                      {' '}purchases
                      {profile.progress.nextThresholdTier !== null && (
                        <span className="text-gray-500"> (Tier {profile.progress.nextThresholdTier})</span>
                      )}
                    </p>
                    <div className="w-full bg-gray-200 rounded-full h-3">
                      <div
                        className="h-3 rounded-full transition-all"
                        style={{
                          width: `${Math.min(
                            100,
                            (profile.progress.currentPurchases / profile.progress.nextThreshold) * 100
                          )}%`,
                          backgroundColor: '#E31837',
                        }}
                        role="progressbar"
                        aria-valuenow={profile.progress.currentPurchases}
                        aria-valuemin={0}
                        aria-valuemax={profile.progress.nextThreshold}
                        aria-label={`${profile.progress.currentPurchases} of ${profile.progress.nextThreshold} purchases`}
                      />
                    </div>
                  </div>
                ) : (
                  <p className="text-sm text-gray-500">{profile.progress.description}</p>
                )}
              </div>
            )}

            {/* Verification Codes */}
            <div>
              <h3 className="text-sm font-semibold text-gray-700 mb-2">
                Verification Codes
              </h3>
              {!codes || codes.codes.length === 0 ? (
                <p className="text-sm text-gray-500">
                  No verification codes issued for this customer.
                </p>
              ) : (
                <div className="overflow-x-auto">
                  <table className="w-full text-left text-sm">
                    <thead className="border-b border-gray-200 bg-gray-50">
                      <tr>
                        <th className="px-3 py-2 text-xs font-semibold uppercase tracking-wider text-gray-600">
                          Code
                        </th>
                        <th className="px-3 py-2 text-xs font-semibold uppercase tracking-wider text-gray-600">
                          Tier
                        </th>
                        <th className="px-3 py-2 text-xs font-semibold uppercase tracking-wider text-gray-600">
                          Gift
                        </th>
                        <th className="px-3 py-2 text-xs font-semibold uppercase tracking-wider text-gray-600">
                          Status
                        </th>
                        <th className="px-3 py-2 text-xs font-semibold uppercase tracking-wider text-gray-600">
                          Outlet
                        </th>
                        <th className="px-3 py-2 text-xs font-semibold uppercase tracking-wider text-gray-600">
                          Issued
                        </th>
                      </tr>
                    </thead>
                    <tbody className="divide-y divide-gray-100">
                      {codes.codes.map((code) => (
                        <tr key={code.code} className="hover:bg-gray-50">
                          <td className="px-3 py-2 font-mono text-gray-900">{code.code}</td>
                          <td className="px-3 py-2 text-gray-700">Tier {code.giftTier}</td>
                          <td className="px-3 py-2 text-gray-700">
                            <div>{code.giftType}</div>
                            <div className="text-xs text-gray-500">{code.giftDescription}</div>
                          </td>
                          <td className="px-3 py-2">
                            <CodeStatusBadge status={code.status} />
                          </td>
                          <td className="px-3 py-2 text-gray-700">{code.designatedOutlet}</td>
                          <td className="px-3 py-2 text-gray-700">
                            {new Date(code.issuedAt).toLocaleDateString('en-BD', {
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
