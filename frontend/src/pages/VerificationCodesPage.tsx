import { useState, useEffect, useMemo } from 'react';
import { Helmet } from 'react-helmet-async';
import { apiClient, ApiError } from '../utils/api';
import { DataTable, LoadingIndicator, useToast } from '../components';
import type { Column } from '../components/DataTable';

interface VerificationCodeItem {
  code: string;
  customerId: string;
  customerPhone: string;
  outletId: string;
  outletName: string;
  tier: number;
  giftType: string;
  giftDescription: string;
  giftValue: number;
  status: string;
  issuedAt: string;
  expiresAt: string;
}

interface VerificationCodesResponse {
  codes: VerificationCodeItem[];
}

type StatusFilter = 'All' | 'Active' | 'Redeemed' | 'Expired';

const STATUS_FILTERS: StatusFilter[] = ['All', 'Active', 'Redeemed', 'Expired'];

export function VerificationCodesPage() {
  const { showToast } = useToast();
  const [codes, setCodes] = useState<VerificationCodeItem[]>([]);
  const [loading, setLoading] = useState(true);
  const [statusFilter, setStatusFilter] = useState<StatusFilter>('All');
  const [search, setSearch] = useState('');
  const [currentPage, setCurrentPage] = useState(1);
  const [pageSize, setPageSize] = useState(25);

  useEffect(() => {
    fetchCodes();
  }, [statusFilter]);

  const fetchCodes = async () => {
    setLoading(true);
    try {
      const params = statusFilter !== 'All' ? `?status=${statusFilter}` : '';
      const response = await apiClient.get<VerificationCodesResponse>(
        `/verification-codes${params}`
      );
      setCodes(response.codes);
    } catch (err) {
      if (err instanceof ApiError) {
        showToast('error', 'Failed to load verification codes.');
      } else {
        showToast('error', 'An unexpected error occurred.');
      }
    } finally {
      setLoading(false);
    }
  };

  const filteredCodes = useMemo(() => {
    if (!search.trim()) return codes;
    const term = search.trim().toLowerCase();
    return codes.filter(
      (c) =>
        c.code.includes(term) ||
        c.customerPhone.toLowerCase().includes(term)
    );
  }, [codes, search]);

  const paginatedCodes = useMemo(() => {
    const start = (currentPage - 1) * pageSize;
    return filteredCodes.slice(start, start + pageSize);
  }, [filteredCodes, currentPage, pageSize]);

  // Reset to page 1 when filter/search changes
  useEffect(() => {
    setCurrentPage(1);
  }, [search, statusFilter]);

  const columns: Column<VerificationCodeItem>[] = [
    {
      key: 'code',
      header: 'Code',
      render: (row) => (
        <span className="font-mono font-medium text-gray-900">{row.code}</span>
      ),
    },
    {
      key: 'customer',
      header: 'Customer',
      render: (row) => (
        <span className="text-gray-700">{row.customerPhone}</span>
      ),
    },
    {
      key: 'outlet',
      header: 'Outlet',
      render: (row) => (
        <span className="text-gray-700">{row.outletName}</span>
      ),
    },
    {
      key: 'tier',
      header: 'Tier',
      render: (row) => (
        <span className="text-gray-700">{row.tier}</span>
      ),
    },
    {
      key: 'gift',
      header: 'Gift',
      render: (row) => (
        <div>
          <div className="text-gray-900">{row.giftType}</div>
          <div className="text-xs text-gray-500">{row.giftDescription}</div>
        </div>
      ),
    },
    {
      key: 'status',
      header: 'Status',
      render: (row) => <StatusBadge status={row.status} />,
    },
    {
      key: 'issuedAt',
      header: 'Issued At',
      render: (row) => (
        <span className="text-gray-700">
          {new Date(row.issuedAt).toLocaleString('en-BD', {
            timeZone: 'Asia/Dhaka',
            dateStyle: 'medium',
            timeStyle: 'short',
          })}
        </span>
      ),
    },
    {
      key: 'expiresAt',
      header: 'Expires At',
      render: (row) => (
        <span className="text-gray-700">
          {new Date(row.expiresAt).toLocaleString('en-BD', {
            timeZone: 'Asia/Dhaka',
            dateStyle: 'medium',
            timeStyle: 'short',
          })}
        </span>
      ),
    },
  ];

  return (
    <div>
      <Helmet>
        <title>Verification Codes | Vision Emporium Loyalty</title>
      </Helmet>

      <div className="mb-6">
        <h1 className="text-2xl font-bold text-gray-900">Verification Codes</h1>
        <p className="mt-1 text-sm text-gray-600">
          All OTPs issued to eligible customers for gift redemption.
        </p>
      </div>

      {/* Filters */}
      <div className="mb-4 flex flex-col gap-4 sm:flex-row sm:items-center sm:justify-between">
        {/* Status filter tabs */}
        <div className="flex gap-1 rounded-lg bg-gray-100 p-1">
          {STATUS_FILTERS.map((filter) => (
            <button
              key={filter}
              onClick={() => setStatusFilter(filter)}
              className={`rounded-md px-3 py-1.5 text-sm font-medium transition-colors ${
                statusFilter === filter
                  ? 'bg-white text-[#E31E24] shadow-sm'
                  : 'text-gray-600 hover:text-gray-900'
              }`}
            >
              {filter}
            </button>
          ))}
        </div>

        {/* Search */}
        <div className="relative">
          <input
            type="text"
            placeholder="Search by code or phone..."
            value={search}
            onChange={(e) => setSearch(e.target.value)}
            className="w-full rounded-md border border-gray-300 px-3 py-2 pl-9 text-sm text-gray-900 placeholder-gray-400 focus:border-[#E31E24] focus:outline-none focus:ring-1 focus:ring-[#E31E24] sm:w-64"
            aria-label="Search verification codes"
          />
          <svg
            className="absolute left-3 top-2.5 h-4 w-4 text-gray-400"
            fill="none"
            viewBox="0 0 24 24"
            stroke="currentColor"
            strokeWidth={2}
          >
            <path
              strokeLinecap="round"
              strokeLinejoin="round"
              d="M21 21l-6-6m2-5a7 7 0 11-14 0 7 7 0 0114 0z"
            />
          </svg>
        </div>
      </div>

      <LoadingIndicator isLoading={loading} label="Loading verification codes..." />

      {!loading && (
        <DataTable
          columns={columns}
          data={paginatedCodes}
          getRowKey={(row) => `${row.code}-${row.customerId}`}
          currentPage={currentPage}
          pageSize={pageSize}
          totalItems={filteredCodes.length}
          onPageChange={setCurrentPage}
          onPageSizeChange={(size) => {
            setPageSize(size);
            setCurrentPage(1);
          }}
          emptyMessage="No verification codes found."
        />
      )}
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
