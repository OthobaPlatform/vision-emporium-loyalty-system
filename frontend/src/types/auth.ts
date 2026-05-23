export type UserRole = 'Admin' | 'Outlet_Manager';

export interface JwtPayload {
  sub: string;
  role: UserRole;
  outletId?: string;
  iat: number;
  exp: number;
}

export interface LoginRequest {
  email: string;
  password: string;
}

export interface LoginResponse {
  token: string;
  expiresAt: string;
}

