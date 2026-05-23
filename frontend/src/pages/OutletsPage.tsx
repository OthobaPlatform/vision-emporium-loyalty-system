import { useEffect, useState, useCallback } from 'react';
import { apiClient, ApiError } from '../utils/api';
import { useToast } from '../components/Toast';
import { LoadingIndicator } from '../components/LoadingIndicator';
import { DataTable, type Column } from '../components/DataTable';

interface Outlet {
  outletId: string;
  name: string;
  address: string;
  phoneNumber: string;
  assignedManagerId: string | null;
  isActive: boolean;
}

interface OutletFormData {
  name: string;
  address: string;
  phoneNumber: string;
  assignedManagerId: string;
}

interface FormErrors {
  name?: string;
  address?: string;
  phoneNumber?: string;
}

export function OutletsPage() {
  const { showToast } = useToast();
  const [outlets, setOutlets] = useState<Outlet[]>([]);
  const [isLoading, setIsLoading] = useState(true);
  const [error, setError] = useState<string | null>(null);
  const [showForm, setShowForm] = useState(false);
  const [editingOutlet, setEditingOutlet] = useState<Outlet | null>(null);
  const [formData, setFormData] = useState<OutletFormData>({
    name: '',
    address: '',
    phoneNumber: '',
    assignedManagerId: '',
  });
  const [formErrors, setFormErrors] = useState<FormErrors>({});
  const [isSubmitting, setIsSubmitting] = useState(false);
  const [statusError, setStatusError] = useState<string | null>(null);

  // Pagination
  const [currentPage, setCurrentPage] = useState(1);
  const [pageSize, setPageSize] = useState(10);

  const fetchOutlets = useCallback(async () => {
    try {
      setIsLoading(true);
      setError(null);
      const result = await apiClient.get<{ outlets: Outlet[] }>('/outlets');
      setOutlets(result.outlets);
    } catch (err) {
      if (err instanceof ApiError) {
        setError(err.message);
      } else {
        setError('Failed to load outlets');
      }
    } finally {
      setIsLoading(false);
    }
  }, []);

  useEffect(() => {
    fetchOutlets();
  }, [fetchOutlets]);

  function validateForm(): boolean {
    const errors: FormErrors = {};
    if (!formData.name.trim()) {
      errors.name = 'Name is required';
    }
    if (!formData.address.trim()) {
      errors.address = 'Address is required';
    }
    if (!formData.phoneNumber.trim()) {
      errors.phoneNumber = 'Phone number is required';
    }
    setFormErrors(errors);
    return Object.keys(errors).length === 0;
  }

  function openCreateForm() {
    setEditingOutlet(null);
    setFormData({ name: '', address: '', phoneNumber: '', assignedManagerId: '' });
    setFormErrors({});
    setShowForm(true);
  }

  function openEditForm(outlet: Outlet) {
    setEditingOutlet(outlet);
    setFormData({
      name: outlet.name,
      address: outlet.address,
      phoneNumber: outlet.phoneNumber,
      assignedManagerId: outlet.assignedManagerId || '',
    });
    setFormErrors({});
    setShowForm(true);
  }

  function closeForm() {
    setShowForm(false);
    setEditingOutlet(null);
    setFormErrors({});
  }

  async function handleSubmit(e: React.FormEvent) {
    e.preventDefault();
    if (!validateForm()) return;

    setIsSubmitting(true);
    try {
      const body = {
        name: formData.name.trim(),
        address: formData.address.trim(),
        phoneNumber: formData.phoneNumber.trim(),
        assignedManagerId: formData.assignedManagerId.trim() || null,
      };

      if (editingOutlet) {
        await apiClient.put(`/outlets/${editingOutlet.outletId}`, body);
        showToast('success', 'Outlet updated successfully');
      } else {
        await apiClient.post('/outlets', body);
        showToast('success', 'Outlet created successfully');
      }
      closeForm();
      await fetchOutlets();
    } catch (err) {
      if (err instanceof ApiError) {
        showToast('error', (err.body as { message?: string })?.message || err.message);
      } else {
        showToast('error', 'Failed to save outlet');
      }
    } finally {
      setIsSubmitting(false);
    }
  }

  async function handleToggleStatus(outlet: Outlet) {
    setStatusError(null);
    try {
      await apiClient.patch(`/outlets/${outlet.outletId}/status`, {
        isActive: !outlet.isActive,
      });
      showToast('success', `Outlet ${outlet.isActive ? 'deactivated' : 'activated'} successfully`);
      await fetchOutlets();
    } catch (err) {
      if (err instanceof ApiError) {
        const body = err.body as { message?: string } | undefined;
        const message = body?.message || err.message;
        // Show last-outlet protection message
        if (err.status === 400 || err.status === 409) {
          setStatusError(message);
        } else {
          showToast('error', message);
        }
      } else {
        showToast('error', 'Failed to update outlet status');
      }
    }
  }

  // Paginated data
  const paginatedOutlets = outlets.slice(
    (currentPage - 1) * pageSize,
    currentPage * pageSize
  );

  const columns: Column<Outlet>[] = [
    {
      key: 'name',
      header: 'Name',
      render: (outlet) => <span className="font-medium text-gray-900">{outlet.name}</span>,
    },
    {
      key: 'address',
      header: 'Address',
      render: (outlet) => outlet.address,
    },
    {
      key: 'phoneNumber',
      header: 'Phone',
      render: (outlet) => outlet.phoneNumber,
    },
    {
      key: 'status',
      header: 'Status',
      render: (outlet) => (
        <span
          className={`inline-flex items-center rounded-full px-2.5 py-0.5 text-xs font-medium ${
            outlet.isActive
              ? 'bg-green-100 text-green-800'
              : 'bg-red-100 text-red-800'
          }`}
        >
          {outlet.isActive ? 'Active' : 'Inactive'}
        </span>
      ),
    },
    {
      key: 'actions',
      header: 'Actions',
      render: (outlet) => (
        <div className="flex items-center gap-2">
          <button
            onClick={() => openEditForm(outlet)}
            className="text-sm text-blue-600 hover:text-blue-800 font-medium"
          >
            Edit
          </button>
          <button
            onClick={() => handleToggleStatus(outlet)}
            className={`text-sm font-medium ${
              outlet.isActive
                ? 'text-red-600 hover:text-red-800'
                : 'text-green-600 hover:text-green-800'
            }`}
          >
            {outlet.isActive ? 'Deactivate' : 'Activate'}
          </button>
        </div>
      ),
    },
  ];

  return (
    <div>
      <div className="flex items-center justify-between mb-6">
        <h1 className="text-2xl font-bold text-gray-900">Outlet Management</h1>
        <button
          onClick={openCreateForm}
          className="rounded-md bg-blue-600 px-4 py-2 text-sm font-medium text-white hover:bg-blue-700 transition-colors"
        >
          Add Outlet
        </button>
      </div>

      {/* Last-outlet protection error */}
      {statusError && (
        <div
          className="mb-4 rounded-md bg-yellow-50 border border-yellow-200 p-4"
          role="alert"
        >
          <p className="text-sm text-yellow-800">{statusError}</p>
          <button
            onClick={() => setStatusError(null)}
            className="mt-2 text-xs text-yellow-600 hover:text-yellow-800 underline"
          >
            Dismiss
          </button>
        </div>
      )}

      <LoadingIndicator isLoading={isLoading} />

      {error && (
        <div className="rounded-md bg-red-50 border border-red-200 p-4" role="alert">
          <p className="text-sm text-red-800">{error}</p>
        </div>
      )}

      {!isLoading && !error && (
        <DataTable
          columns={columns}
          data={paginatedOutlets}
          getRowKey={(outlet) => outlet.outletId}
          currentPage={currentPage}
          pageSize={pageSize}
          totalItems={outlets.length}
          onPageChange={setCurrentPage}
          onPageSizeChange={(size) => {
            setPageSize(size);
            setCurrentPage(1);
          }}
          emptyMessage="No outlets found. Click 'Add Outlet' to create one."
        />
      )}

      {/* Create/Edit Form Modal */}
      {showForm && (
        <div className="fixed inset-0 z-50 flex items-center justify-center bg-black/50">
          <div className="w-full max-w-md rounded-lg bg-white p-6 shadow-xl">
            <h2 className="text-lg font-semibold text-gray-900 mb-4">
              {editingOutlet ? 'Edit Outlet' : 'Create Outlet'}
            </h2>
            <form onSubmit={handleSubmit} className="space-y-4">
              <div>
                <label htmlFor="outlet-name" className="block text-sm font-medium text-gray-700">
                  Name *
                </label>
                <input
                  id="outlet-name"
                  type="text"
                  value={formData.name}
                  onChange={(e) => setFormData({ ...formData, name: e.target.value })}
                  className={`mt-1 block w-full rounded-md border px-3 py-2 text-sm shadow-sm focus:outline-none focus:ring-1 ${
                    formErrors.name
                      ? 'border-red-300 focus:border-red-500 focus:ring-red-500'
                      : 'border-gray-300 focus:border-blue-500 focus:ring-blue-500'
                  }`}
                  placeholder="Enter outlet name"
                />
                {formErrors.name && (
                  <p className="mt-1 text-xs text-red-600">{formErrors.name}</p>
                )}
              </div>

              <div>
                <label htmlFor="outlet-address" className="block text-sm font-medium text-gray-700">
                  Address *
                </label>
                <input
                  id="outlet-address"
                  type="text"
                  value={formData.address}
                  onChange={(e) => setFormData({ ...formData, address: e.target.value })}
                  className={`mt-1 block w-full rounded-md border px-3 py-2 text-sm shadow-sm focus:outline-none focus:ring-1 ${
                    formErrors.address
                      ? 'border-red-300 focus:border-red-500 focus:ring-red-500'
                      : 'border-gray-300 focus:border-blue-500 focus:ring-blue-500'
                  }`}
                  placeholder="Enter outlet address"
                />
                {formErrors.address && (
                  <p className="mt-1 text-xs text-red-600">{formErrors.address}</p>
                )}
              </div>

              <div>
                <label htmlFor="outlet-phone" className="block text-sm font-medium text-gray-700">
                  Phone Number *
                </label>
                <input
                  id="outlet-phone"
                  type="text"
                  value={formData.phoneNumber}
                  onChange={(e) => setFormData({ ...formData, phoneNumber: e.target.value })}
                  className={`mt-1 block w-full rounded-md border px-3 py-2 text-sm shadow-sm focus:outline-none focus:ring-1 ${
                    formErrors.phoneNumber
                      ? 'border-red-300 focus:border-red-500 focus:ring-red-500'
                      : 'border-gray-300 focus:border-blue-500 focus:ring-blue-500'
                  }`}
                  placeholder="Enter phone number"
                />
                {formErrors.phoneNumber && (
                  <p className="mt-1 text-xs text-red-600">{formErrors.phoneNumber}</p>
                )}
              </div>

              <div>
                <label htmlFor="outlet-manager" className="block text-sm font-medium text-gray-700">
                  Assigned Manager ID
                </label>
                <input
                  id="outlet-manager"
                  type="text"
                  value={formData.assignedManagerId}
                  onChange={(e) => setFormData({ ...formData, assignedManagerId: e.target.value })}
                  className="mt-1 block w-full rounded-md border border-gray-300 px-3 py-2 text-sm shadow-sm focus:border-blue-500 focus:outline-none focus:ring-1 focus:ring-blue-500"
                  placeholder="Optional manager ID"
                />
              </div>

              <div className="flex justify-end gap-3 pt-4">
                <button
                  type="button"
                  onClick={closeForm}
                  className="rounded-md border border-gray-300 bg-white px-4 py-2 text-sm font-medium text-gray-700 hover:bg-gray-50"
                >
                  Cancel
                </button>
                <button
                  type="submit"
                  disabled={isSubmitting}
                  className="rounded-md bg-blue-600 px-4 py-2 text-sm font-medium text-white hover:bg-blue-700 disabled:opacity-50 disabled:cursor-not-allowed"
                >
                  {isSubmitting ? 'Saving...' : editingOutlet ? 'Update' : 'Create'}
                </button>
              </div>
            </form>
          </div>
        </div>
      )}
    </div>
  );
}
