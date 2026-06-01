using VELoyalty.Api.Services;
using VELoyalty.Auth;

namespace VELoyalty.Api.Endpoints;

public static class UserEndpoints
{
    public static WebApplication MapUserEndpoints(this WebApplication app)
    {
        // GET /api/v1/users
        app.MapGet("/api/v1/users", async (
            UserService userService,
            CancellationToken cancellationToken) =>
        {
            var users = await userService.ListUsersAsync(cancellationToken);
            return Results.Ok(new { users });
        }).RequireAdmin();

        // POST /api/v1/users
        app.MapPost("/api/v1/users", async (
            CreateUserRequest request,
            UserService userService,
            CancellationToken cancellationToken) =>
        {
            var result = await userService.CreateUserAsync(request, cancellationToken);

            if (result.ValidationErrors is { Count: > 0 })
                return Results.BadRequest(new { error = "ValidationError", details = result.ValidationErrors });

            return Results.Created($"/api/v1/users/{result.User!.UserId}", result.User);
        }).RequireAdmin();

        // PUT /api/v1/users/{id}
        app.MapPut("/api/v1/users/{id}", async (
            string id,
            UpdateUserRequest request,
            UserService userService,
            CancellationToken cancellationToken) =>
        {
            var result = await userService.UpdateUserAsync(id, request, cancellationToken);

            if (result.IsNotFound)
                return Results.NotFound(new { error = "NotFound", message = "User not found." });

            if (result.ValidationErrors is { Count: > 0 })
                return Results.BadRequest(new { error = "ValidationError", details = result.ValidationErrors });

            return Results.Ok(result.User);
        }).RequireAdmin();

        return app;
    }
}
