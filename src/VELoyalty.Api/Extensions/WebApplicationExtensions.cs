using Asp.Versioning;
using Asp.Versioning.Builder;
using VELoyalty.Api.Endpoints;
using VELoyalty.Auth;

namespace VELoyalty.Api.Extensions;

public static class WebApplicationExtensions
{
    /// <summary>
    /// Configures CORS and VELoyalty authorization middleware.
    /// </summary>
    public static WebApplication UseVELoyaltyMiddleware(this WebApplication app)
    {
        app.UseCors();
        app.UseVELoyaltyAuthorization();
        return app;
    }

    /// <summary>
    /// Maps all VELoyalty API endpoint groups using versioned route groups.
    /// </summary>
    public static WebApplication MapVELoyaltyEndpoints(this WebApplication app)
    {
        var versionSet = app.NewApiVersionSet()
            .HasApiVersion(new ApiVersion(1, 0))
            .ReportApiVersions()
            .Build();

        var v1 = app.MapGroup("api/v{version:apiVersion}")
            .WithApiVersionSet(versionSet);

        v1.MapHealthEndpoints();
        v1.MapAuthEndpoints();
        v1.MapRedemptionEndpoints();
        v1.MapCustomerEndpoints();
        v1.MapOutletEndpoints();
        v1.MapUserEndpoints();
        v1.MapConfigurationEndpoints();
        v1.MapDashboardEndpoints();
        v1.MapIngestionEndpoints();

        return app;
    }

    private static RouteGroupBuilder MapHealthEndpoints(this RouteGroupBuilder group)
    {
        group.MapGet("/health", () => Results.Ok(new { Status = "Healthy", Service = "VELoyalty.Api" }))
            .MapToApiVersion(1, 0);

        return group;
    }
}
