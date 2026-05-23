using System.Text.Json.Serialization;
using Microsoft.AspNetCore.Http;
using VELoyalty.Core;

namespace VELoyalty.Auth;

/// <summary>
/// Standard 403 Forbidden response body for insufficient permissions.
/// </summary>
public record ForbiddenResponse(
    [property: JsonPropertyName("error")] string Error,
    [property: JsonPropertyName("message")] string Message
);

/// <summary>
/// Source-generated JSON context for AOT-compatible serialization of auth responses.
/// </summary>
[JsonSerializable(typeof(ForbiddenResponse))]
internal partial class AuthJsonContext : JsonSerializerContext
{
}

/// <summary>
/// Endpoint filter that requires the user to have the Admin role.
/// Returns HTTP 403 if the user does not have the required role.
/// </summary>
public sealed class RequireAdminFilter : IEndpointFilter
{
    private static readonly ForbiddenResponse Forbidden = new("Forbidden", "Insufficient permissions");

    public async ValueTask<object?> InvokeAsync(EndpointFilterInvocationContext context, EndpointFilterDelegate next)
    {
        var role = context.HttpContext.Items[AuthorizationMiddleware.ContextKeys.Role] as string;

        if (string.IsNullOrWhiteSpace(role))
        {
            return Results.Json(Forbidden, AuthJsonContext.Default.ForbiddenResponse, statusCode: StatusCodes.Status403Forbidden);
        }

        if (!string.Equals(role, nameof(UserRole.Admin), StringComparison.Ordinal))
        {
            return Results.Json(Forbidden, AuthJsonContext.Default.ForbiddenResponse, statusCode: StatusCodes.Status403Forbidden);
        }

        return await next(context);
    }
}

/// <summary>
/// Endpoint filter that requires the user to have the Outlet_Manager role.
/// Returns HTTP 403 if the user does not have the required role.
/// </summary>
public sealed class RequireOutletManagerFilter : IEndpointFilter
{
    private static readonly ForbiddenResponse Forbidden = new("Forbidden", "Insufficient permissions");

    public async ValueTask<object?> InvokeAsync(EndpointFilterInvocationContext context, EndpointFilterDelegate next)
    {
        var role = context.HttpContext.Items[AuthorizationMiddleware.ContextKeys.Role] as string;

        if (string.IsNullOrWhiteSpace(role))
        {
            return Results.Json(Forbidden, AuthJsonContext.Default.ForbiddenResponse, statusCode: StatusCodes.Status403Forbidden);
        }

        if (!string.Equals(role, nameof(UserRole.Outlet_Manager), StringComparison.Ordinal))
        {
            return Results.Json(Forbidden, AuthJsonContext.Default.ForbiddenResponse, statusCode: StatusCodes.Status403Forbidden);
        }

        return await next(context);
    }
}

/// <summary>
/// Endpoint filter that requires the user to have any authenticated role (Admin or Outlet_Manager).
/// Returns HTTP 403 if no role is present in the request context.
/// </summary>
public sealed class RequireAnyRoleFilter : IEndpointFilter
{
    private static readonly ForbiddenResponse Forbidden = new("Forbidden", "Insufficient permissions");

    public async ValueTask<object?> InvokeAsync(EndpointFilterInvocationContext context, EndpointFilterDelegate next)
    {
        var role = context.HttpContext.Items[AuthorizationMiddleware.ContextKeys.Role] as string;

        if (string.IsNullOrWhiteSpace(role))
        {
            return Results.Json(Forbidden, AuthJsonContext.Default.ForbiddenResponse, statusCode: StatusCodes.Status403Forbidden);
        }

        // Validate that the role is a known role
        if (!string.Equals(role, nameof(UserRole.Admin), StringComparison.Ordinal) &&
            !string.Equals(role, nameof(UserRole.Outlet_Manager), StringComparison.Ordinal))
        {
            return Results.Json(Forbidden, AuthJsonContext.Default.ForbiddenResponse, statusCode: StatusCodes.Status403Forbidden);
        }

        return await next(context);
    }
}
