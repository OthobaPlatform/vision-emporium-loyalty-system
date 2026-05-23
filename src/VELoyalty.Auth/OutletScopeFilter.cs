using Microsoft.AspNetCore.Http;
using VELoyalty.Core;

namespace VELoyalty.Auth;

/// <summary>
/// Endpoint filter that enforces outlet-scoped data access for Outlet_Manager users.
/// Validates that the requested outletId (from route or query) matches the user's assigned outletId.
/// Admin users bypass this filter. Returns HTTP 403 if an Outlet_Manager attempts to access
/// another outlet's data.
/// </summary>
public sealed class OutletScopeFilter : IEndpointFilter
{
    private static readonly ForbiddenResponse Forbidden = new("Forbidden", "Insufficient permissions");

    private readonly string _outletIdParameterName;

    /// <summary>
    /// Creates a new OutletScopeFilter.
    /// </summary>
    /// <param name="outletIdParameterName">
    /// The name of the route or query parameter containing the outlet ID to validate.
    /// Defaults to "outletId".
    /// </param>
    public OutletScopeFilter(string outletIdParameterName = "outletId")
    {
        _outletIdParameterName = outletIdParameterName;
    }

    public async ValueTask<object?> InvokeAsync(EndpointFilterInvocationContext context, EndpointFilterDelegate next)
    {
        var role = context.HttpContext.Items[AuthorizationMiddleware.ContextKeys.Role] as string;

        // Admin users have unrestricted access — bypass outlet scope check
        if (string.Equals(role, nameof(UserRole.Admin), StringComparison.Ordinal))
        {
            return await next(context);
        }

        // For Outlet_Manager, enforce outlet scope
        if (string.Equals(role, nameof(UserRole.Outlet_Manager), StringComparison.Ordinal))
        {
            var userOutletId = context.HttpContext.Items[AuthorizationMiddleware.ContextKeys.OutletId] as string;

            if (string.IsNullOrWhiteSpace(userOutletId))
            {
                return Results.Json(Forbidden, AuthJsonContext.Default.ForbiddenResponse, statusCode: StatusCodes.Status403Forbidden);
            }

            // Try to get the requested outlet ID from route values first, then query string
            var requestedOutletId = GetRequestedOutletId(context.HttpContext);

            if (!string.IsNullOrWhiteSpace(requestedOutletId) &&
                !string.Equals(requestedOutletId, userOutletId, StringComparison.Ordinal))
            {
                return Results.Json(Forbidden, AuthJsonContext.Default.ForbiddenResponse, statusCode: StatusCodes.Status403Forbidden);
            }
        }

        return await next(context);
    }

    private string? GetRequestedOutletId(HttpContext context)
    {
        // Check route values
        if (context.Request.RouteValues.TryGetValue(_outletIdParameterName, out var routeValue) &&
            routeValue is string routeOutletId &&
            !string.IsNullOrWhiteSpace(routeOutletId))
        {
            return routeOutletId;
        }

        // Check query string
        if (context.Request.Query.TryGetValue(_outletIdParameterName, out var queryValue) &&
            !string.IsNullOrWhiteSpace(queryValue.FirstOrDefault()))
        {
            return queryValue.FirstOrDefault();
        }

        return null;
    }
}
