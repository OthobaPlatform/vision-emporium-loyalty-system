using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Routing;
using Microsoft.Extensions.DependencyInjection;

namespace VELoyalty.Auth;

/// <summary>
/// Extension methods for registering and applying authorization middleware and filters.
/// </summary>
public static class AuthorizationExtensions
{
    /// <summary>
    /// Adds the VELoyalty authorization middleware to the application pipeline.
    /// This middleware extracts user identity from API Gateway context headers
    /// or validates JWT from the Authorization header for local development.
    /// </summary>
    public static IApplicationBuilder UseVELoyaltyAuthorization(this IApplicationBuilder app)
    {
        return app.UseMiddleware<AuthorizationMiddleware>();
    }

    /// <summary>
    /// Registers the IJwtTokenService in the DI container for use by the authorization middleware.
    /// </summary>
    /// <param name="services">The service collection.</param>
    /// <param name="jwtSecret">The HMAC-SHA256 signing secret.</param>
    /// <param name="expiryHours">Token expiry in hours (default: 8).</param>
    public static IServiceCollection AddVELoyaltyAuth(this IServiceCollection services, string jwtSecret, int expiryHours = 8)
    {
        services.AddSingleton<IJwtTokenService>(new JwtTokenService(jwtSecret, expiryHours));
        return services;
    }

    /// <summary>
    /// Applies the RequireAdmin filter to an endpoint, restricting access to Admin role only.
    /// Returns HTTP 403 with { error: "Forbidden", message: "Insufficient permissions" } if unauthorized.
    /// </summary>
    public static RouteHandlerBuilder RequireAdmin(this RouteHandlerBuilder builder)
    {
        return builder.AddEndpointFilter<RequireAdminFilter>();
    }

    /// <summary>
    /// Applies the RequireOutletManager filter to an endpoint, restricting access to Outlet_Manager role only.
    /// Returns HTTP 403 with { error: "Forbidden", message: "Insufficient permissions" } if unauthorized.
    /// </summary>
    public static RouteHandlerBuilder RequireOutletManager(this RouteHandlerBuilder builder)
    {
        return builder.AddEndpointFilter<RequireOutletManagerFilter>();
    }

    /// <summary>
    /// Applies the RequireAnyRole filter to an endpoint, requiring any authenticated role.
    /// Returns HTTP 403 with { error: "Forbidden", message: "Insufficient permissions" } if no valid role.
    /// </summary>
    public static RouteHandlerBuilder RequireAnyRole(this RouteHandlerBuilder builder)
    {
        return builder.AddEndpointFilter<RequireAnyRoleFilter>();
    }

    /// <summary>
    /// Applies the OutletScopeFilter to an endpoint, enforcing outlet-scoped data access
    /// for Outlet_Manager users. Admin users bypass this filter.
    /// </summary>
    /// <param name="builder">The route handler builder.</param>
    /// <param name="outletIdParameterName">
    /// The name of the route or query parameter containing the outlet ID. Defaults to "outletId".
    /// </param>
    public static RouteHandlerBuilder RequireOutletScope(this RouteHandlerBuilder builder, string outletIdParameterName = "outletId")
    {
        return builder.AddEndpointFilter(new OutletScopeFilter(outletIdParameterName));
    }

    /// <summary>
    /// Applies the RequireAdmin filter to a route group, restricting all endpoints in the group to Admin role.
    /// </summary>
    public static RouteGroupBuilder RequireAdmin(this RouteGroupBuilder group)
    {
        group.AddEndpointFilter<RequireAdminFilter>();
        return group;
    }

    /// <summary>
    /// Applies the RequireOutletManager filter to a route group, restricting all endpoints to Outlet_Manager role.
    /// </summary>
    public static RouteGroupBuilder RequireOutletManager(this RouteGroupBuilder group)
    {
        group.AddEndpointFilter<RequireOutletManagerFilter>();
        return group;
    }

    /// <summary>
    /// Applies the RequireAnyRole filter to a route group, requiring any authenticated role.
    /// </summary>
    public static RouteGroupBuilder RequireAnyRole(this RouteGroupBuilder group)
    {
        group.AddEndpointFilter<RequireAnyRoleFilter>();
        return group;
    }

    /// <summary>
    /// Applies the OutletScopeFilter to a route group, enforcing outlet-scoped data access
    /// for Outlet_Manager users across all endpoints in the group.
    /// </summary>
    public static RouteGroupBuilder RequireOutletScope(this RouteGroupBuilder group, string outletIdParameterName = "outletId")
    {
        group.AddEndpointFilter(new OutletScopeFilter(outletIdParameterName));
        return group;
    }

    /// <summary>
    /// Gets the authenticated user's ID from the HttpContext.
    /// </summary>
    public static string? GetUserId(this HttpContext context)
    {
        return context.Items[AuthorizationMiddleware.ContextKeys.UserId] as string;
    }

    /// <summary>
    /// Gets the authenticated user's role from the HttpContext.
    /// </summary>
    public static string? GetUserRole(this HttpContext context)
    {
        return context.Items[AuthorizationMiddleware.ContextKeys.Role] as string;
    }

    /// <summary>
    /// Gets the authenticated user's assigned outlet ID from the HttpContext.
    /// </summary>
    public static string? GetUserOutletId(this HttpContext context)
    {
        return context.Items[AuthorizationMiddleware.ContextKeys.OutletId] as string;
    }

    /// <summary>
    /// Returns true if the current user has the Admin role.
    /// </summary>
    public static bool IsAdmin(this HttpContext context)
    {
        return string.Equals(context.GetUserRole(), nameof(Core.UserRole.Admin), StringComparison.Ordinal);
    }

    /// <summary>
    /// Returns true if the current user has the Outlet_Manager role.
    /// </summary>
    public static bool IsOutletManager(this HttpContext context)
    {
        return string.Equals(context.GetUserRole(), nameof(Core.UserRole.Outlet_Manager), StringComparison.Ordinal);
    }
}
