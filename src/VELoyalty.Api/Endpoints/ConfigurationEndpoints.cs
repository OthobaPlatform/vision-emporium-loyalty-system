using VELoyalty.Api.Services;
using VELoyalty.Auth;

namespace VELoyalty.Api.Endpoints;

public static class ConfigurationEndpoints
{
    public static WebApplication MapConfigurationEndpoints(this WebApplication app)
    {
        // GET /api/v1/config/cycle
        app.MapGet("/api/v1/config/cycle", async (
            ConfigurationService configService,
            CancellationToken cancellationToken) =>
        {
            var cycle = await configService.GetCycleConfigAsync(cancellationToken);
            if (cycle is null)
                return Results.NotFound(new { error = "NotFound", message = "No active loyalty cycle configured." });

            return Results.Ok(cycle);
        }).RequireAdmin();

        // PUT /api/v1/config/cycle
        app.MapPut("/api/v1/config/cycle", async (
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
        }).RequireAdmin();

        // GET /api/v1/config/thresholds
        app.MapGet("/api/v1/config/thresholds", async (
            ConfigurationService configService,
            CancellationToken cancellationToken) =>
        {
            var thresholds = await configService.GetThresholdConfigsAsync(cancellationToken);
            return Results.Ok(new { thresholds });
        }).RequireAdmin();

        // PUT /api/v1/config/thresholds
        app.MapPut("/api/v1/config/thresholds", async (
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
        }).RequireAdmin();

        // GET /api/v1/config/general
        app.MapGet("/api/v1/config/general", async (
            ConfigurationService configService,
            CancellationToken cancellationToken) =>
        {
            var config = await configService.GetGeneralConfigAsync(cancellationToken);
            return Results.Ok(config);
        }).RequireAdmin();

        // PUT /api/v1/config/general
        app.MapPut("/api/v1/config/general", async (
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
        }).RequireAdmin();

        return app;
    }
}
