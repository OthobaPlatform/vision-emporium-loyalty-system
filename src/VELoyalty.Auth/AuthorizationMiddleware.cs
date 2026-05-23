using Microsoft.AspNetCore.Http;
using VELoyalty.Core;

namespace VELoyalty.Auth;

/// <summary>
/// ASP.NET Core middleware that extracts user identity from API Gateway context headers
/// or validates JWT directly from the Authorization header (for local development).
/// Sets HttpContext.Items with UserId, Role, and OutletId for downstream use.
/// </summary>
public sealed class AuthorizationMiddleware
{
    private readonly RequestDelegate _next;
    private readonly IJwtTokenService _jwtTokenService;

    /// <summary>
    /// Header names used by the API Gateway Custom Authorizer to pass user context.
    /// </summary>
    public static class Headers
    {
        public const string UserId = "x-user-id";
        public const string UserRole = "x-user-role";
        public const string OutletId = "x-outlet-id";
    }

    /// <summary>
    /// HttpContext.Items keys for accessing extracted user claims.
    /// </summary>
    public static class ContextKeys
    {
        public const string UserId = "UserId";
        public const string Role = "Role";
        public const string OutletId = "OutletId";
    }

    public AuthorizationMiddleware(RequestDelegate next, IJwtTokenService jwtTokenService)
    {
        _next = next;
        _jwtTokenService = jwtTokenService;
    }

    public async Task InvokeAsync(HttpContext context)
    {
        // Try extracting from API Gateway context headers first
        var userId = context.Request.Headers[Headers.UserId].FirstOrDefault();
        var role = context.Request.Headers[Headers.UserRole].FirstOrDefault();
        var outletId = context.Request.Headers[Headers.OutletId].FirstOrDefault();

        if (!string.IsNullOrWhiteSpace(userId) && !string.IsNullOrWhiteSpace(role))
        {
            // API Gateway has already validated the token and passed context
            context.Items[ContextKeys.UserId] = userId;
            context.Items[ContextKeys.Role] = role;
            if (!string.IsNullOrWhiteSpace(outletId))
            {
                context.Items[ContextKeys.OutletId] = outletId;
            }
        }
        else
        {
            // Fallback: validate JWT directly from Authorization header (local development)
            var authHeader = context.Request.Headers.Authorization.FirstOrDefault();
            if (!string.IsNullOrWhiteSpace(authHeader) && authHeader.StartsWith("Bearer ", StringComparison.OrdinalIgnoreCase))
            {
                var token = authHeader["Bearer ".Length..].Trim();
                var result = _jwtTokenService.ValidateToken(token);

                if (result.IsValid)
                {
                    context.Items[ContextKeys.UserId] = result.UserId;
                    context.Items[ContextKeys.Role] = result.Role;
                    if (!string.IsNullOrWhiteSpace(result.OutletId))
                    {
                        context.Items[ContextKeys.OutletId] = result.OutletId;
                    }
                }
            }
        }

        await _next(context);
    }
}
