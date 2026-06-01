using Asp.Versioning;
using VELoyalty.Api.Services;
using VELoyalty.Auth;

namespace VELoyalty.Api.Endpoints;

public static class CustomerEndpoints
{
    public static RouteGroupBuilder MapCustomerEndpoints(this RouteGroupBuilder group)
    {
        // GET /customers
        group.MapGet("/customers", async (
            string? search,
            CustomerService customerService,
            CancellationToken cancellationToken) =>
        {
            var customers = await customerService.ListAllCustomersAsync(search, cancellationToken);
            return Results.Ok(customers);
        }).RequireAnyRole().MapToApiVersion(1, 0);

        // GET /customers/{phone}
        group.MapGet("/customers/{phone}", async (
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
        }).RequireAnyRole().MapToApiVersion(1, 0);

        // GET /customers/{phone}/codes
        group.MapGet("/customers/{phone}/codes", async (
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
        }).RequireAnyRole().MapToApiVersion(1, 0);

        return group;
    }
}
