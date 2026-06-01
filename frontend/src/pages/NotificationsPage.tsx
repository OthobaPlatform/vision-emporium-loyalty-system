import { useState, useEffect, useCallback } from 'react';
import { Helmet } from 'react-helmet-async';
import { apiClient, ApiError } from '../utils/api';
import { useToast } from '../components/Toast';
import { LoadingIndicator } from '../components/LoadingIndicator';

interface FailedNotification {
  notificationId: string;
  customerId: string;
  phoneNumber: string;
  messageType: string;
  content: string;
  deliveryStatus: string;
  failureReason: string | null;
  attemptCount: number;
  sentAt: string;
}

export function NotificationsPage() {
  const { showToast } = useToast();
  const [loading, setLoading] = useState(true);
  const [notifications, setNotifications] = useState<FailedNotification[]>([]);
  const [retryingId, setRetryingId] = useState<string | null>(null);

  const loadNotifications = useCallback(async () => {
    setLoading(true);
    try {
      const data = await apiClient.get<{ notifications: FailedNotification[] }>('/notifications/failed');
      setNotifications(data.notifications);
    } catch (err) {
      const message =
        err instanceof ApiError ? 'Failed to load notifications' : 'Network error';
      showToast('error', message);
    } finally {
      setLoading(false);
    }
  }, [showToast]);

  useEffect(() => {
    loadNotifications();
  }, [loadNotifications]);

  async function handleRetry(notificationId: string) {
    setRetryingId(notificationId);
    try {
      const result = await apiClient.post<{ message: string; status: string }>(
        `/notifications/${notificationId}/retry`
      );
      if (result.status === 'Sent') {
        showToast('success', 'Notification retried successfully');
        // Remove from list or reload
        setNotifications((prev) => prev.filter((n) => n.notificationId !== notificationId));
      } else {
        showToast('error', result.message);
      }
    } catch (err) {
      if (err instanceof ApiError && err.body) {
        const body = err.body as { message?: string };
        showToast('error', body.message || 'Failed to retry notification');
      } else {
        showToast('error', 'Failed to retry notification');
      }
    } finally {
      setRetryingId(null);
    }
  }

  function formatDate(dateStr: string): string {
    const date = new Date(dateStr);
    return date.toLocaleString('en-US', {
      year: 'numeric',
      month: 'short',
      day: 'numeric',
      hour: '2-digit',
      minute: '2-digit',
    });
  }

  function getMessageTypeLabel(type: string): string {
    switch (type) {
      case 'ThresholdReached':
        return 'Threshold Reached';
      case 'ProgressUpdate':
        return 'Progress Update';
      case 'RedemptionConfirmation':
        return 'Redemption Confirmation';
      default:
        return type;
    }
  }

  return (
    <div>
      <Helmet>
        <title>Notifications | Vision Emporium Loyalty</title>
      </Helmet>

      <div className="flex items-center justify-between mb-6">
        <h1 className="text-2xl font-bold text-gray-900">Failed Notifications</h1>
        <button
          onClick={loadNotifications}
          disabled={loading}
          className="rounded-md bg-gray-100 px-3 py-2 text-sm font-medium text-gray-700 hover:bg-gray-200 disabled:opacity-50"
        >
          Refresh
        </button>
      </div>

      {loading ? (
        <LoadingIndicator isLoading={true} label="Loading failed notifications..." />
      ) : notifications.length === 0 ? (
        <div className="rounded-lg bg-white p-8 shadow text-center">
          <svg
            className="mx-auto h-12 w-12 text-gray-400"
            fill="none"
            viewBox="0 0 24 24"
            stroke="currentColor"
            strokeWidth={1.5}
          >
            <path
              strokeLinecap="round"
              strokeLinejoin="round"
              d="M9 12.75L11.25 15 15 9.75M21 12a9 9 0 11-18 0 9 9 0 0118 0z"
            />
          </svg>
          <h3 className="mt-2 text-sm font-medium text-gray-900">No failed notifications</h3>
          <p className="mt-1 text-sm text-gray-500">All SMS notifications have been delivered successfully.</p>
        </div>
      ) : (
        <div className="overflow-hidden rounded-lg bg-white shadow">
          <div className="overflow-x-auto">
            <table className="min-w-full divide-y divide-gray-200">
              <thead className="bg-gray-50">
                <tr>
                  <th className="px-4 py-3 text-left text-xs font-medium uppercase tracking-wider text-gray-500">
                    Phone
                  </th>
                  <th className="px-4 py-3 text-left text-xs font-medium uppercase tracking-wider text-gray-500">
                    Message Type
                  </th>
                  <th className="px-4 py-3 text-left text-xs font-medium uppercase tracking-wider text-gray-500">
                    Timestamp
                  </th>
                  <th className="px-4 py-3 text-left text-xs font-medium uppercase tracking-wider text-gray-500">
                    Error Reason
                  </th>
                  <th className="px-4 py-3 text-left text-xs font-medium uppercase tracking-wider text-gray-500">
                    Attempts
                  </th>
                  <th className="px-4 py-3 text-right text-xs font-medium uppercase tracking-wider text-gray-500">
                    Action
                  </th>
                </tr>
              </thead>
              <tbody className="divide-y divide-gray-200 bg-white">
                {notifications.map((notification) => (
                  <tr key={notification.notificationId} className="hover:bg-gray-50">
                    <td className="whitespace-nowrap px-4 py-3 text-sm text-gray-900">
                      {notification.phoneNumber}
                    </td>
                    <td className="whitespace-nowrap px-4 py-3 text-sm text-gray-600">
                      <span className="inline-flex items-center rounded-full bg-gray-100 px-2.5 py-0.5 text-xs font-medium text-gray-800">
                        {getMessageTypeLabel(notification.messageType)}
                      </span>
                    </td>
                    <td className="whitespace-nowrap px-4 py-3 text-sm text-gray-500">
                      {formatDate(notification.sentAt)}
                    </td>
                    <td className="max-w-xs truncate px-4 py-3 text-sm text-red-600" title={notification.failureReason || ''}>
                      {notification.failureReason || 'Unknown error'}
                    </td>
                    <td className="whitespace-nowrap px-4 py-3 text-sm text-gray-500">
                      {notification.attemptCount}
                    </td>
                    <td className="whitespace-nowrap px-4 py-3 text-right text-sm">
                      <button
                        onClick={() => handleRetry(notification.notificationId)}
                        disabled={retryingId === notification.notificationId}
                        className="rounded-md bg-blue-600 px-3 py-1.5 text-xs font-medium text-white shadow-sm hover:bg-blue-700 disabled:opacity-50 disabled:cursor-not-allowed"
                      >
                        {retryingId === notification.notificationId ? 'Retrying...' : 'Retry'}
                      </button>
                    </td>
                  </tr>
                ))}
              </tbody>
            </table>
          </div>
        </div>
      )}
    </div>
  );
}
