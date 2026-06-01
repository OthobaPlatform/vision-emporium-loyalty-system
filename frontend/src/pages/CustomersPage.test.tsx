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

import { apiClient } from '../utils/api';

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

const mockCustomers = [
  {
    customerId: '+8801712345678',
    name: 'Rahim Ahmed',
    phoneNumber: '+8801712345678',
    qualifyingPurchases: 4,
  },
  {
    customerId: '+8801799999999',
    name: 'Karim Hossain',
    phoneNumber: '+8801799999999',
    qualifyingPurchases: 2,
  },
  {
    customerId: '+8801973776409',
    name: '',
    phoneNumber: '+8801973776409',
    qualifyingPurchases: 0,
  },
];

describe('CustomersPage', () => {
  beforeEach(() => {
    vi.clearAllMocks();
  });

  describe('Customer List', () => {
    it('renders the page title and search box', async () => {
      mockedGet.mockResolvedValueOnce(mockCustomers);
      renderPage();

      expect(screen.getByText('Customers')).toBeInTheDocument();
      expect(screen.getByLabelText('Search customers by phone number')).toBeInTheDocument();
    });

    it('loads and displays customers in a table', async () => {
      mockedGet.mockResolvedValueOnce(mockCustomers);
      renderPage();

      await waitFor(() => {
        expect(screen.getByText('Rahim Ahmed')).toBeInTheDocument();
      });
      expect(screen.getByText('+8801712345678')).toBeInTheDocument();
      expect(screen.getByText('Karim Hossain')).toBeInTheDocument();
      expect(screen.getByText('+8801799999999')).toBeInTheDocument();
    });

    it('shows phone number as name when name is empty', async () => {
      mockedGet.mockResolvedValueOnce(mockCustomers);
      renderPage();

      await waitFor(() => {
        // The customer with empty name should show phone as the clickable link
        const links = screen.getAllByRole('button');
        const phoneAsName = links.find((btn) => btn.textContent === '+8801973776409');
        expect(phoneAsName).toBeInTheDocument();
      });
    });

    it('filters customers by phone number search', async () => {
      const user = userEvent.setup();
      mockedGet.mockResolvedValueOnce(mockCustomers);
      renderPage();

      await waitFor(() => {
        expect(screen.getByText('Rahim Ahmed')).toBeInTheDocument();
      });

      await user.type(
        screen.getByLabelText('Search customers by phone number'),
        '9999'
      );

      // Only Karim should be visible
      expect(screen.queryByText('Rahim Ahmed')).not.toBeInTheDocument();
      expect(screen.getByText('Karim Hossain')).toBeInTheDocument();
    });

    it('shows empty message when no customers match search', async () => {
      const user = userEvent.setup();
      mockedGet.mockResolvedValueOnce(mockCustomers);
      renderPage();

      await waitFor(() => {
        expect(screen.getByText('Rahim Ahmed')).toBeInTheDocument();
      });

      await user.type(
        screen.getByLabelText('Search customers by phone number'),
        'nonexistent'
      );

      expect(screen.getByText('No customers match your search.')).toBeInTheDocument();
    });
  });

  describe('Customer Detail Panel', () => {
    it('opens detail panel when customer name is clicked', async () => {
      const user = userEvent.setup();
      mockedGet.mockResolvedValueOnce(mockCustomers);
      renderPage();

      await waitFor(() => {
        expect(screen.getByText('Rahim Ahmed')).toBeInTheDocument();
      });

      // Mock the profile and codes API calls
      mockedGet.mockResolvedValueOnce({
        customerId: '+8801712345678',
        name: 'Rahim Ahmed',
        phoneNumber: '+8801712345678',
        qualifyingPurchases: 4,
        currentCycleId: 'cycle-1',
        progress: {
          currentPurchases: 4,
          nextThreshold: 6,
          nextThresholdTier: 2,
          isComplete: false,
          description: '4 of 6 purchases',
        },
      });
      mockedGet.mockResolvedValueOnce({
        customerId: '+8801712345678',
        name: 'Rahim Ahmed',
        phoneNumber: '+8801712345678',
        codes: [],
      });

      await user.click(screen.getByText('Rahim Ahmed'));

      await waitFor(() => {
        expect(screen.getByRole('dialog')).toBeInTheDocument();
      });
      expect(screen.getByText('Customer Details')).toBeInTheDocument();
      expect(screen.getByRole('progressbar')).toBeInTheDocument();
    });

    it('shows progress bar with brand red color', async () => {
      const user = userEvent.setup();
      mockedGet.mockResolvedValueOnce(mockCustomers);
      renderPage();

      await waitFor(() => {
        expect(screen.getByText('Rahim Ahmed')).toBeInTheDocument();
      });

      mockedGet.mockResolvedValueOnce({
        customerId: '+8801712345678',
        name: 'Rahim Ahmed',
        phoneNumber: '+8801712345678',
        qualifyingPurchases: 4,
        currentCycleId: 'cycle-1',
        progress: {
          currentPurchases: 4,
          nextThreshold: 6,
          nextThresholdTier: 2,
          isComplete: false,
          description: '4 of 6 purchases',
        },
      });
      mockedGet.mockResolvedValueOnce({
        customerId: '+8801712345678',
        name: 'Rahim Ahmed',
        phoneNumber: '+8801712345678',
        codes: [],
      });

      await user.click(screen.getByText('Rahim Ahmed'));

      await waitFor(() => {
        const progressBar = screen.getByRole('progressbar');
        expect(progressBar).toHaveStyle({ backgroundColor: '#E31837' });
      });
    });

    it('shows verification codes in detail panel', async () => {
      const user = userEvent.setup();
      mockedGet.mockResolvedValueOnce(mockCustomers);
      renderPage();

      await waitFor(() => {
        expect(screen.getByText('Rahim Ahmed')).toBeInTheDocument();
      });

      mockedGet.mockResolvedValueOnce({
        customerId: '+8801712345678',
        name: 'Rahim Ahmed',
        phoneNumber: '+8801712345678',
        qualifyingPurchases: 4,
        currentCycleId: 'cycle-1',
        progress: {
          currentPurchases: 4,
          nextThreshold: 6,
          nextThresholdTier: 2,
          isComplete: false,
          description: '4 of 6 purchases',
        },
      });
      mockedGet.mockResolvedValueOnce({
        customerId: '+8801712345678',
        name: 'Rahim Ahmed',
        phoneNumber: '+8801712345678',
        codes: [
          {
            code: 'ABC123',
            status: 'Active',
            giftTier: 1,
            giftType: 'Cash_Return',
            giftDescription: '500 BDT Cash Back',
            giftValue: 500,
            designatedOutlet: 'Outlet Dhaka',
            issuedAt: '2024-01-10T00:00:00Z',
          },
        ],
      });

      await user.click(screen.getByText('Rahim Ahmed'));

      await waitFor(() => {
        expect(screen.getByText('ABC123')).toBeInTheDocument();
      });
      expect(screen.getByText('Cash_Return')).toBeInTheDocument();
      expect(screen.getByText('Outlet Dhaka')).toBeInTheDocument();
    });

    it('closes detail panel when close button is clicked', async () => {
      const user = userEvent.setup();
      mockedGet.mockResolvedValueOnce(mockCustomers);
      renderPage();

      await waitFor(() => {
        expect(screen.getByText('Rahim Ahmed')).toBeInTheDocument();
      });

      mockedGet.mockResolvedValueOnce({
        customerId: '+8801712345678',
        name: 'Rahim Ahmed',
        phoneNumber: '+8801712345678',
        qualifyingPurchases: 4,
        currentCycleId: 'cycle-1',
        progress: {
          currentPurchases: 4,
          nextThreshold: 6,
          nextThresholdTier: 2,
          isComplete: false,
          description: '4 of 6 purchases',
        },
      });
      mockedGet.mockResolvedValueOnce({
        customerId: '+8801712345678',
        name: 'Rahim Ahmed',
        phoneNumber: '+8801712345678',
        codes: [],
      });

      await user.click(screen.getByText('Rahim Ahmed'));

      await waitFor(() => {
        expect(screen.getByRole('dialog')).toBeInTheDocument();
      });

      await user.click(screen.getByLabelText('Close detail panel'));

      await waitFor(() => {
        expect(screen.queryByRole('dialog')).not.toBeInTheDocument();
      });
    });

    it('shows all tiers achieved message when complete', async () => {
      const user = userEvent.setup();
      mockedGet.mockResolvedValueOnce(mockCustomers);
      renderPage();

      await waitFor(() => {
        expect(screen.getByText('Rahim Ahmed')).toBeInTheDocument();
      });

      mockedGet.mockResolvedValueOnce({
        customerId: '+8801712345678',
        name: 'Rahim Ahmed',
        phoneNumber: '+8801712345678',
        qualifyingPurchases: 10,
        currentCycleId: 'cycle-1',
        progress: {
          currentPurchases: 10,
          nextThreshold: null,
          nextThresholdTier: null,
          isComplete: true,
          description: 'All reward tiers achieved',
        },
      });
      mockedGet.mockResolvedValueOnce({
        customerId: '+8801712345678',
        name: 'Rahim Ahmed',
        phoneNumber: '+8801712345678',
        codes: [],
      });

      await user.click(screen.getByText('Rahim Ahmed'));

      await waitFor(() => {
        expect(screen.getByText(/all reward tiers achieved/i)).toBeInTheDocument();
      });
    });
  });
});
