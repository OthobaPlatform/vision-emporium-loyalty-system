import { render, screen, waitFor } from '@testing-library/react';
import userEvent from '@testing-library/user-event';
import { MemoryRouter } from 'react-router-dom';
import { describe, it, expect, vi, beforeEach } from 'vitest';
import { RedemptionsPage } from './RedemptionsPage';
import { ToastProvider } from '../components/Toast';

// Mock the api module
vi.mock('../utils/api', () => ({
  apiClient: {
    post: vi.fn(),
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

const mockedPost = vi.mocked(apiClient.post);
const mockedGet = vi.mocked(apiClient.get);

function renderPage() {
  return render(
    <MemoryRouter>
      <ToastProvider>
        <RedemptionsPage />
      </ToastProvider>
    </MemoryRouter>
  );
}

function getVerifyCodeInput() {
  return document.getElementById('verify-code') as HTMLInputElement;
}

function getSearchPhoneInput() {
  return document.getElementById('search-phone') as HTMLInputElement;
}

function getSearchCodeInput() {
  return document.getElementById('search-code') as HTMLInputElement;
}

describe('RedemptionsPage', () => {
  beforeEach(() => {
    vi.clearAllMocks();
  });

  describe('Verification Form', () => {
    it('renders the verification form', () => {
      renderPage();
      expect(screen.getByText('Verify Redemption Code')).toBeInTheDocument();
      expect(getVerifyCodeInput()).toBeInTheDocument();
      expect(screen.getByRole('button', { name: /verify & redeem/i })).toBeInTheDocument();
    });

    it('shows error when submitting empty code', async () => {
      const user = userEvent.setup();
      renderPage();

      await user.click(screen.getByRole('button', { name: /verify & redeem/i }));

      expect(screen.getByText('Verification code is required')).toBeInTheDocument();
    });

    it('shows error for non-6-digit code', async () => {
      const user = userEvent.setup();
      renderPage();

      await user.type(getVerifyCodeInput(), '123');
      await user.click(screen.getByRole('button', { name: /verify & redeem/i }));

      expect(screen.getByText('Code must be exactly 6 digits')).toBeInTheDocument();
    });

    it('only allows numeric input in code field', async () => {
      const user = userEvent.setup();
      renderPage();

      const input = getVerifyCodeInput();
      await user.type(input, 'abc123def456');

      expect(input).toHaveValue('123456');
    });

    it('displays success result on valid redemption', async () => {
      const user = userEvent.setup();
      mockedPost.mockResolvedValueOnce({
        message: 'Gift redeemed successfully',
        redemption: {
          code: '123456',
          giftType: 'Cash_Return',
          giftDescription: '500 BDT Cash Back',
          redeemedAt: '2024-01-15T10:30:00Z',
        },
      });

      renderPage();

      await user.type(getVerifyCodeInput(), '123456');
      await user.click(screen.getByRole('button', { name: /verify & redeem/i }));

      await waitFor(() => {
        expect(screen.getByRole('status')).toBeInTheDocument();
      });
      expect(screen.getByText('Cash_Return')).toBeInTheDocument();
      expect(screen.getByText('500 BDT Cash Back')).toBeInTheDocument();
    });

    it('displays error message for 404 response', async () => {
      const user = userEvent.setup();
      mockedPost.mockRejectedValueOnce(
        new ApiError(404, 'Not Found', { error: 'NotFound', message: 'Code not found' })
      );

      renderPage();

      await user.type(getVerifyCodeInput(), '999999');
      await user.click(screen.getByRole('button', { name: /verify & redeem/i }));

      await waitFor(() => {
        expect(screen.getByText('Code not found')).toBeInTheDocument();
      });
    });

    it('displays rate limit error for 429 response', async () => {
      const user = userEvent.setup();
      mockedPost.mockRejectedValueOnce(
        new ApiError(429, 'Too Many Requests', {
          error: 'TooManyRequests',
          message: 'Too many failed attempts. Please wait 30 minutes.',
        })
      );

      renderPage();

      await user.type(getVerifyCodeInput(), '111111');
      await user.click(screen.getByRole('button', { name: /verify & redeem/i }));

      await waitFor(() => {
        expect(
          screen.getByText('Too many failed attempts. Please wait 30 minutes.')
        ).toBeInTheDocument();
      });
    });

    it('clears error when user starts typing', async () => {
      const user = userEvent.setup();
      renderPage();

      await user.click(screen.getByRole('button', { name: /verify & redeem/i }));
      expect(screen.getByText('Verification code is required')).toBeInTheDocument();

      await user.type(getVerifyCodeInput(), '1');
      expect(screen.queryByText('Verification code is required')).not.toBeInTheDocument();
    });
  });

  describe('Search Form', () => {
    it('renders the search form', () => {
      renderPage();
      expect(screen.getByText('Search Redemptions')).toBeInTheDocument();
      expect(getSearchPhoneInput()).toBeInTheDocument();
      expect(getSearchCodeInput()).toBeInTheDocument();
    });

    it('shows error when both fields are empty', async () => {
      const user = userEvent.setup();
      renderPage();

      await user.click(screen.getByRole('button', { name: /^search$/i }));

      expect(
        screen.getByText('Enter a phone number or verification code to search')
      ).toBeInTheDocument();
    });

    it('shows error for invalid phone format', async () => {
      const user = userEvent.setup();
      renderPage();

      await user.type(getSearchPhoneInput(), '01712345678');
      await user.click(screen.getByRole('button', { name: /^search$/i }));

      expect(
        screen.getByText(
          'Phone number must be in E.164 format with +880 prefix (e.g., +8801712345678)'
        )
      ).toBeInTheDocument();
    });

    it('displays search results on success', async () => {
      const user = userEvent.setup();
      mockedGet.mockResolvedValueOnce({
        results: [
          {
            code: '123456',
            customerName: 'John Doe',
            phoneNumber: '+8801712345678',
            giftType: 'Cash_Return',
            giftDescription: '500 BDT',
            status: 'Active',
            designatedOutlet: 'Outlet Dhaka',
            issuedAt: '2024-01-10T00:00:00Z',
          },
        ],
      });

      renderPage();

      await user.type(getSearchPhoneInput(), '+8801712345678');
      await user.click(screen.getByRole('button', { name: /^search$/i }));

      await waitFor(() => {
        expect(screen.getByText('John Doe')).toBeInTheDocument();
      });
      expect(screen.getByText('Outlet Dhaka')).toBeInTheDocument();
    });

    it('shows no results message when search returns empty', async () => {
      const user = userEvent.setup();
      mockedGet.mockResolvedValueOnce({ results: [] });

      renderPage();

      await user.type(getSearchPhoneInput(), '+8801712345678');
      await user.click(screen.getByRole('button', { name: /^search$/i }));

      await waitFor(() => {
        expect(screen.getByText('No redemption records found.')).toBeInTheDocument();
      });
    });
  });
});
