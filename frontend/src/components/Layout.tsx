import { useState } from 'react';
import { Outlet } from 'react-router-dom';
import { useAuth } from '../contexts/AuthContext';
import { Navigation, getNavItemsForRole } from './Navigation';
import { VisionEmporiumIcon } from './VisionEmporiumLogo';

/**
 * Main layout component for authenticated pages.
 * Provides a responsive sidebar navigation and header with Vision Emporium branding.
 * - Desktop (≥1024px): Fixed sidebar with content area
 * - Tablet (768–1023px): Collapsible sidebar with hamburger toggle
 */
export function Layout() {
  const { user, logout } = useAuth();
  const [sidebarOpen, setSidebarOpen] = useState(false);

  if (!user) return null;

  const navItems = getNavItemsForRole(user.role);

  return (
    <div className="flex h-screen bg-[#f0f4f8]">
      {/* Mobile/Tablet overlay */}
      {sidebarOpen && (
        <div
          className="fixed inset-0 z-30 bg-black/50 lg:hidden"
          onClick={() => setSidebarOpen(false)}
          aria-hidden="true"
        />
      )}

      {/* Sidebar */}
      <aside
        className={`fixed inset-y-0 left-0 z-40 flex w-64 flex-col border-r border-gray-200 bg-white transition-transform duration-200 lg:static lg:translate-x-0 ${
          sidebarOpen ? 'translate-x-0' : '-translate-x-full'
        }`}
        aria-label="Sidebar"
      >
        {/* Brand header with red accent */}
        <div className="flex h-16 items-center gap-3 border-b border-gray-200 px-4" style={{ background: 'linear-gradient(to right, var(--brand-primary, #E31E24), var(--brand-secondary, #1a1a1a))' }}>
          <VisionEmporiumIcon className="flex-shrink-0" />
          <div className="flex flex-col">
            <span className="text-sm font-bold text-white leading-tight">
              Vision Emporium
            </span>
            <span className="text-[10px] text-white/80 font-medium">
              Loyalty System
            </span>
          </div>
        </div>

        {/* Navigation */}
        <div className="flex-1 overflow-y-auto px-3 py-4">
          <Navigation items={navItems} />
        </div>

        {/* User info at bottom */}
        <div className="border-t border-gray-200 p-4">
          <div className="flex items-center gap-3">
            <div className="flex h-8 w-8 items-center justify-center rounded-full text-xs font-bold" style={{ backgroundColor: 'color-mix(in srgb, var(--brand-primary, #E31E24) 10%, transparent)', color: 'var(--brand-primary, #E31E24)' }}>
              {user.sub.charAt(0).toUpperCase()}
            </div>
            <div className="flex-1 min-w-0">
              <p className="truncate text-sm font-medium text-gray-900">
                {user.sub}
              </p>
              <p className="text-xs text-gray-500">
                {user.role === 'Outlet_Manager' ? 'Outlet Manager' : user.role}
              </p>
            </div>
          </div>
        </div>
      </aside>

      {/* Main content area */}
      <div className="flex flex-1 flex-col overflow-hidden">
        {/* Top header */}
        <header className="flex h-16 items-center justify-between border-b border-gray-200 bg-white px-4 lg:px-6 shadow-sm">
          {/* Hamburger for tablet/mobile */}
          <button
            onClick={() => setSidebarOpen(!sidebarOpen)}
            className="rounded-md p-2 text-gray-600 hover:bg-gray-100 lg:hidden"
            aria-label={sidebarOpen ? 'Close menu' : 'Open menu'}
          >
            <svg className="h-6 w-6" fill="none" viewBox="0 0 24 24" stroke="currentColor" strokeWidth={2}>
              {sidebarOpen ? (
                <path strokeLinecap="round" strokeLinejoin="round" d="M6 18L18 6M6 6l12 12" />
              ) : (
                <path strokeLinecap="round" strokeLinejoin="round" d="M4 6h16M4 12h16M4 18h16" />
              )}
            </svg>
          </button>

          {/* Spacer for desktop (no hamburger) */}
          <div className="hidden lg:block" />

          {/* Right side: user actions */}
          <div className="flex items-center gap-4">
            <span className="hidden text-sm text-gray-600 sm:inline">
              {user.role === 'Outlet_Manager' ? 'Outlet Manager' : user.role}
            </span>
            <button
              onClick={logout}
              className="rounded-md border border-gray-200 bg-white px-3 py-1.5 text-sm font-medium text-gray-700 hover:bg-gray-50 transition-colors"
              style={{ '--hover-border': 'var(--brand-primary, #E31E24)' } as React.CSSProperties}
            >
              Sign out
            </button>
          </div>
        </header>

        {/* Page content */}
        <main className="flex-1 overflow-y-auto p-4 lg:p-6">
          <Outlet />
        </main>
      </div>
    </div>
  );
}
