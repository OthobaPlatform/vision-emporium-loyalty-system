import { useState, useEffect } from 'react';

interface LoadingIndicatorProps {
  /** Whether the loading state is active */
  isLoading: boolean;
  /** Delay in ms before showing the indicator (default: 300ms) */
  delay?: number;
  /** Optional label for accessibility */
  label?: string;
}

/**
 * Loading indicator that only appears after a configurable delay (default 300ms)
 * to avoid flashing for fast operations. Uses Vision Emporium brand gradient.
 */
export function LoadingIndicator({
  isLoading,
  delay = 300,
  label = 'Loading...',
}: LoadingIndicatorProps) {
  const [showSpinner, setShowSpinner] = useState(false);

  useEffect(() => {
    if (!isLoading) {
      setShowSpinner(false);
      return;
    }

    const timer = setTimeout(() => {
      setShowSpinner(true);
    }, delay);

    return () => clearTimeout(timer);
  }, [isLoading, delay]);

  if (!showSpinner) {
    return null;
  }

  return (
    <div
      className="flex items-center justify-center p-4"
      role="status"
      aria-live="polite"
      aria-label={label}
    >
      <svg
        className="h-7 w-7 animate-spin"
        xmlns="http://www.w3.org/2000/svg"
        fill="none"
        viewBox="0 0 24 24"
        aria-hidden="true"
      >
        <defs>
          <linearGradient id="ve-spinner-gradient" x1="0%" y1="0%" x2="100%" y2="100%">
            <stop offset="0%" stopColor="#E31E24" />
            <stop offset="100%" stopColor="#1a1a1a" />
          </linearGradient>
        </defs>
        <circle
          cx="12"
          cy="12"
          r="10"
          stroke="#E31E24"
          strokeWidth="3"
          opacity="0.2"
        />
        <path
          fill="url(#ve-spinner-gradient)"
          d="M4 12a8 8 0 018-8V0C5.373 0 0 5.373 0 12h4zm2 5.291A7.962 7.962 0 014 12H0c0 3.042 1.135 5.824 3 7.938l3-2.647z"
        />
      </svg>
      <span className="ml-2 text-sm font-medium text-gray-700">{label}</span>
    </div>
  );
}
