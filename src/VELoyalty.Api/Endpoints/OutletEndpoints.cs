using VELoyalty.Api.Services;
using VELoyalty.Auth;

namespace VELoyalty.Api.Endpoints;

public static class OutletEndpoints
{
    public static WebApplication MapOutletEndpoints(this WebApplication app)
    {
        // GET /api/v1/outlets
        app.MapGet("/api/v1/outlets", async (
            OutletService outletService,
            CancellationToken cancellationToken) =>
        {
            var outlets = await outletService.ListAllAsync(cancellationToken);
            return Results.Ok(new { outlets });
        }).RequireAdmin();

        // POST /api/v1/outlets
        app.MapPost("/api/v1/outlets", async (
            CreateOutletRequest request,
            OutletService outletService,
            CancellationToken cancellationToken) =>
        {
            if (string.IsNullOrWhiteSpace(request.Name))
                return Results.BadRequest(new { error = "ValidationError", message = "Outlet name is required." });
            if (string.IsNullOrWhiteSpace(request.Address))
                return Results.BadRequest(new { error = "ValidationError", message = "Outlet address is required." });
            if (string.IsNullOrWhiteSpace(request.PhoneNumber))
                return Results.BadRequest(new { error = "ValidationError", message = "Outlet phone number is required." });
            if (string.IsNullOrWhiteSpace(request.AssignedManagerId))
                return Results.BadRequest(new { error = "ValidationError", message = "Assigned manager ID is required." });

            var outlet = await outletService.CreateAsync(request, cancellationToken);
            return Results.Created($"/api/v1/outlets/{outlet.OutletId}", outlet);
        }).RequireAdmin();

        // PUT /api/v1/outlets/{id}
        app.MapPut("/api/v1/outlets/{id}", async (
            string id,
            UpdateOutletRequest request,
            OutletService outletService,
            CancellationToken cancellationToken) =>
        {
            if (string.IsNullOrWhiteSpace(request.Name))
                return Results.BadRequest(new { error = "ValidationError", message = "Outlet name is required." });
            if (string.IsNullOrWhiteSpace(request.Address))
                return Results.BadRequest(new { error = "ValidationError", message = "Outlet address is required." });
            if (string.IsNullOrWhiteSpace(request.PhoneNumber))
                return Results.BadRequest(new { error = "ValidationError", message = "Outlet phone number is required." });
            if (string.IsNullOrWhiteSpace(request.AssignedManagerId))
                return Results.BadRequest(new { error = "ValidationError", message = "Assigned manager ID is required." });

            var result = await outletService.UpdateAsync(id, request, cancellationToken);
            if (result is null)
                return Results.NotFound(new { error = "NotFound", message = "Outlet not found." });

            return Results.Ok(result);
        }).RequireAdmin();

        // PATCH /api/v1/outlets/{id}/status
        app.MapPatch("/api/v1/outlets/{id}/status", async (
            string id,
            UpdateOutletStatusRequest request,
            OutletService outletService,
            CancellationToken cancellationToken) =>
        {
            var result = await outletService.UpdateStatusAsync(id, request.IsActive, cancellationToken);

            if (!result.IsSuccess)
            {
                return result.ErrorType switch
                {
                    "NotFound" => Results.NotFound(new { error = "NotFound", message = result.Message }),
                    "ValidationError" => Results.BadRequest(new { error = "ValidationError", message = result.Message }),
                    _ => Results.StatusCode(500)
                };
            }

            return Results.Ok(result.Outlet);
        }).RequireAdmin();

        return app;
    }
}
