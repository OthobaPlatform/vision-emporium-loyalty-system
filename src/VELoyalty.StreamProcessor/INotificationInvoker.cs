namespace VELoyalty.StreamProcessor;

/// <summary>
/// Abstraction for invoking the Notification Lambda to send SMS notifications.
/// </summary>
public interface INotificationInvoker
{
    /// <summary>
    /// Invokes the Notification Lambda with the given payload.
    /// </summary>
    /// <param name="payload">The notification payload containing customer and gift details.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    Task InvokeAsync(NotificationPayload payload, CancellationToken cancellationToken = default);
}
