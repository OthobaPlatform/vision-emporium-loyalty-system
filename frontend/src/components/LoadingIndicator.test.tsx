import { render, screen, act } from '@testing-library/react';
import { describe, it, expect, vi, beforeEach, afterEach } from 'vitest';
import { LoadingIndicator } from './LoadingIndicator';

describe('LoadingIndicator', () => {
  beforeEach(() => {
    vi.useFakeTimers();
  });

  afterEach(() => {
    vi.useRealTimers();
  });

  it('does not render immediately when isLoading is true', () => {
    render(<LoadingIndicator isLoading={true} />);
    expect(screen.queryByRole('status')).not.toBeInTheDocument();
  });

  it('renders after 300ms delay when isLoading is true', () => {
    render(<LoadingIndicator isLoading={true} />);

    act(() => {
      vi.advanceTimersByTime(300);
    });

    expect(screen.getByRole('status')).toBeInTheDocument();
    expect(screen.getByText('Loading...')).toBeInTheDocument();
  });

  it('does not render when isLoading is false', () => {
    render(<LoadingIndicator isLoading={false} />);

    act(() => {
      vi.advanceTimersByTime(500);
    });

    expect(screen.queryByRole('status')).not.toBeInTheDocument();
  });

  it('hides when isLoading changes from true to false before delay', () => {
    const { rerender } = render(<LoadingIndicator isLoading={true} />);

    act(() => {
      vi.advanceTimersByTime(100);
    });

    rerender(<LoadingIndicator isLoading={false} />);

    act(() => {
      vi.advanceTimersByTime(300);
    });

    expect(screen.queryByRole('status')).not.toBeInTheDocument();
  });

  it('uses custom delay value', () => {
    render(<LoadingIndicator isLoading={true} delay={500} />);

    act(() => {
      vi.advanceTimersByTime(300);
    });
    expect(screen.queryByRole('status')).not.toBeInTheDocument();

    act(() => {
      vi.advanceTimersByTime(200);
    });
    expect(screen.getByRole('status')).toBeInTheDocument();
  });

  it('displays custom label', () => {
    render(<LoadingIndicator isLoading={true} label="Fetching data..." />);

    act(() => {
      vi.advanceTimersByTime(300);
    });

    expect(screen.getByText('Fetching data...')).toBeInTheDocument();
  });
});
