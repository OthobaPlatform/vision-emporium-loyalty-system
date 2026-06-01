using System.Text.RegularExpressions;
using VELoyalty.Api.Services;
using VELoyalty.Auth;

namespace VELoyalty.Api.Endpoints;

public static class RedemptionEndpoints
{
    public static WebApplication MapRedemptionEndpoints(this WebApplication app)
    {
        // POST /api/v1/redemptions/verify
        app.MapPost("/api/v1/redemptions/verify", async (
            HttpContext httpContext,
            RedemptionService redemptionService,
            CancellationToken cancellationToken) =>
        {
            var input = await httpContext.Request.ReadFromJsonAsync<RedemptionCodeInput>(cancellationToken: cancellationToken);
            if (input is null || string.IsNullOrWhiteSpace(input.Code))
            {
                return Results.BadRequest(new { error = "ValidationError", message = "Verification code is required." });
            }

            if (!Regex.IsMatch(input.Code.Trim(), @"^\d{6}$"))
            {
                return Results.BadRequest(new { error = "ValidationError", message = "Code must be exactly 6 digits." });
            }

            var staffId = httpContext.GetUserId() ?? "staff";
            var outletId = httpContext.GetUserOutletId();

            var serviceRequest = new RedemptionVerifyRequest(input.Code.Trim(), outletId ?? "", staffId);
            var result = await redemptionService.VerifyAndRedeemAsync(serviceRequest, cancellationToken);

            if (!result.IsSuccess)
            {
                var statusCode = result.ErrorType switch
                {
                    "NotFound" => 404,
                    "Expired" => 400,
                    "AlreadyRedeemed" => 400,
                    "WrongOutlet" => 400,
                    "RateLimited" => 429,
                    _ => 400
                };
                return Results.Json(new { error = result.ErrorType, message = result.Message }, statusCode: statusCode);
            }

            return Results.Ok(new
            {
                message = "Gift redeemed successfully.",
                redemption = new
                {
                    code = input.Code.Trim(),
                    giftType = result.GiftType,
                    giftDescription = result.GiftDescription,
                    redeemedAt = result.RedeemedAt
                }
            });
        }).RequireAnyRole();

        // GET /api/v1/redemptions/search
        app.MapGet("/api/v1/redemptions/search", async (
            string? phone,
            string? code,
            HttpContext httpContext,
            CustomerService customerService,
            CancellationToken cancellationToken) =>
        {
            if (string.IsNullOrWhiteSpace(phone) && string.IsNullOrWhiteSpace(code))
            {
                return Results.BadRequest(new { error = "ValidationError", message = "Either 'phone' or 'code' query parameter is required." });
            }

            var results = await customerService.SearchRedemptionsAsync(phone, code, cancellationToken);

            if (httpContext.IsOutletManager())
            {
                var userOutletId = httpContext.GetUserOutletId();
                if (!string.IsNullOrWhiteSpace(userOutletId))
                {
                    results = results.Where(r => string.Equals(r.OutletId, userOutletId, StringComparison.Ordinal)).ToList();
                }
            }

            return Results.Ok(new { results });
        }).RequireAnyRole();

        return app;
    }
}

public record RedemptionCodeInput(string Code);
