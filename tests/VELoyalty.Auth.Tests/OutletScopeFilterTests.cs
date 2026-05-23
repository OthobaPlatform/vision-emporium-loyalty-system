using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Http.HttpResults;
using Microsoft.AspNetCore.Routing;
using Xunit;

namespace VELoyalty.Auth.Tests;

public class OutletScopeFilterTests
{
    [Fact]
    public async Task InvokeAsync_AdminRole_BypassesOutletCheck()
    {
        // Arrange: Admin accessing any outlet data should pass
        var context = CreateHttpContext("Admin", outletId: null);
        context.Request.RouteValues["outletId"] = "any-outlet";
        var filterContext = CreateFilterContext(context);
        var filter = new OutletScopeFilter();
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
    public async Task InvokeAsync_OutletManager_MatchingOutlet_CallsNext()
    {
        // Arrange: Outlet_Manager accessing their own outlet data
        var context = CreateHttpContext("Outlet_Manager", outletId: "outlet-123");
        context.Request.RouteValues["outletId"] = "outlet-123";
        var filterContext = CreateFilterContext(context);
        var filter = new OutletScopeFilter();
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
    public async Task InvokeAsync_OutletManager_DifferentOutlet_Returns403()
    {
        // Arrange: Outlet_Manager trying to access another outlet's data
        var context = CreateHttpContext("Outlet_Manager", outletId: "outlet-123");
        context.Request.RouteValues["outletId"] = "outlet-456";
        var filterContext = CreateFilterContext(context);
        var filter = new OutletScopeFilter();

        // Act
        var result = await filter.InvokeAsync(filterContext, _ =>
            ValueTask.FromResult<object?>(Results.Ok()));

        // Assert
        Assert.NotNull(result);
        var jsonResult = Assert.IsType<JsonHttpResult<ForbiddenResponse>>(result);
        Assert.Equal(StatusCodes.Status403Forbidden, jsonResult.StatusCode);
    }

    [Fact]
    public async Task InvokeAsync_OutletManager_NoOutletIdInRequest_CallsNext()
    {
        // Arrange: No outlet ID in the request (e.g., listing own data)
        var context = CreateHttpContext("Outlet_Manager", outletId: "outlet-123");
        var filterContext = CreateFilterContext(context);
        var filter = new OutletScopeFilter();
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
    public async Task InvokeAsync_OutletManager_NoAssignedOutlet_Returns403()
    {
        // Arrange: Outlet_Manager with no assigned outlet trying to access outlet data
        var context = CreateHttpContext("Outlet_Manager", outletId: null);
        context.Request.RouteValues["outletId"] = "outlet-123";
        var filterContext = CreateFilterContext(context);
        var filter = new OutletScopeFilter();

        // Act
        var result = await filter.InvokeAsync(filterContext, _ =>
            ValueTask.FromResult<object?>(Results.Ok()));

        // Assert
        Assert.NotNull(result);
        var jsonResult = Assert.IsType<JsonHttpResult<ForbiddenResponse>>(result);
        Assert.Equal(StatusCodes.Status403Forbidden, jsonResult.StatusCode);
    }

    [Fact]
    public async Task InvokeAsync_OutletManager_OutletIdFromQueryString_MatchingOutlet_CallsNext()
    {
        // Arrange: Outlet ID comes from query string instead of route
        var context = CreateHttpContext("Outlet_Manager", outletId: "outlet-123");
        context.Request.QueryString = new QueryString("?outletId=outlet-123");
        var filterContext = CreateFilterContext(context);
        var filter = new OutletScopeFilter();
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
    public async Task InvokeAsync_OutletManager_OutletIdFromQueryString_DifferentOutlet_Returns403()
    {
        // Arrange: Outlet ID from query string doesn't match assigned outlet
        var context = CreateHttpContext("Outlet_Manager", outletId: "outlet-123");
        context.Request.QueryString = new QueryString("?outletId=outlet-456");
        var filterContext = CreateFilterContext(context);
        var filter = new OutletScopeFilter();

        // Act
        var result = await filter.InvokeAsync(filterContext, _ =>
            ValueTask.FromResult<object?>(Results.Ok()));

        // Assert
        Assert.NotNull(result);
        var jsonResult = Assert.IsType<JsonHttpResult<ForbiddenResponse>>(result);
        Assert.Equal(StatusCodes.Status403Forbidden, jsonResult.StatusCode);
    }

    [Fact]
    public async Task InvokeAsync_CustomParameterName_UsesCorrectParameter()
    {
        // Arrange: Using a custom parameter name for outlet ID
        var context = CreateHttpContext("Outlet_Manager", outletId: "outlet-123");
        context.Request.RouteValues["storeId"] = "outlet-123";
        var filterContext = CreateFilterContext(context);
        var filter = new OutletScopeFilter("storeId");
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

    private static DefaultHttpContext CreateHttpContext(string role, string? outletId)
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
