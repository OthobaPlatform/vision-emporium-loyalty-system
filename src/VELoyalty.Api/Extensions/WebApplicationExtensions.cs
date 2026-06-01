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
    /// Maps all VELoyalty API endpoint groups.
    /// </summary>
    public static WebApplication MapVELoyaltyEndpoints(this WebApplication app)
    {
        app.MapGet("/api/v1/health", () => Results.Ok(new { Status = "Healthy", Service = "VELoyalty.Api" }));

        app.MapAuthEndpoints();
        app.MapRedemptionEndpoints();
        app.MapCustomerEndpoints();
        app.MapOutletEndpoints();
        app.MapUserEndpoints();
        app.MapConfigurationEndpoints();
        app.MapDashboardEndpoints();
        app.MapIngestionEndpoints();

        return app;
    }
}
