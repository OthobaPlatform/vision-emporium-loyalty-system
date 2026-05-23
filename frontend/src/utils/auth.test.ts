import { describe, it, expect, beforeEach } from 'vitest';
import { getToken, setToken, removeToken, parseJwt, isTokenExpired, getAuthState } from './auth';

function createMockJwt(payload: Record<string, unknown>): string {
  const header = btoa(JSON.stringify({ alg: 'HS256', typ: 'JWT' }));
  const body = btoa(JSON.stringify(payload));
  const signature = 'mock-signature';
  return `${header}.${body}.${signature}`;
}

describe('auth utilities', () => {
  beforeEach(() => {
    localStorage.clear();
  });

  describe('token storage', () => {
    it('should store and retrieve a token', () => {
      setToken('test-token');
      expect(getToken()).toBe('test-token');
    });

    it('should return null when no token is stored', () => {
      expect(getToken()).toBeNull();
    });

    it('should remove a stored token', () => {
      setToken('test-token');
      removeToken();
      expect(getToken()).toBeNull();
    });
  });

  describe('parseJwt', () => {
    it('should parse a valid JWT payload', () => {
      const payload = {
        sub: 'user-123',
        role: 'Admin',
        iat: 1700000000,
        exp: 1700028800,
      };
      const token = createMockJwt(payload);
      const result = parseJwt(token);

      expect(result).toEqual(payload);
    });

    it('should parse a JWT with outletId for Outlet_Manager', () => {
      const payload = {
        sub: 'user-456',
        role: 'Outlet_Manager',
        outletId: 'outlet-1',
        iat: 1700000000,
        exp: 1700028800,
      };
      const token = createMockJwt(payload);
      const result = parseJwt(token);

      expect(result).toEqual(payload);
    });

    it('should return null for invalid token', () => {
      expect(parseJwt('invalid')).toBeNull();
      expect(parseJwt('')).toBeNull();
      expect(parseJwt('a.b')).toBeNull();
    });
  });

  describe('isTokenExpired', () => {
    it('should return false for a non-expired token', () => {
      const futureExp = Math.floor(Date.now() / 1000) + 3600;
      const payload = { sub: 'user-1', role: 'Admin' as const, iat: 1700000000, exp: futureExp };
      expect(isTokenExpired(payload)).toBe(false);
    });

    it('should return true for an expired token', () => {
      const pastExp = Math.floor(Date.now() / 1000) - 3600;
      const payload = { sub: 'user-1', role: 'Admin' as const, iat: 1700000000, exp: pastExp };
      expect(isTokenExpired(payload)).toBe(true);
    });

    it('should return true when exp equals current time', () => {
      const now = Math.floor(Date.now() / 1000);
      const payload = { sub: 'user-1', role: 'Admin' as const, iat: 1700000000, exp: now };
      expect(isTokenExpired(payload)).toBe(true);
    });
  });

  describe('getAuthState', () => {
    it('should return unauthenticated state when no token exists', () => {
      const state = getAuthState();
      expect(state.isAuthenticated).toBe(false);
      expect(state.token).toBeNull();
      expect(state.user).toBeNull();
    });

    it('should return authenticated state for valid non-expired token', () => {
      const futureExp = Math.floor(Date.now() / 1000) + 3600;
      const payload = { sub: 'user-1', role: 'Admin', iat: 1700000000, exp: futureExp };
      const token = createMockJwt(payload);
      setToken(token);

      const state = getAuthState();
      expect(state.isAuthenticated).toBe(true);
      expect(state.token).toBe(token);
      expect(state.user?.sub).toBe('user-1');
      expect(state.user?.role).toBe('Admin');
    });

    it('should return unauthenticated state and clear expired token', () => {
      const pastExp = Math.floor(Date.now() / 1000) - 3600;
      const payload = { sub: 'user-1', role: 'Admin', iat: 1700000000, exp: pastExp };
      const token = createMockJwt(payload);
      setToken(token);

      const state = getAuthState();
      expect(state.isAuthenticated).toBe(false);
      expect(state.token).toBeNull();
      expect(state.user).toBeNull();
      // Token should be removed from localStorage
      expect(getToken()).toBeNull();
    });
  });
});
