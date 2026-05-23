import { createContext, useContext, useState, useCallback, type ReactNode } from 'react';
import type { JwtPayload, LoginRequest, LoginResponse, UserRole } from '../types/auth';
import { getAuthState, setToken, removeToken, parseJwt } from '../utils/auth';

interface AuthContextType {
  user: JwtPayload | null;
  isAuthenticated: boolean;
  login: (credentials: LoginRequest) => Promise<void>;
  logout: () => void;
  hasRole: (role: UserRole) => boolean;
}

const AuthContext = createContext<AuthContextType | undefined>(undefined);

const API_BASE_URL = import.meta.env.VITE_API_BASE_URL || '/api/v1';

export function AuthProvider({ children }: { children: ReactNode }) {
  const [authState, setAuthState] = useState(() => getAuthState());

  const login = useCallback(async (credentials: LoginRequest) => {
    const response = await fetch(`${API_BASE_URL}/auth/login`, {
      method: 'POST',
      headers: { 'Content-Type': 'application/json' },
      body: JSON.stringify(credentials),
    });

    if (!response.ok) {
      const errorBody = await response.json().catch(() => ({ message: 'Login failed' }));
      throw new Error(errorBody.message || 'Invalid email or password');
    }

    const data: LoginResponse = await response.json();
    setToken(data.token);

    const user = parseJwt(data.token);
    setAuthState({ token: data.token, user, isAuthenticated: true });
  }, []);

  const logout = useCallback(() => {
    removeToken();
    setAuthState({ token: null, user: null, isAuthenticated: false });
  }, []);

  const hasRole = useCallback(
    (role: UserRole) => {
      return authState.user?.role === role;
    },
    [authState.user]
  );

  return (
    <AuthContext.Provider
      value={{
        user: authState.user,
        isAuthenticated: authState.isAuthenticated,
        login,
        logout,
        hasRole,
      }}
    >
      {children}
    </AuthContext.Provider>
  );
}

export function useAuth(): AuthContextType {
  const context = useContext(AuthContext);
  if (context === undefined) {
    throw new Error('useAuth must be used within an AuthProvider');
  }
  return context;
}
