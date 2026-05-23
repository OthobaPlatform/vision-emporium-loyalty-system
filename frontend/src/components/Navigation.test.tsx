import { render, screen } from '@testing-library/react';
import { MemoryRouter } from 'react-router-dom';
import { describe, it, expect } from 'vitest';
import { Navigation, getNavItemsForRole } from './Navigation';

describe('getNavItemsForRole', () => {
  it('returns all 8 menu items for Admin role', () => {
    const items = getNavItemsForRole('Admin');
    expect(items).toHaveLength(8);
    expect(items.map((i) => i.label)).toEqual([
      'Dashboard',
      'Customers',
      'Redemptions',
      'Configuration',
      'Outlets',
      'Users',
      'Import',
      'Sync Status',
    ]);
  });

  it('returns correct paths for Admin role', () => {
    const items = getNavItemsForRole('Admin');
    expect(items.map((i) => i.path)).toEqual([
      '/dashboard',
      '/customers',
      '/redemptions',
      '/configuration',
      '/outlets',
      '/users',
      '/import',
      '/sync',
    ]);
  });

  it('returns 2 menu items for Outlet_Manager role', () => {
    const items = getNavItemsForRole('Outlet_Manager');
    expect(items).toHaveLength(2);
    expect(items.map((i) => i.label)).toEqual(['Redemptions', 'Customers']);
  });

  it('returns correct paths for Outlet_Manager role', () => {
    const items = getNavItemsForRole('Outlet_Manager');
    expect(items.map((i) => i.path)).toEqual(['/redemptions', '/customers']);
  });
});

describe('Navigation', () => {
  it('renders navigation links for given items', () => {
    const items = getNavItemsForRole('Admin');
    render(
      <MemoryRouter>
        <Navigation items={items} />
      </MemoryRouter>
    );

    expect(screen.getByText('Dashboard')).toBeInTheDocument();
    expect(screen.getByText('Customers')).toBeInTheDocument();
    expect(screen.getByText('Redemptions')).toBeInTheDocument();
    expect(screen.getByText('Configuration')).toBeInTheDocument();
    expect(screen.getByText('Outlets')).toBeInTheDocument();
    expect(screen.getByText('Users')).toBeInTheDocument();
  });

  it('renders navigation with correct aria label', () => {
    const items = getNavItemsForRole('Admin');
    render(
      <MemoryRouter>
        <Navigation items={items} />
      </MemoryRouter>
    );

    expect(screen.getByRole('navigation', { name: 'Main navigation' })).toBeInTheDocument();
  });

  it('hides labels when collapsed', () => {
    const items = getNavItemsForRole('Admin');
    render(
      <MemoryRouter>
        <Navigation items={items} collapsed={true} />
      </MemoryRouter>
    );

    expect(screen.queryByText('Dashboard')).not.toBeInTheDocument();
  });
});
