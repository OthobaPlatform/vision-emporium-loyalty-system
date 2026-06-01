using Asp.Versioning;
using VELoyalty.Api.Services;
using VELoyalty.Auth;
using VELoyalty.Core;
using VELoyalty.Data.Repositories;

namespace VELoyalty.Api.Endpoints;

public static class ConfigurationEndpoints
{
    public static RouteGroupBuilder MapConfigurationEndpoints(this RouteGroupBuilder group)
    {
        // GET /config/cycle
        group.MapGet("/config/cycle", async (
            ConfigurationService configService,
            CancellationToken cancellationToken) =>
        {
            var cycle = await configService.GetCycleConfigAsync(cancellationToken);
            if (cycle is null)
                return Results.NotFound(new { error = "NotFound", message = "No active loyalty cycle configured." });

            return Results.Ok(cycle);
        }).RequireAdmin().MapToApiVersion(1, 0);

        // PUT /config/cycle
        group.MapPut("/config/cycle", async (
            HttpContext httpContext,
            UpdateCycleRequest request,
            ConfigurationService configService,
            CancellationToken cancellationToken) =>
        {
            var actorId = httpContext.GetUserId() ?? "system";
            var result = await configService.UpdateCycleConfigAsync(request, actorId, cancellationToken);

            if (!result.IsSuccess)
                return Results.BadRequest(new { error = "ValidationError", details = result.Errors });

            return Results.Ok(result.Data);
        }).RequireAdmin().MapToApiVersion(1, 0);

        // GET /config/thresholds
        group.MapGet("/config/thresholds", async (
            ConfigurationService configService,
            CancellationToken cancellationToken) =>
        {
            var thresholds = await configService.GetThresholdConfigsAsync(cancellationToken);
            return Results.Ok(new { thresholds });
        }).RequireAdmin().MapToApiVersion(1, 0);

        // PUT /config/thresholds
        group.MapPut("/config/thresholds", async (
            HttpContext httpContext,
            UpdateThresholdsRequest request,
            ConfigurationService configService,
            CancellationToken cancellationToken) =>
        {
            var actorId = httpContext.GetUserId() ?? "system";
            var result = await configService.UpdateThresholdConfigsAsync(request, actorId, cancellationToken);

            if (!result.IsSuccess)
                return Results.BadRequest(new { error = "ValidationError", details = result.Errors });

            return Results.Ok(new { thresholds = result.Data });
        }).RequireAdmin().MapToApiVersion(1, 0);

        // GET /config/general
        group.MapGet("/config/general", async (
            ConfigurationService configService,
            CancellationToken cancellationToken) =>
        {
            var config = await configService.GetGeneralConfigAsync(cancellationToken);
            return Results.Ok(config);
        }).RequireAdmin().MapToApiVersion(1, 0);

        // PUT /config/general
        group.MapPut("/config/general", async (
            HttpContext httpContext,
            UpdateGeneralConfigRequest request,
            ConfigurationService configService,
            CancellationToken cancellationToken) =>
        {
            var actorId = httpContext.GetUserId() ?? "system";
            var result = await configService.UpdateGeneralConfigAsync(request, actorId, cancellationToken);

            if (!result.IsSuccess)
                return Results.BadRequest(new { error = "ValidationError", details = result.Errors });

            return Results.Ok(result.Data);
        }).RequireAdmin().MapToApiVersion(1, 0);

        // GET /config/sms
        group.MapGet("/config/sms", async (
            ConfigRepository configRepository,
            CancellationToken cancellationToken) =>
        {
            var config = await configRepository.GetSmsConfigAsync(cancellationToken);
            if (config is null)
                return Results.Ok(new SmsConfigResponse(false, "", "", ""));

            // Mask the API key in response
            var maskedApiKey = config.ApiKey.Length > 4
                ? new string('*', config.ApiKey.Length - 4) + config.ApiKey[^4..]
                : new string('*', config.ApiKey.Length);

            return Results.Ok(new SmsConfigResponse(
                config.Enabled,
                config.BaseUrl,
                maskedApiKey,
                config.SenderId));
        }).RequireAdmin().MapToApiVersion(1, 0);

        // PUT /config/sms
        group.MapPut("/config/sms", async (
            UpdateSmsConfigRequest request,
            ConfigRepository configRepository,
            CancellationToken cancellationToken) =>
        {
            var errors = new List<string>();
            if (request.Enabled && string.IsNullOrWhiteSpace(request.BaseUrl))
                errors.Add("Base URL is required when SMS is enabled.");
            if (request.Enabled && string.IsNullOrWhiteSpace(request.ApiKey))
                errors.Add("API Key is required when SMS is enabled.");
            if (request.Enabled && string.IsNullOrWhiteSpace(request.SenderId))
                errors.Add("Sender ID is required when SMS is enabled.");

            if (errors.Count > 0)
                return Results.BadRequest(new { error = "ValidationError", details = errors });

            // If API key is masked (unchanged), preserve the existing one
            var existingConfig = await configRepository.GetSmsConfigAsync(cancellationToken);
            var apiKey = request.ApiKey;
            if (apiKey.Contains('*') && existingConfig is not null)
            {
                apiKey = existingConfig.ApiKey;
            }

            var config = new SmsConfig(
                Enabled: request.Enabled,
                BaseUrl: request.BaseUrl ?? "",
                ApiKey: apiKey ?? "",
                SenderId: request.SenderId ?? "VisionEmporium"
            );

            await configRepository.PutSmsConfigAsync(config, cancellationToken);

            // Return masked response
            var maskedApiKey = config.ApiKey.Length > 4
                ? new string('*', config.ApiKey.Length - 4) + config.ApiKey[^4..]
                : new string('*', config.ApiKey.Length);

            return Results.Ok(new SmsConfigResponse(
                config.Enabled,
                config.BaseUrl,
                maskedApiKey,
                config.SenderId));
        }).RequireAdmin().MapToApiVersion(1, 0);

        // GET /notifications/failed
        group.MapGet("/notifications/failed", async (
            NotificationRepository notificationRepository,
            CancellationToken cancellationToken) =>
        {
            var notifications = await notificationRepository.GetFailedNotificationsAsync(cancellationToken: cancellationToken);
            return Results.Ok(new { notifications });
        }).RequireAdmin().MapToApiVersion(1, 0);

        // POST /notifications/{id}/retry
        group.MapPost("/notifications/{id}/retry", async (
            string id,
            NotificationRepository notificationRepository,
            SmsService smsService,
            CancellationToken cancellationToken) =>
        {
            var notification = await notificationRepository.GetNotificationByIdAsync(id, cancellationToken);
            if (notification is null)
                return Results.NotFound(new { error = "NotFound", message = "Notification not found." });

            if (notification.DeliveryStatus != "Failed")
                return Results.BadRequest(new { error = "InvalidState", message = "Only failed notifications can be retried." });

            var success = await smsService.RetrySendAsync(notification, cancellationToken);
            return success
                ? Results.Ok(new { message = "Notification retried successfully.", status = "Sent" })
                : Results.Ok(new { message = "Retry failed. Notification remains in failed state.", status = "Failed" });
        }).RequireAdmin().MapToApiVersion(1, 0);

        return group;
    }
}

// ─── SMS Config DTOs ────────────────────────────────────────────────────────────

/// <summary>
/// Response for SMS configuration (with masked API key).
/// </summary>
public record SmsConfigResponse(
    bool Enabled,
    string BaseUrl,
    string ApiKey,
    string SenderId
);

/// <summary>
/// Request to update SMS configuration.
/// </summary>
public record UpdateSmsConfigRequest(
    bool Enabled,
    string? BaseUrl,
    string? ApiKey,
    string? SenderId
);

/// <summary>
/// Response for a notification log entry.
/// </summary>
public record NotificationResponse(
    string NotificationId,
    string CustomerId,
    string PhoneNumber,
    string MessageType,
    string Content,
    string DeliveryStatus,
    string? FailureReason,
    int AttemptCount,
    DateTime SentAt
);
