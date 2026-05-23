using System.Text.Json;
using Amazon.Lambda;
using Amazon.Lambda.Model;

namespace VELoyalty.StreamProcessor;

/// <summary>
/// Invokes the Notification Lambda function asynchronously using the AWS Lambda SDK.
/// Uses InvokeAsync (Event invocation type) so the Stream Processor does not wait for the notification to complete.
/// </summary>
public class LambdaNotificationInvoker : INotificationInvoker
{
    private readonly IAmazonLambda _lambdaClient;
    private readonly string _notificationFunctionName;

    public LambdaNotificationInvoker(IAmazonLambda lambdaClient, string notificationFunctionName)
    {
        _lambdaClient = lambdaClient ?? throw new ArgumentNullException(nameof(lambdaClient));
        _notificationFunctionName = notificationFunctionName ?? throw new ArgumentNullException(nameof(notificationFunctionName));
    }

    /// <summary>
    /// Invokes the Notification Lambda with the given payload using Event invocation type (fire-and-forget).
    /// </summary>
    public async Task InvokeAsync(NotificationPayload payload, CancellationToken cancellationToken = default)
    {
        var payloadJson = JsonSerializer.Serialize(payload, StreamProcessorJsonContext.Default.NotificationPayload);

        var request = new InvokeRequest
        {
            FunctionName = _notificationFunctionName,
            InvocationType = InvocationType.Event, // Asynchronous invocation
            Payload = payloadJson
        };

        await _lambdaClient.InvokeAsync(request, cancellationToken);
    }
}
