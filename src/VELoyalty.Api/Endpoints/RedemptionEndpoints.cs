using System.Text.RegularExpressions;
using Asp.Versioning;
using VELoyalty.Api.Services;
using VELoyalty.Auth;
using VELoyalty.Data.Repositories;

namespace VELoyalty.Api.Endpoints;

public static class RedemptionEndpoints
{
    public static RouteGroupBuilder MapRedemptionEndpoints(this RouteGroupBuilder group)
    {
        // GET /verification-codes
        group.MapGet("/verification-codes", async (
            string? status,
            VerificationCodeRepository verificationCodeRepository,
            CustomerRepository customerRepository,
            OutletRepository outletRepository,
            CancellationToken cancellationToken) =>
        {
            // Validate status filter if provided
            if (!string.IsNullOrWhiteSpace(status) &&
                status != "Active" && status != "Redeemed" && status != "Expired")
            {
                return Results.BadRequest(new { error = "ValidationError", message = "Status must be 'Active', 'Redeemed', or 'Expired'." });
            }

            var codes = await verificationCodeRepository.ListAllCodesAsync(status, cancellationToken);

            // Enrich codes with customer phone and outlet name
            var results = new List<object>();
            foreach (var code in codes)
            {
                var customer = await customerRepository.GetByIdAsync(code.CustomerId, cancellationToken);
                var outlet = await outletRepository.GetByIdAsync(code.OutletId, cancellationToken);

                // Determine effective status (Active codes past expiry are Expired)
                var effectiveStatus = code.Status;
                if (code.Status == "Active" && DateTime.UtcNow > code.ExpiresAt)
                    effectiveStatus = "Expired";

                results.Add(new
                {
                    code = code.Code,
                    customerId = code.CustomerId,
                    customerPhone = customer?.PhoneNumber ?? "Unknown",
                    outletId = code.OutletId,
                    outletName = outlet?.Name ?? code.OutletId,
                    tier = code.Tier,
                    giftType = code.GiftType,
                    giftDescription = code.GiftDescription,
                    giftValue = code.GiftValue,
                    status = effectiveStatus,
                    issuedAt = code.IssuedAt,
                    expiresAt = code.ExpiresAt
                });
            }

            return Results.Ok(new { codes = results });
        }).RequireAdmin().MapToApiVersion(1, 0);

        // POST /redemptions/verify
        group.MapPost("/redemptions/verify", async (
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
        }).RequireAnyRole().MapToApiVersion(1, 0);

        // GET /redemptions/search
        group.MapGet("/redemptions/search", async (
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
        }).RequireAnyRole().MapToApiVersion(1, 0);

        return group;
    }
}

public record RedemptionCodeInput(string Code);
