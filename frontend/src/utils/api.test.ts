import { describe, it, expect, beforeEach, vi } from 'vitest';
import { apiClient, ApiError } from './api';
import { setToken, getToken } from './auth';

// Mock fetch globally
const mockFetch = vi.fn();
globalThis.fetch = mockFetch;

describe('apiClient', () => {
  beforeEach(() => {
    localStorage.clear();
    mockFetch.mockReset();
    // Mock window.location
    Object.defineProperty(window, 'location', {
      value: { href: '' },
      writable: true,
    });
  });

  describe('authorization header', () => {
    it('should attach Bearer token when token exists', async () => {
      setToken('my-jwt-token');
      mockFetch.mockResolvedValueOnce({
        ok: true,
        status: 200,
        json: () => Promise.resolve({ data: 'test' }),
      });

      await apiClient.get('/test');

      expect(mockFetch).toHaveBeenCalledWith(
        expect.any(String),
        expect.objectContaining({
          headers: expect.objectContaining({
            Authorization: 'Bearer my-jwt-token',
          }),
        })
      );
    });

    it('should not attach Authorization header when no token exists', async () => {
      mockFetch.mockResolvedValueOnce({
        ok: true,
        status: 200,
        json: () => Promise.resolve({ data: 'test' }),
      });

      await apiClient.get('/test');

      const callHeaders = mockFetch.mock.calls[0][1].headers;
      expect(callHeaders.Authorization).toBeUndefined();
    });
  });

  describe('401 response handling', () => {
    it('should clear token and redirect to login on 401', async () => {
      setToken('expired-token');
      mockFetch.mockResolvedValueOnce({
        ok: false,
        status: 401,
        statusText: 'Unauthorized',
        json: () => Promise.resolve({ error: 'Unauthorized' }),
      });

      await expect(apiClient.get('/protected')).rejects.toThrow(ApiError);
      expect(getToken()).toBeNull();
      expect(window.location.href).toBe('/login');
    });
  });

  describe('error handling', () => {
    it('should throw ApiError for non-OK responses', async () => {
      mockFetch.mockResolvedValueOnce({
        ok: false,
        status: 403,
        statusText: 'Forbidden',
        json: () => Promise.resolve({ error: 'Forbidden', message: 'Access denied' }),
      });

      await expect(apiClient.get('/admin-only')).rejects.toThrow(ApiError);
    });
  });

  describe('HTTP methods', () => {
    it('should make GET requests', async () => {
      mockFetch.mockResolvedValueOnce({
        ok: true,
        status: 200,
        json: () => Promise.resolve({ result: 'ok' }),
      });

      const result = await apiClient.get('/endpoint');
      expect(result).toEqual({ result: 'ok' });
      expect(mockFetch.mock.calls[0][1].method).toBe('GET');
    });

    it('should make POST requests with body', async () => {
      mockFetch.mockResolvedValueOnce({
        ok: true,
        status: 200,
        json: () => Promise.resolve({ id: '123' }),
      });

      await apiClient.post('/endpoint', { name: 'test' });
      expect(mockFetch.mock.calls[0][1].method).toBe('POST');
      expect(mockFetch.mock.calls[0][1].body).toBe(JSON.stringify({ name: 'test' }));
    });

    it('should make PUT requests with body', async () => {
      mockFetch.mockResolvedValueOnce({
        ok: true,
        status: 200,
        json: () => Promise.resolve({ updated: true }),
      });

      await apiClient.put('/endpoint/1', { name: 'updated' });
      expect(mockFetch.mock.calls[0][1].method).toBe('PUT');
    });

    it('should make DELETE requests', async () => {
      mockFetch.mockResolvedValueOnce({
        ok: true,
        status: 204,
      });

      await apiClient.delete('/endpoint/1');
      expect(mockFetch.mock.calls[0][1].method).toBe('DELETE');
    });
  });
});
