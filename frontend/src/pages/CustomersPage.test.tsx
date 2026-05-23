import { render, screen, waitFor } from '@testing-library/react';
import userEvent from '@testing-library/user-event';
import { MemoryRouter } from 'react-router-dom';
import { describe, it, expect, vi, beforeEach } from 'vitest';
import { CustomersPage } from './CustomersPage';
import { ToastProvider } from '../components/Toast';

// Mock the api module
vi.mock('../utils/api', () => ({
  apiClient: {
    get: vi.fn(),
  },
  ApiError: class ApiError extends Error {
    status: number;
    statusText: string;
    body?: unknown;
    constructor(status: number, statusText: string, body?: unknown) {
      super(`API Error: ${status} ${statusText}`);
      this.name = 'ApiError';
      this.status = status;
      this.statusText = statusText;
      this.body = body;
    }
  },
}));

import { apiClient, ApiError } from '../utils/api';

const mockedGet = vi.mocked(apiClient.get);

function renderPage() {
  return render(
    <MemoryRouter>
      <ToastProvider>
        <CustomersPage />
      </ToastProvider>
    </MemoryRouter>
  );
}

describe('CustomersPage', () => {
  beforeEach(() => {
    vi.clearAllMocks();
  });

  describe('Search Form', () => {
    it('renders the search form', () => {
      renderPage();
      expect(screen.getByText('Customer Lookup')).toBeInTheDocument();
      expect(screen.getByLabelText('Phone Number')).toBeInTheDocument();
      expect(screen.getByRole('button', { name: /look up customer/i })).toBeInTheDocument();
    });

    it('shows error when submitting empty phone', async () => {
      const user = userEvent.setup();
      renderPage();

      await user.click(screen.getByRole('button', { name: /look up customer/i }));

      expect(screen.getByText('Phone number is required')).toBeInTheDocument();
    });

    it('shows error for invalid phone format', async () => {
      const user = userEvent.setup();
      renderPage();

      await user.type(screen.getByLabelText('Phone Number'), '01712345678');
      await user.click(screen.getByRole('button', { name: /look up customer/i }));

      expect(
        screen.getByText(
          'Phone number must be in E.164 format with +880 prefix (e.g., +8801712345678)'
        )
      ).toBeInTheDocument();
    });

    it('clears error when user starts typing', async () => {
      const user = userEvent.setup();
      renderPage();

      await user.click(screen.getByRole('button', { name: /look up customer/i }));
      expect(screen.getByText('Phone number is required')).toBeInTheDocument();

      await user.type(screen.getByLabelText('Phone Number'), '+');
      expect(screen.queryByText('Phone number is required')).not.toBeInTheDocument();
    });
  });

  describe('Customer Profile Display', () => {
    it('displays customer profile on successful lookup', async () => {
      const user = userEvent.setup();
      mockedGet.mockResolvedValueOnce({
        customerId: 'CUST001',
        name: 'Rahim Ahmed',
        phoneNumber: '+8801712345678',
        qualifyingPurchases: 4,
        progress: { current: 1, target: 6, nextThreshold: 6 },
        codes: [],
      });

      renderPage();

      await user.type(screen.getByLabelText('Phone Number'), '+8801712345678');
      await user.click(screen.getByRole('button', { name: /look up customer/i }));

      await waitFor(() => {
        expect(screen.getByText('Rahim Ahmed')).toBeInTheDocument();
      });
      expect(screen.getByText('+8801712345678')).toBeInTheDocument();
      expect(screen.getByText('4')).toBeInTheDocument();
    });

    it('displays progress toward next threshold', async () => {
      const user = userEvent.setup();
      mockedGet.mockResolvedValueOnce({
        customerId: 'CUST001',
        name: 'Rahim Ahmed',
        phoneNumber: '+8801712345678',
        qualifyingPurchases: 2,
        progress: { current: 2, target: 3, nextThreshold: 3 },
        codes: [],
      });

      renderPage();

      await user.type(screen.getByLabelText('Phone Number'), '+8801712345678');
      await user.click(screen.getByRole('button', { name: /look up customer/i }));

      await waitFor(() => {
        expect(screen.getByRole('progressbar')).toBeInTheDocument();
      });
      expect(screen.getByRole('progressbar')).toHaveAttribute('aria-valuenow', '2');
      expect(screen.getByRole('progressbar')).toHaveAttribute('aria-valuemax', '3');
      expect(screen.getByRole('progressbar')).toHaveAttribute('aria-label', '2 of 3 purchases');
    });

    it('displays completion status when all thresholds achieved', async () => {
      const user = userEvent.setup();
      mockedGet.mockResolvedValueOnce({
        customerId: 'CUST001',
        name: 'Rahim Ahmed',
        phoneNumber: '+8801712345678',
        qualifyingPurchases: 6,
        progress: { current: 6, target: 6, nextThreshold: null },
        codes: [],
      });

      renderPage();

      await user.type(screen.getByLabelText('Phone Number'), '+8801712345678');
      await user.click(screen.getByRole('button', { name: /look up customer/i }));

      await waitFor(() => {
        expect(
          screen.getByText(/all reward tiers achieved/i)
        ).toBeInTheDocument();
      });
    });

    it('displays verification codes table', async () => {
      const user = userEvent.setup();
      mockedGet.mockResolvedValueOnce({
        customerId: 'CUST001',
        name: 'Rahim Ahmed',
        phoneNumber: '+8801712345678',
        qualifyingPurchases: 3,
        progress: { current: 0, target: 6, nextThreshold: 6 },
        codes: [
          {
            code: '123456',
            tier: 1,
            giftType: 'Cash_Return',
            giftDescription: '500 BDT Cash Back',
            status: 'Active',
            designatedOutlet: 'Outlet Dhaka',
            issuedAt: '2024-01-10T00:00:00Z',
            expiresAt: '2024-02-09T00:00:00Z',
          },
        ],
      });

      renderPage();

      await user.type(screen.getByLabelText('Phone Number'), '+8801712345678');
      await user.click(screen.getByRole('button', { name: /look up customer/i }));

      await waitFor(() => {
        expect(screen.getByText('123456')).toBeInTheDocument();
      });
      expect(screen.getByText('Cash_Return')).toBeInTheDocument();
      expect(screen.getByText('Outlet Dhaka')).toBeInTheDocument();
    });

    it('shows not found message for 404 response', async () => {
      const user = userEvent.setup();
      mockedGet.mockRejectedValueOnce(
        new ApiError(404, 'Not Found', { error: 'NotFound', message: 'Customer not found' })
      );

      renderPage();

      await user.type(screen.getByLabelText('Phone Number'), '+8801799999999');
      await user.click(screen.getByRole('button', { name: /look up customer/i }));

      await waitFor(() => {
        expect(
          screen.getByText(/no customer found with this phone number/i)
        ).toBeInTheDocument();
      });
    });

    it('shows no codes message when customer has no verification codes', async () => {
      const user = userEvent.setup();
      mockedGet.mockResolvedValueOnce({
        customerId: 'CUST001',
        name: 'Rahim Ahmed',
        phoneNumber: '+8801712345678',
        qualifyingPurchases: 1,
        progress: { current: 1, target: 3, nextThreshold: 3 },
        codes: [],
      });

      renderPage();

      await user.type(screen.getByLabelText('Phone Number'), '+8801712345678');
      await user.click(screen.getByRole('button', { name: /look up customer/i }));

      await waitFor(() => {
        expect(
          screen.getByText(/no verification codes issued/i)
        ).toBeInTheDocument();
      });
    });
  });
});
