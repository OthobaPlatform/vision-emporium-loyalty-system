using Asp.Versioning;
using VELoyalty.Api.Services;
using VELoyalty.Auth;

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

        return group;
    }
}
