using Asp.Versioning;
using VELoyalty.Api.Services;
using VELoyalty.Auth;

namespace VELoyalty.Api.Endpoints;

public static class UserEndpoints
{
    public static RouteGroupBuilder MapUserEndpoints(this RouteGroupBuilder group)
    {
        // GET /users
        group.MapGet("/users", async (
            UserService userService,
            CancellationToken cancellationToken) =>
        {
            var users = await userService.ListUsersAsync(cancellationToken);
            return Results.Ok(new { users });
        }).RequireAdmin().MapToApiVersion(1, 0);

        // POST /users
        group.MapPost("/users", async (
            CreateUserRequest request,
            UserService userService,
            CancellationToken cancellationToken) =>
        {
            var result = await userService.CreateUserAsync(request, cancellationToken);

            if (result.ValidationErrors is { Count: > 0 })
                return Results.BadRequest(new { error = "ValidationError", details = result.ValidationErrors });

            return Results.Created($"/api/v1/users/{result.User!.UserId}", result.User);
        }).RequireAdmin().MapToApiVersion(1, 0);

        // PUT /users/{id}
        group.MapPut("/users/{id}", async (
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
        }).RequireAdmin().MapToApiVersion(1, 0);

        return group;
    }
}
