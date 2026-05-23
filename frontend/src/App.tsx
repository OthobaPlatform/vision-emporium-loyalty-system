import { BrowserRouter, Routes, Route, Navigate } from 'react-router-dom';
import { AuthProvider } from './contexts/AuthContext';
import { ToastProvider } from './components/Toast';
import { ProtectedRoute } from './components/ProtectedRoute';
import { Layout } from './components/Layout';
import { LoginPage } from './pages/LoginPage';
import { DashboardPage } from './pages/DashboardPage';
import { RedemptionsPage } from './pages/RedemptionsPage';
import { ConfigurationPage } from './pages/ConfigurationPage';
import { CustomersPage } from './pages/CustomersPage';
import { OutletsPage } from './pages/OutletsPage';
import { UsersPage } from './pages/UsersPage';
import { ImportPage } from './pages/ImportPage';
import { SyncStatusPage } from './pages/SyncStatusPage';

function App() {
  return (
    <BrowserRouter>
      <AuthProvider>
        <ToastProvider>
          <Routes>
            {/* Public routes */}
            <Route path="/login" element={<LoginPage />} />

            {/* Authenticated routes wrapped in Layout */}
            <Route
              element={
                <ProtectedRoute>
                  <Layout />
                </ProtectedRoute>
              }
            >
              {/* Admin routes */}
              <Route
                path="/dashboard"
                element={
                  <ProtectedRoute allowedRoles={['Admin']}>
                    <DashboardPage />
                  </ProtectedRoute>
                }
              />

              {/* Shared routes (Admin + Outlet_Manager) */}
              <Route
                path="/redemptions"
                element={
                  <ProtectedRoute allowedRoles={['Admin', 'Outlet_Manager']}>
                    <RedemptionsPage />
                  </ProtectedRoute>
                }
              />

              <Route
                path="/customers"
                element={
                  <ProtectedRoute allowedRoles={['Admin', 'Outlet_Manager']}>
                    <CustomersPage />
                  </ProtectedRoute>
                }
              />

              {/* Admin-only routes */}
              <Route
                path="/configuration"
                element={
                  <ProtectedRoute allowedRoles={['Admin']}>
                    <ConfigurationPage />
                  </ProtectedRoute>
                }
              />

              <Route
                path="/outlets"
                element={
                  <ProtectedRoute allowedRoles={['Admin']}>
                    <OutletsPage />
                  </ProtectedRoute>
                }
              />

              <Route
                path="/users"
                element={
                  <ProtectedRoute allowedRoles={['Admin']}>
                    <UsersPage />
                  </ProtectedRoute>
                }
              />

              <Route
                path="/import"
                element={
                  <ProtectedRoute allowedRoles={['Admin']}>
                    <ImportPage />
                  </ProtectedRoute>
                }
              />

              <Route
                path="/sync"
                element={
                  <ProtectedRoute allowedRoles={['Admin']}>
                    <SyncStatusPage />
                  </ProtectedRoute>
                }
              />
            </Route>

            {/* Default redirect */}
            <Route path="/" element={<Navigate to="/login" replace />} />

            {/* Catch-all: redirect to login */}
            <Route path="*" element={<Navigate to="/login" replace />} />
          </Routes>
        </ToastProvider>
      </AuthProvider>
    </BrowserRouter>
  );
}

export default App;
