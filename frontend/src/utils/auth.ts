import type { JwtPayload } from '../types/auth';

const TOKEN_KEY = 'vel_auth_token';

export function getToken(): string | null {
  return localStorage.getItem(TOKEN_KEY);
}

export function setToken(token: string): void {
  localStorage.setItem(TOKEN_KEY, token);
}

export function removeToken(): void {
  localStorage.removeItem(TOKEN_KEY);
}

export function parseJwt(token: string): JwtPayload | null {
  try {
    const base64Url = token.split('.')[1];
    if (!base64Url) return null;

    const base64 = base64Url.replace(/-/g, '+').replace(/_/g, '/');
    const jsonPayload = decodeURIComponent(
      atob(base64)
        .split('')
        .map((c) => '%' + ('00' + c.charCodeAt(0).toString(16)).slice(-2))
        .join('')
    );

    return JSON.parse(jsonPayload) as JwtPayload;
  } catch {
    return null;
  }
}

export function isTokenExpired(payload: JwtPayload): boolean {
  const now = Math.floor(Date.now() / 1000);
  return payload.exp <= now;
}

export function getAuthState(): { token: string | null; user: JwtPayload | null; isAuthenticated: boolean } {
  const token = getToken();
  if (!token) {
    return { token: null, user: null, isAuthenticated: false };
  }

  const payload = parseJwt(token);
  if (!payload || isTokenExpired(payload)) {
    removeToken();
    return { token: null, user: null, isAuthenticated: false };
  }

  return { token, user: payload, isAuthenticated: true };
}
