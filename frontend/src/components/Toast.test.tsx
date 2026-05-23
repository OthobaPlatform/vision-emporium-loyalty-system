import { render, screen, act, fireEvent } from '@testing-library/react';
import { describe, it, expect, vi, beforeEach, afterEach } from 'vitest';
import { ToastProvider, useToast } from './Toast';

function TestTrigger() {
  const { showToast } = useToast();
  return (
    <div>
      <button onClick={() => showToast('success', 'Operation successful')}>
        Show Success
      </button>
      <button onClick={() => showToast('error', 'Something went wrong')}>
        Show Error
      </button>
    </div>
  );
}

describe('Toast', () => {
  beforeEach(() => {
    vi.useFakeTimers();
  });

  afterEach(() => {
    vi.useRealTimers();
  });

  it('shows a success toast when triggered', () => {
    render(
      <ToastProvider>
        <TestTrigger />
      </ToastProvider>
    );

    fireEvent.click(screen.getByText('Show Success'));
    expect(screen.getByText('Operation successful')).toBeInTheDocument();
  });

  it('shows an error toast when triggered', () => {
    render(
      <ToastProvider>
        <TestTrigger />
      </ToastProvider>
    );

    fireEvent.click(screen.getByText('Show Error'));
    expect(screen.getByText('Something went wrong')).toBeInTheDocument();
  });

  it('auto-dismisses toast after 4 seconds', () => {
    render(
      <ToastProvider>
        <TestTrigger />
      </ToastProvider>
    );

    fireEvent.click(screen.getByText('Show Success'));
    expect(screen.getByText('Operation successful')).toBeInTheDocument();

    act(() => {
      vi.advanceTimersByTime(4000);
    });

    expect(screen.queryByText('Operation successful')).not.toBeInTheDocument();
  });

  it('can be manually dismissed', () => {
    render(
      <ToastProvider>
        <TestTrigger />
      </ToastProvider>
    );

    fireEvent.click(screen.getByText('Show Success'));
    expect(screen.getByText('Operation successful')).toBeInTheDocument();

    fireEvent.click(screen.getByLabelText('Dismiss notification'));
    expect(screen.queryByText('Operation successful')).not.toBeInTheDocument();
  });

  it('throws error when useToast is used outside provider', () => {
    function BadComponent() {
      useToast();
      return null;
    }

    expect(() => render(<BadComponent />)).toThrow(
      'useToast must be used within a ToastProvider'
    );
  });
});
