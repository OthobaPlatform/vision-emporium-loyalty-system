namespace VELoyalty.Api.Services;

/// <summary>
/// Request model for the redemption verification endpoint.
/// </summary>
/// <param name="Code">The 6-digit numeric verification code.</param>
/// <param name="OutletId">The outlet where the redemption is being attempted.</param>
/// <param name="StaffMemberId">The staff member processing the redemption.</param>
public record RedemptionVerifyRequest(
    string Code,
    string OutletId,
    string StaffMemberId
);

/// <summary>
/// Result of a redemption verification attempt.
/// </summary>
public class RedemptionVerificationResult
{
    public bool IsSuccess { get; private init; }
    public string? ErrorType { get; private init; }
    public string? Message { get; private init; }
    public string? CustomerId { get; private init; }
    public string? GiftType { get; private init; }
    public string? GiftDescription { get; private init; }
    public decimal? GiftValue { get; private init; }
    public DateTime? RedeemedAt { get; private init; }
    public DateTime? ExpiresAt { get; private init; }
    public DateTime? RedemptionDate { get; private init; }
    public string? CorrectOutletName { get; private init; }
    public DateTime? RetryAfter { get; private init; }

    public static RedemptionVerificationResult Success(
        string customerId, string giftType, string giftDescription, decimal giftValue, DateTime redeemedAt) =>
        new()
        {
            IsSuccess = true,
            CustomerId = customerId,
            GiftType = giftType,
            GiftDescription = giftDescription,
            GiftValue = giftValue,
            RedeemedAt = redeemedAt,
            Message = "Gift redeemed successfully."
        };

    public static RedemptionVerificationResult Invalid(string message) =>
        new()
        {
            IsSuccess = false,
            ErrorType = "InvalidCode",
            Message = message
        };

    public static RedemptionVerificationResult Expired(string message, DateTime expiresAt) =>
        new()
        {
            IsSuccess = false,
            ErrorType = "Expired",
            Message = message,
            ExpiresAt = expiresAt
        };

    public static RedemptionVerificationResult AlreadyRedeemed(string message, DateTime redemptionDate) =>
        new()
        {
            IsSuccess = false,
            ErrorType = "AlreadyRedeemed",
            Message = message,
            RedemptionDate = redemptionDate
        };

    public static RedemptionVerificationResult WrongOutlet(string message, string correctOutletName) =>
        new()
        {
            IsSuccess = false,
            ErrorType = "WrongOutlet",
            Message = message,
            CorrectOutletName = correctOutletName
        };

    public static RedemptionVerificationResult RateLimited(string message, DateTime retryAfter) =>
        new()
        {
            IsSuccess = false,
            ErrorType = "RateLimited",
            Message = message,
            RetryAfter = retryAfter
        };
}
