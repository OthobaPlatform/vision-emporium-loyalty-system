using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Http.HttpResults;
using Microsoft.AspNetCore.Routing;
using Xunit;

namespace VELoyalty.Auth.Tests;

public class RequireAdminFilterTests
{
    [Fact]
    public async Task InvokeAsync_AdminRole_CallsNext()
    {
        // Arrange
        var context = CreateHttpContext("Admin");
        var filterContext = CreateFilterContext(context);
        var filter = new RequireAdminFilter();
        var nextCalled = false;

        // Act
        var result = await filter.InvokeAsync(filterContext, _ =>
        {
            nextCalled = true;
            return ValueTask.FromResult<object?>(Results.Ok());
        });

        // Assert
        Assert.True(nextCalled);
    }

    [Fact]
    public async Task InvokeAsync_OutletManagerRole_Returns403()
    {
        // Arrange
        var context = CreateHttpContext("Outlet_Manager", "outlet-123");
        var filterContext = CreateFilterContext(context);
        var filter = new RequireAdminFilter();

        // Act
        var result = await filter.InvokeAsync(filterContext, _ =>
            ValueTask.FromResult<object?>(Results.Ok()));

        // Assert
        Assert.NotNull(result);
        var jsonResult = Assert.IsType<JsonHttpResult<ForbiddenResponse>>(result);
        Assert.Equal(StatusCodes.Status403Forbidden, jsonResult.StatusCode);
    }

    [Fact]
    public async Task InvokeAsync_NoRole_Returns403()
    {
        // Arrange
        var context = new DefaultHttpContext();
        var filterContext = CreateFilterContext(context);
        var filter = new RequireAdminFilter();

        // Act
        var result = await filter.InvokeAsync(filterContext, _ =>
            ValueTask.FromResult<object?>(Results.Ok()));

        // Assert
        Assert.NotNull(result);
        var jsonResult = Assert.IsType<JsonHttpResult<ForbiddenResponse>>(result);
        Assert.Equal(StatusCodes.Status403Forbidden, jsonResult.StatusCode);
    }

    [Fact]
    public async Task InvokeAsync_UnknownRole_Returns403()
    {
        // Arrange
        var context = CreateHttpContext("SuperUser");
        var filterContext = CreateFilterContext(context);
        var filter = new RequireAdminFilter();

        // Act
        var result = await filter.InvokeAsync(filterContext, _ =>
            ValueTask.FromResult<object?>(Results.Ok()));

        // Assert
        Assert.NotNull(result);
        var jsonResult = Assert.IsType<JsonHttpResult<ForbiddenResponse>>(result);
        Assert.Equal(StatusCodes.Status403Forbidden, jsonResult.StatusCode);
    }

    private static DefaultHttpContext CreateHttpContext(string role, string? outletId = null)
    {
        var context = new DefaultHttpContext();
        context.Items[AuthorizationMiddleware.ContextKeys.UserId] = "user-1";
        context.Items[AuthorizationMiddleware.ContextKeys.Role] = role;
        if (outletId is not null)
            context.Items[AuthorizationMiddleware.ContextKeys.OutletId] = outletId;
        return context;
    }

    private static EndpointFilterInvocationContext CreateFilterContext(HttpContext httpContext)
    {
        return new DefaultEndpointFilterInvocationContext(httpContext);
    }
}

public class RequireOutletManagerFilterTests
{
    [Fact]
    public async Task InvokeAsync_OutletManagerRole_CallsNext()
    {
        // Arrange
        var context = CreateHttpContext("Outlet_Manager", "outlet-123");
        var filterContext = CreateFilterContext(context);
        var filter = new RequireOutletManagerFilter();
        var nextCalled = false;

        // Act
        var result = await filter.InvokeAsync(filterContext, _ =>
        {
            nextCalled = true;
            return ValueTask.FromResult<object?>(Results.Ok());
        });

        // Assert
        Assert.True(nextCalled);
    }

    [Fact]
    public async Task InvokeAsync_AdminRole_Returns403()
    {
        // Arrange
        var context = CreateHttpContext("Admin");
        var filterContext = CreateFilterContext(context);
        var filter = new RequireOutletManagerFilter();

        // Act
        var result = await filter.InvokeAsync(filterContext, _ =>
            ValueTask.FromResult<object?>(Results.Ok()));

        // Assert
        Assert.NotNull(result);
        var jsonResult = Assert.IsType<JsonHttpResult<ForbiddenResponse>>(result);
        Assert.Equal(StatusCodes.Status403Forbidden, jsonResult.StatusCode);
    }

    [Fact]
    public async Task InvokeAsync_NoRole_Returns403()
    {
        // Arrange
        var context = new DefaultHttpContext();
        var filterContext = CreateFilterContext(context);
        var filter = new RequireOutletManagerFilter();

        // Act
        var result = await filter.InvokeAsync(filterContext, _ =>
            ValueTask.FromResult<object?>(Results.Ok()));

        // Assert
        Assert.NotNull(result);
        var jsonResult = Assert.IsType<JsonHttpResult<ForbiddenResponse>>(result);
        Assert.Equal(StatusCodes.Status403Forbidden, jsonResult.StatusCode);
    }

    private static DefaultHttpContext CreateHttpContext(string role, string? outletId = null)
    {
        var context = new DefaultHttpContext();
        context.Items[AuthorizationMiddleware.ContextKeys.UserId] = "user-1";
        context.Items[AuthorizationMiddleware.ContextKeys.Role] = role;
        if (outletId is not null)
            context.Items[AuthorizationMiddleware.ContextKeys.OutletId] = outletId;
        return context;
    }

    private static EndpointFilterInvocationContext CreateFilterContext(HttpContext httpContext)
    {
        return new DefaultEndpointFilterInvocationContext(httpContext);
    }
}

public class RequireAnyRoleFilterTests
{
    [Theory]
    [InlineData("Admin")]
    [InlineData("Outlet_Manager")]
    public async Task InvokeAsync_ValidRole_CallsNext(string role)
    {
        // Arrange
        var context = CreateHttpContext(role);
        var filterContext = CreateFilterContext(context);
        var filter = new RequireAnyRoleFilter();
        var nextCalled = false;

        // Act
        var result = await filter.InvokeAsync(filterContext, _ =>
        {
            nextCalled = true;
            return ValueTask.FromResult<object?>(Results.Ok());
        });

        // Assert
        Assert.True(nextCalled);
    }

    [Fact]
    public async Task InvokeAsync_NoRole_Returns403()
    {
        // Arrange
        var context = new DefaultHttpContext();
        var filterContext = CreateFilterContext(context);
        var filter = new RequireAnyRoleFilter();

        // Act
        var result = await filter.InvokeAsync(filterContext, _ =>
            ValueTask.FromResult<object?>(Results.Ok()));

        // Assert
        Assert.NotNull(result);
        var jsonResult = Assert.IsType<JsonHttpResult<ForbiddenResponse>>(result);
        Assert.Equal(StatusCodes.Status403Forbidden, jsonResult.StatusCode);
    }

    [Fact]
    public async Task InvokeAsync_UnknownRole_Returns403()
    {
        // Arrange
        var context = CreateHttpContext("Viewer");
        var filterContext = CreateFilterContext(context);
        var filter = new RequireAnyRoleFilter();

        // Act
        var result = await filter.InvokeAsync(filterContext, _ =>
            ValueTask.FromResult<object?>(Results.Ok()));

        // Assert
        Assert.NotNull(result);
        var jsonResult = Assert.IsType<JsonHttpResult<ForbiddenResponse>>(result);
        Assert.Equal(StatusCodes.Status403Forbidden, jsonResult.StatusCode);
    }

    private static DefaultHttpContext CreateHttpContext(string role)
    {
        var context = new DefaultHttpContext();
        context.Items[AuthorizationMiddleware.ContextKeys.UserId] = "user-1";
        context.Items[AuthorizationMiddleware.ContextKeys.Role] = role;
        return context;
    }

    private static EndpointFilterInvocationContext CreateFilterContext(HttpContext httpContext)
    {
        return new DefaultEndpointFilterInvocationContext(httpContext);
    }
}
