import { useState, type FormEvent } from 'react';
import { useNavigate } from 'react-router-dom';
import { Helmet } from 'react-helmet-async';
import { useAuth } from '../contexts/AuthContext';
import { VisionEmporiumLogo } from '../components/VisionEmporiumLogo';

export function LoginPage() {
  const [email, setEmail] = useState('');
  const [password, setPassword] = useState('');
  const [error, setError] = useState('');
  const [isLoading, setIsLoading] = useState(false);

  const { login, user, isAuthenticated } = useAuth();
  const navigate = useNavigate();

  // If already authenticated, redirect to appropriate landing page
  if (isAuthenticated && user) {
    const landingPage = user.role === 'Admin' ? '/dashboard' : '/redemptions';
    navigate(landingPage, { replace: true });
  }

  const handleSubmit = async (e: FormEvent) => {
    e.preventDefault();
    setError('');

    if (!email.trim()) {
      setError('Email is required');
      return;
    }

    if (!password.trim()) {
      setError('Password is required');
      return;
    }

    setIsLoading(true);

    try {
      await login({ email, password });
      const token = localStorage.getItem('vel_auth_token');
      if (token) {
        const base64Url = token.split('.')[1];
        const base64 = base64Url.replace(/-/g, '+').replace(/_/g, '/');
        const payload = JSON.parse(
          decodeURIComponent(
            atob(base64)
              .split('')
              .map((c) => '%' + ('00' + c.charCodeAt(0).toString(16)).slice(-2))
              .join('')
          )
        );
        const landingPage = payload.role === 'Admin' ? '/dashboard' : '/redemptions';
        navigate(landingPage, { replace: true });
      }
    } catch (err) {
      setError(err instanceof Error ? err.message : 'Login failed. Please try again.');
    } finally {
      setIsLoading(false);
    }
  };

  return (
    <div className="min-h-screen flex items-center justify-center bg-[#D6E4F0] px-4 py-12 sm:px-6 lg:px-8">
      <Helmet>
        <title>Sign In | Vision Emporium Loyalty</title>
      </Helmet>
      {/* Red border accent (matching the brand image) */}
      <div className="absolute inset-0 border-[6px] border-[#E31E24] pointer-events-none" />

      <div className="w-full max-w-md space-y-8">
        {/* Logo */}
        <div className="flex flex-col items-center">
          <VisionEmporiumLogo size="lg" />
          <p className="mt-6 text-center text-sm text-gray-600">
            Loyalty Management System
          </p>
        </div>

        {/* Login Card */}
        <div className="rounded-xl bg-white p-8 shadow-lg border border-gray-100">
          <h2 className="text-center text-lg font-semibold text-gray-800 mb-6">
            Sign in to your account
          </h2>

          <form className="space-y-5" onSubmit={handleSubmit} noValidate>
            {error && (
              <div
                className="rounded-md bg-red-50 border border-red-200 p-3"
                role="alert"
                aria-live="assertive"
              >
                <p className="text-sm text-[#E31E24]">{error}</p>
              </div>
            )}

            <div>
              <label
                htmlFor="email"
                className="block text-sm font-medium text-gray-700"
              >
                Email address
              </label>
              <input
                id="email"
                name="email"
                type="email"
                autoComplete="email"
                required
                value={email}
                onChange={(e) => setEmail(e.target.value)}
                className="mt-1 block w-full rounded-lg border border-gray-300 px-4 py-2.5 text-gray-900 placeholder-gray-400 focus:border-[#E31E24] focus:outline-none focus:ring-2 focus:ring-[#E31E24]/20 sm:text-sm transition-colors"
                placeholder="you@example.com"
                disabled={isLoading}
              />
            </div>

            <div>
              <label
                htmlFor="password"
                className="block text-sm font-medium text-gray-700"
              >
                Password
              </label>
              <input
                id="password"
                name="password"
                type="password"
                autoComplete="current-password"
                required
                value={password}
                onChange={(e) => setPassword(e.target.value)}
                className="mt-1 block w-full rounded-lg border border-gray-300 px-4 py-2.5 text-gray-900 placeholder-gray-400 focus:border-[#E31E24] focus:outline-none focus:ring-2 focus:ring-[#E31E24]/20 sm:text-sm transition-colors"
                placeholder="Enter your password"
                disabled={isLoading}
              />
            </div>

            <button
              type="submit"
              disabled={isLoading}
              className="flex w-full justify-center rounded-lg bg-[#E31E24] px-4 py-2.5 text-sm font-semibold text-white shadow-sm hover:bg-[#c91a1f] focus-visible:outline-2 focus-visible:outline-offset-2 focus-visible:outline-[#E31E24] disabled:opacity-50 disabled:cursor-not-allowed transition-colors"
            >
              {isLoading ? 'Signing in...' : 'Sign in'}
            </button>
          </form>
        </div>

        {/* Footer */}
        <p className="text-center text-xs text-gray-500">
          © {new Date().getFullYear()} Vision Emporium. All rights reserved.
        </p>
      </div>
    </div>
  );
}
