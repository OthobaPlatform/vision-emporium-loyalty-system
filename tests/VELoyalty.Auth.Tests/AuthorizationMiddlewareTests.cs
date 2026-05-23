using Microsoft.AspNetCore.Http;
using Xunit;

namespace VELoyalty.Auth.Tests;

public class AuthorizationMiddlewareTests
{
    private const string TestSecret = "test-secret-key-that-is-long-enough-for-hmac-sha256";
    private readonly IJwtTokenService _jwtTokenService = new JwtTokenService(TestSecret);

    private AuthorizationMiddleware CreateMiddleware(RequestDelegate next)
    {
        return new AuthorizationMiddleware(next, _jwtTokenService);
    }

    [Fact]
    public async Task InvokeAsync_WithApiGatewayHeaders_SetsContextItems()
    {
        // Arrange
        var context = new DefaultHttpContext();
        context.Request.Headers[AuthorizationMiddleware.Headers.UserId] = "user-123";
        context.Request.Headers[AuthorizationMiddleware.Headers.UserRole] = "Admin";
        context.Request.Headers[AuthorizationMiddleware.Headers.OutletId] = "outlet-456";

        var nextCalled = false;
        var middleware = CreateMiddleware(_ => { nextCalled = true; return Task.CompletedTask; });

        // Act
        await middleware.InvokeAsync(context);

        // Assert
        Assert.True(nextCalled);
        Assert.Equal("user-123", context.Items[AuthorizationMiddleware.ContextKeys.UserId]);
        Assert.Equal("Admin", context.Items[AuthorizationMiddleware.ContextKeys.Role]);
        Assert.Equal("outlet-456", context.Items[AuthorizationMiddleware.ContextKeys.OutletId]);
    }

    [Fact]
    public async Task InvokeAsync_WithApiGatewayHeaders_NoOutletId_SetsUserIdAndRole()
    {
        // Arrange
        var context = new DefaultHttpContext();
        context.Request.Headers[AuthorizationMiddleware.Headers.UserId] = "user-123";
        context.Request.Headers[AuthorizationMiddleware.Headers.UserRole] = "Admin";

        var middleware = CreateMiddleware(_ => Task.CompletedTask);

        // Act
        await middleware.InvokeAsync(context);

        // Assert
        Assert.Equal("user-123", context.Items[AuthorizationMiddleware.ContextKeys.UserId]);
        Assert.Equal("Admin", context.Items[AuthorizationMiddleware.ContextKeys.Role]);
        Assert.False(context.Items.ContainsKey(AuthorizationMiddleware.ContextKeys.OutletId));
    }

    [Fact]
    public async Task InvokeAsync_WithValidJwtBearer_SetsContextItems()
    {
        // Arrange
        var token = _jwtTokenService.GenerateToken("user-789", "Outlet_Manager", "outlet-abc");
        var context = new DefaultHttpContext();
        context.Request.Headers.Authorization = $"Bearer {token.Token}";

        var middleware = CreateMiddleware(_ => Task.CompletedTask);

        // Act
        await middleware.InvokeAsync(context);

        // Assert
        Assert.Equal("user-789", context.Items[AuthorizationMiddleware.ContextKeys.UserId]);
        Assert.Equal("Outlet_Manager", context.Items[AuthorizationMiddleware.ContextKeys.Role]);
        Assert.Equal("outlet-abc", context.Items[AuthorizationMiddleware.ContextKeys.OutletId]);
    }

    [Fact]
    public async Task InvokeAsync_WithInvalidJwt_DoesNotSetContextItems()
    {
        // Arrange
        var context = new DefaultHttpContext();
        context.Request.Headers.Authorization = "Bearer invalid.token.here";

        var middleware = CreateMiddleware(_ => Task.CompletedTask);

        // Act
        await middleware.InvokeAsync(context);

        // Assert
        Assert.False(context.Items.ContainsKey(AuthorizationMiddleware.ContextKeys.UserId));
        Assert.False(context.Items.ContainsKey(AuthorizationMiddleware.ContextKeys.Role));
    }

    [Fact]
    public async Task InvokeAsync_WithNoAuthHeaders_DoesNotSetContextItems()
    {
        // Arrange
        var context = new DefaultHttpContext();
        var middleware = CreateMiddleware(_ => Task.CompletedTask);

        // Act
        await middleware.InvokeAsync(context);

        // Assert
        Assert.False(context.Items.ContainsKey(AuthorizationMiddleware.ContextKeys.UserId));
        Assert.False(context.Items.ContainsKey(AuthorizationMiddleware.ContextKeys.Role));
    }

    [Fact]
    public async Task InvokeAsync_ApiGatewayHeaders_TakePrecedenceOverJwt()
    {
        // Arrange: both API Gateway headers and JWT are present
        var token = _jwtTokenService.GenerateToken("jwt-user", "Outlet_Manager", "jwt-outlet");
        var context = new DefaultHttpContext();
        context.Request.Headers[AuthorizationMiddleware.Headers.UserId] = "gateway-user";
        context.Request.Headers[AuthorizationMiddleware.Headers.UserRole] = "Admin";
        context.Request.Headers.Authorization = $"Bearer {token.Token}";

        var middleware = CreateMiddleware(_ => Task.CompletedTask);

        // Act
        await middleware.InvokeAsync(context);

        // Assert: API Gateway headers take precedence
        Assert.Equal("gateway-user", context.Items[AuthorizationMiddleware.ContextKeys.UserId]);
        Assert.Equal("Admin", context.Items[AuthorizationMiddleware.ContextKeys.Role]);
    }

    [Fact]
    public async Task InvokeAsync_AlwaysCallsNext()
    {
        // Arrange: even with no auth, middleware should call next (filters handle 403)
        var context = new DefaultHttpContext();
        var nextCalled = false;
        var middleware = CreateMiddleware(_ => { nextCalled = true; return Task.CompletedTask; });

        // Act
        await middleware.InvokeAsync(context);

        // Assert
        Assert.True(nextCalled);
    }
}
