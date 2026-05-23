import { useEffect, useState, useCallback } from 'react';
import { apiClient, ApiError } from '../utils/api';
import { useToast } from '../components/Toast';
import { LoadingIndicator } from '../components/LoadingIndicator';
import { DataTable, type Column } from '../components/DataTable';

interface User {
  userId: string;
  email: string;
  name: string;
  role: 'Admin' | 'Outlet_Manager';
  outletId?: string;
  isActive: boolean;
  createdAt: string;
  updatedAt: string;
}

interface Outlet {
  outletId: string;
  name: string;
  isActive: boolean;
}

interface UserFormData {
  email: string;
  name: string;
  password: string;
  role: 'Admin' | 'Outlet_Manager';
  outletId: string;
}

interface FormErrors {
  email?: string;
  name?: string;
  password?: string;
  role?: string;
  outletId?: string;
}

export function UsersPage() {
  const { showToast } = useToast();
  const [users, setUsers] = useState<User[]>([]);
  const [outlets, setOutlets] = useState<Outlet[]>([]);
  const [isLoading, setIsLoading] = useState(true);
  const [error, setError] = useState<string | null>(null);
  const [showForm, setShowForm] = useState(false);
  const [editingUser, setEditingUser] = useState<User | null>(null);
  const [formData, setFormData] = useState<UserFormData>({
    email: '',
    name: '',
    password: '',
    role: 'Admin',
    outletId: '',
  });
  const [formErrors, setFormErrors] = useState<FormErrors>({});
  const [isSubmitting, setIsSubmitting] = useState(false);

  // Pagination
  const [currentPage, setCurrentPage] = useState(1);
  const [pageSize, setPageSize] = useState(10);

  const fetchUsers = useCallback(async () => {
    try {
      setIsLoading(true);
      setError(null);
      const [usersResult, outletsResult] = await Promise.all([
        apiClient.get<{ users: User[] }>('/users'),
        apiClient.get<{ outlets: Outlet[] }>('/outlets'),
      ]);
      setUsers(usersResult.users);
      setOutlets(outletsResult.outlets);
    } catch (err) {
      if (err instanceof ApiError) {
        setError(err.message);
      } else {
        setError('Failed to load users');
      }
    } finally {
      setIsLoading(false);
    }
  }, []);

  useEffect(() => {
    fetchUsers();
  }, [fetchUsers]);

  function validateForm(): boolean {
    const errors: FormErrors = {};
    if (!editingUser && !formData.email.trim()) {
      errors.email = 'Email is required';
    } else if (!editingUser && !/^[^\s@]+@[^\s@]+\.[^\s@]+$/.test(formData.email.trim())) {
      errors.email = 'Invalid email format';
    }
    if (!formData.name.trim()) {
      errors.name = 'Name is required';
    }
    if (!editingUser && !formData.password.trim()) {
      errors.password = 'Password is required';
    } else if (!editingUser && formData.password.length < 8) {
      errors.password = 'Password must be at least 8 characters';
    }
    if (formData.role === 'Outlet_Manager' && !formData.outletId) {
      errors.outletId = 'Outlet selection is required for Outlet Manager role';
    }
    setFormErrors(errors);
    return Object.keys(errors).length === 0;
  }

  function openCreateForm() {
    setEditingUser(null);
    setFormData({ email: '', name: '', password: '', role: 'Admin', outletId: '' });
    setFormErrors({});
    setShowForm(true);
  }

  function openEditForm(user: User) {
    setEditingUser(user);
    setFormData({
      email: user.email,
      name: user.name,
      password: '',
      role: user.role,
      outletId: user.outletId || '',
    });
    setFormErrors({});
    setShowForm(true);
  }

  function closeForm() {
    setShowForm(false);
    setEditingUser(null);
    setFormErrors({});
  }

  async function handleSubmit(e: React.FormEvent) {
    e.preventDefault();
    if (!validateForm()) return;

    setIsSubmitting(true);
    try {
      if (editingUser) {
        const body: Record<string, unknown> = {
          name: formData.name.trim(),
          role: formData.role,
          outletId: formData.role === 'Outlet_Manager' ? formData.outletId : null,
        };
        if (formData.password.trim()) {
          body.password = formData.password;
        }
        await apiClient.put(`/users/${editingUser.userId}`, body);
        showToast('success', 'User updated successfully');
      } else {
        const body = {
          email: formData.email.trim(),
          name: formData.name.trim(),
          password: formData.password,
          role: formData.role,
          outletId: formData.role === 'Outlet_Manager' ? formData.outletId : undefined,
        };
        await apiClient.post('/users', body);
        showToast('success', 'User created successfully');
      }
      closeForm();
      await fetchUsers();
    } catch (err) {
      if (err instanceof ApiError) {
        showToast('error', (err.body as { message?: string })?.message || err.message);
      } else {
        showToast('error', 'Failed to save user');
      }
    } finally {
      setIsSubmitting(false);
    }
  }

  function getOutletName(outletId?: string): string {
    if (!outletId) return '—';
    const outlet = outlets.find((o) => o.outletId === outletId);
    return outlet ? outlet.name : outletId;
  }

  // Paginated data
  const paginatedUsers = users.slice(
    (currentPage - 1) * pageSize,
    currentPage * pageSize
  );

  const columns: Column<User>[] = [
    {
      key: 'name',
      header: 'Name',
      render: (user) => <span className="font-medium text-gray-900">{user.name}</span>,
    },
    {
      key: 'email',
      header: 'Email',
      render: (user) => user.email,
    },
    {
      key: 'role',
      header: 'Role',
      render: (user) => (
        <span
          className={`inline-flex items-center rounded-full px-2.5 py-0.5 text-xs font-medium ${
            user.role === 'Admin'
              ? 'bg-purple-100 text-purple-800'
              : 'bg-blue-100 text-blue-800'
          }`}
        >
          {user.role === 'Outlet_Manager' ? 'Outlet Manager' : user.role}
        </span>
      ),
    },
    {
      key: 'outlet',
      header: 'Assigned Outlet',
      render: (user) => getOutletName(user.outletId),
    },
    {
      key: 'status',
      header: 'Status',
      render: (user) => (
        <span
          className={`inline-flex items-center rounded-full px-2.5 py-0.5 text-xs font-medium ${
            user.isActive
              ? 'bg-green-100 text-green-800'
              : 'bg-red-100 text-red-800'
          }`}
        >
          {user.isActive ? 'Active' : 'Inactive'}
        </span>
      ),
    },
    {
      key: 'actions',
      header: 'Actions',
      render: (user) => (
        <button
          onClick={() => openEditForm(user)}
          className="text-sm text-blue-600 hover:text-blue-800 font-medium"
        >
          Edit
        </button>
      ),
    },
  ];

  return (
    <div>
      <div className="flex items-center justify-between mb-6">
        <h1 className="text-2xl font-bold text-gray-900">User Management</h1>
        <button
          onClick={openCreateForm}
          className="rounded-md bg-blue-600 px-4 py-2 text-sm font-medium text-white hover:bg-blue-700 transition-colors"
        >
          Add User
        </button>
      </div>

      <LoadingIndicator isLoading={isLoading} />

      {error && (
        <div className="rounded-md bg-red-50 border border-red-200 p-4" role="alert">
          <p className="text-sm text-red-800">{error}</p>
        </div>
      )}

      {!isLoading && !error && (
        <DataTable
          columns={columns}
          data={paginatedUsers}
          getRowKey={(user) => user.userId}
          currentPage={currentPage}
          pageSize={pageSize}
          totalItems={users.length}
          onPageChange={setCurrentPage}
          onPageSizeChange={(size) => {
            setPageSize(size);
            setCurrentPage(1);
          }}
          emptyMessage="No users found. Click 'Add User' to create one."
        />
      )}

      {/* Create/Edit Form Modal */}
      {showForm && (
        <div className="fixed inset-0 z-50 flex items-center justify-center bg-black/50">
          <div className="w-full max-w-md rounded-lg bg-white p-6 shadow-xl">
            <h2 className="text-lg font-semibold text-gray-900 mb-4">
              {editingUser ? 'Edit User' : 'Create User'}
            </h2>
            <form onSubmit={handleSubmit} className="space-y-4">
              {!editingUser && (
                <div>
                  <label htmlFor="user-email" className="block text-sm font-medium text-gray-700">
                    Email *
                  </label>
                  <input
                    id="user-email"
                    type="email"
                    value={formData.email}
                    onChange={(e) => setFormData({ ...formData, email: e.target.value })}
                    className={`mt-1 block w-full rounded-md border px-3 py-2 text-sm shadow-sm focus:outline-none focus:ring-1 ${
                      formErrors.email
                        ? 'border-red-300 focus:border-red-500 focus:ring-red-500'
                        : 'border-gray-300 focus:border-blue-500 focus:ring-blue-500'
                    }`}
                    placeholder="user@example.com"
                  />
                  {formErrors.email && (
                    <p className="mt-1 text-xs text-red-600">{formErrors.email}</p>
                  )}
                </div>
              )}

              <div>
                <label htmlFor="user-name" className="block text-sm font-medium text-gray-700">
                  Name *
                </label>
                <input
                  id="user-name"
                  type="text"
                  value={formData.name}
                  onChange={(e) => setFormData({ ...formData, name: e.target.value })}
                  className={`mt-1 block w-full rounded-md border px-3 py-2 text-sm shadow-sm focus:outline-none focus:ring-1 ${
                    formErrors.name
                      ? 'border-red-300 focus:border-red-500 focus:ring-red-500'
                      : 'border-gray-300 focus:border-blue-500 focus:ring-blue-500'
                  }`}
                  placeholder="Enter full name"
                />
                {formErrors.name && (
                  <p className="mt-1 text-xs text-red-600">{formErrors.name}</p>
                )}
              </div>

              <div>
                <label htmlFor="user-password" className="block text-sm font-medium text-gray-700">
                  Password {editingUser ? '(leave blank to keep current)' : '*'}
                </label>
                <input
                  id="user-password"
                  type="password"
                  value={formData.password}
                  onChange={(e) => setFormData({ ...formData, password: e.target.value })}
                  className={`mt-1 block w-full rounded-md border px-3 py-2 text-sm shadow-sm focus:outline-none focus:ring-1 ${
                    formErrors.password
                      ? 'border-red-300 focus:border-red-500 focus:ring-red-500'
                      : 'border-gray-300 focus:border-blue-500 focus:ring-blue-500'
                  }`}
                  placeholder={editingUser ? 'Leave blank to keep current' : 'Minimum 8 characters'}
                />
                {formErrors.password && (
                  <p className="mt-1 text-xs text-red-600">{formErrors.password}</p>
                )}
              </div>

              <div>
                <label htmlFor="user-role" className="block text-sm font-medium text-gray-700">
                  Role *
                </label>
                <select
                  id="user-role"
                  value={formData.role}
                  onChange={(e) =>
                    setFormData({
                      ...formData,
                      role: e.target.value as 'Admin' | 'Outlet_Manager',
                      outletId: e.target.value === 'Admin' ? '' : formData.outletId,
                    })
                  }
                  className="mt-1 block w-full rounded-md border border-gray-300 px-3 py-2 text-sm shadow-sm focus:border-blue-500 focus:outline-none focus:ring-1 focus:ring-blue-500"
                >
                  <option value="Admin">Admin</option>
                  <option value="Outlet_Manager">Outlet Manager</option>
                </select>
              </div>

              {formData.role === 'Outlet_Manager' && (
                <div>
                  <label htmlFor="user-outlet" className="block text-sm font-medium text-gray-700">
                    Assigned Outlet *
                  </label>
                  <select
                    id="user-outlet"
                    value={formData.outletId}
                    onChange={(e) => setFormData({ ...formData, outletId: e.target.value })}
                    className={`mt-1 block w-full rounded-md border px-3 py-2 text-sm shadow-sm focus:outline-none focus:ring-1 ${
                      formErrors.outletId
                        ? 'border-red-300 focus:border-red-500 focus:ring-red-500'
                        : 'border-gray-300 focus:border-blue-500 focus:ring-blue-500'
                    }`}
                  >
                    <option value="">Select an outlet</option>
                    {outlets
                      .filter((o) => o.isActive)
                      .map((outlet) => (
                        <option key={outlet.outletId} value={outlet.outletId}>
                          {outlet.name}
                        </option>
                      ))}
                  </select>
                  {formErrors.outletId && (
                    <p className="mt-1 text-xs text-red-600">{formErrors.outletId}</p>
                  )}
                </div>
              )}

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
                  {isSubmitting ? 'Saving...' : editingUser ? 'Update' : 'Create'}
                </button>
              </div>
            </form>
          </div>
        </div>
      )}
    </div>
  );
}
