using VELoyalty.Api.Services;
using VELoyalty.Auth;

namespace VELoyalty.Api.Endpoints;

public static class CustomerEndpoints
{
    public static WebApplication MapCustomerEndpoints(this WebApplication app)
    {
        // GET /api/v1/customers/{phone}
        app.MapGet("/api/v1/customers/{phone}", async (
            string phone,
            CustomerService customerService,
            CancellationToken cancellationToken) =>
        {
            var profile = await customerService.GetCustomerProfileAsync(phone, cancellationToken);

            if (profile is null)
            {
                return Results.NotFound(new { error = "NotFound", message = "No customer found with the specified phone number." });
            }

            return Results.Ok(profile);
        }).RequireAnyRole();

        // GET /api/v1/customers/{phone}/codes
        app.MapGet("/api/v1/customers/{phone}/codes", async (
            string phone,
            CustomerService customerService,
            CancellationToken cancellationToken) =>
        {
            var codesResponse = await customerService.GetCustomerCodesAsync(phone, cancellationToken);

            if (codesResponse is null)
            {
                return Results.NotFound(new { error = "NotFound", message = "No customer found with the specified phone number." });
            }

            return Results.Ok(codesResponse);
        }).RequireAnyRole();

        return app;
    }
}
