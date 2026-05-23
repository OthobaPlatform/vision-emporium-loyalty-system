using VELoyalty.Core;
using VELoyalty.Data.Repositories;

namespace VELoyalty.Api.Services;

/// <summary>
/// Service handling redemption verification logic including code validation,
/// expiry checks, outlet binding, rate limiting, and successful redemption recording.
/// </summary>
public class RedemptionService
{
    private readonly VerificationCodeRepository _verificationCodeRepository;
    private readonly RedemptionRepository _redemptionRepository;
    private readonly RateLimitRepository _rateLimitRepository;
    private readonly OutletRepository _outletRepository;
    private readonly AuditRepository _auditRepository;

    public RedemptionService(
        VerificationCodeRepository verificationCodeRepository,
        RedemptionRepository redemptionRepository,
        RateLimitRepository rateLimitRepository,
        OutletRepository outletRepository,
        AuditRepository auditRepository)
    {
        _verificationCodeRepository = verificationCodeRepository;
        _redemptionRepository = redemptionRepository;
        _rateLimitRepository = rateLimitRepository;
        _outletRepository = outletRepository;
        _auditRepository = auditRepository;
    }

    /// <summary>
    /// Verifies and processes a redemption attempt.
    /// Validates code format, checks existence, expiry, outlet binding, rate limit, and redeemed status.
    /// On success, marks the code as redeemed, creates a redemption record, and logs an audit entry.
    /// </summary>
    /// <param name="request">The verification request containing code, outlet, and staff info.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>A result indicating success or the specific failure reason.</returns>
    public async Task<RedemptionVerificationResult> VerifyAndRedeemAsync(
        RedemptionVerifyRequest request,
        CancellationToken cancellationToken = default)
    {
        var utcNow = DateTime.UtcNow;

        // Step 1: Validate code format (must be exactly 6 digits numeric)
        if (!IsValidCodeFormat(request.Code))
        {
            return RedemptionVerificationResult.Invalid("Code must be exactly 6 digits numeric.");
        }

        // Step 2: Check rate limit before any lookups
        var isBlocked = await _rateLimitRepository.IsBlockedAsync(request.Code, utcNow, cancellationToken);
        if (isBlocked)
        {
            var blockedUntil = await _rateLimitRepository.GetBlockedUntilAsync(request.Code, utcNow, cancellationToken);
            return RedemptionVerificationResult.RateLimited(
                "Too many failed attempts. Please try again later.",
                blockedUntil ?? utcNow.AddMinutes(Constants.RateLimitBlockMinutes));
        }

        // Step 3: Look up code using GSI2
        var verificationCode = await _verificationCodeRepository.GetByCodeAsync(request.Code, cancellationToken);
        if (verificationCode is null)
        {
            // Record failed attempt for rate limiting
            await _rateLimitRepository.RecordFailedAttemptAsync(request.Code, utcNow, cancellationToken);
            return RedemptionVerificationResult.Invalid("The verification code is invalid.");
        }

        // Step 4: Check if code is expired
        if (verificationCode.ExpiresAt < utcNow)
        {
            // Record failed attempt for rate limiting
            await _rateLimitRepository.RecordFailedAttemptAsync(request.Code, utcNow, cancellationToken);
            return RedemptionVerificationResult.Expired(
                "The verification code has expired.",
                verificationCode.ExpiresAt);
        }

        // Step 5: Check if code is already redeemed
        if (string.Equals(verificationCode.Status, nameof(CodeStatus.Redeemed), StringComparison.OrdinalIgnoreCase))
        {
            // Look up the redemption record for the date
            var existingRedemption = await _redemptionRepository.GetRedemptionByCodeAsync(request.Code, cancellationToken);
            var redemptionDate = existingRedemption?.RedeemedAt ?? utcNow;

            // Record failed attempt for rate limiting
            await _rateLimitRepository.RecordFailedAttemptAsync(request.Code, utcNow, cancellationToken);
            return RedemptionVerificationResult.AlreadyRedeemed(
                "This gift has already been claimed.",
                redemptionDate);
        }

        // Step 6: Check outlet binding
        if (!string.Equals(verificationCode.OutletId, request.OutletId, StringComparison.OrdinalIgnoreCase))
        {
            // Get the correct outlet name for the error message
            var correctOutlet = await _outletRepository.GetByIdAsync(verificationCode.OutletId, cancellationToken);
            var correctOutletName = correctOutlet?.Name ?? verificationCode.OutletId;

            // Record failed attempt for rate limiting
            await _rateLimitRepository.RecordFailedAttemptAsync(request.Code, utcNow, cancellationToken);
            return RedemptionVerificationResult.WrongOutlet(
                $"This code can only be redeemed at {correctOutletName}.",
                correctOutletName);
        }

        // Step 6b: Check if the outlet is active (deactivated outlets cannot process redemptions)
        var redemptionOutlet = await _outletRepository.GetByIdAsync(request.OutletId, cancellationToken);
        if (redemptionOutlet is not null && !redemptionOutlet.IsActive)
        {
            return RedemptionVerificationResult.Invalid(
                "Redemptions are not available at this outlet. The outlet is currently inactive.");
        }

        // Step 7: All checks passed — perform redemption
        // Mark code as redeemed
        await _verificationCodeRepository.UpdateStatusAsync(
            verificationCode.CustomerId,
            GetCycleIdFromCode(verificationCode),
            verificationCode.Tier,
            nameof(CodeStatus.Redeemed),
            cancellationToken);

        // Create redemption record
        var redemption = new Redemption(
            Code: request.Code,
            CustomerId: verificationCode.CustomerId,
            OutletId: request.OutletId,
            StaffMemberId: request.StaffMemberId,
            GiftType: verificationCode.GiftType,
            RedeemedAt: utcNow);

        await _redemptionRepository.CreateRedemptionAsync(redemption, cancellationToken);

        // Create audit entry
        var auditEntry = new AuditEntry(
            EventType: nameof(AuditEventType.Redemption),
            ActorId: request.StaffMemberId,
            EntityType: "VerificationCode",
            EntityId: request.Code,
            Details: new Dictionary<string, string>
            {
                ["customerId"] = verificationCode.CustomerId,
                ["outletId"] = request.OutletId,
                ["giftType"] = verificationCode.GiftType,
                ["giftDescription"] = verificationCode.GiftDescription,
                ["giftValue"] = verificationCode.GiftValue.ToString("F2"),
                ["tier"] = verificationCode.Tier.ToString()
            },
            Timestamp: utcNow);

        await _auditRepository.AppendAsync(auditEntry, cancellationToken);

        return RedemptionVerificationResult.Success(
            verificationCode.CustomerId,
            verificationCode.GiftType,
            verificationCode.GiftDescription,
            verificationCode.GiftValue,
            utcNow);
    }

    /// <summary>
    /// Validates that the code is exactly 6 digits numeric.
    /// </summary>
    private static bool IsValidCodeFormat(string? code)
    {
        if (string.IsNullOrWhiteSpace(code))
            return false;

        if (code.Length != Constants.VerificationCodeLength)
            return false;

        return code.All(char.IsDigit);
    }

    /// <summary>
    /// Extracts the cycle ID from a verification code record.
    /// The cycle ID is embedded in the eligibility record's sort key pattern.
    /// We derive it from the IssuedAt date as a fallback.
    /// </summary>
    private static string GetCycleIdFromCode(VerificationCode code)
    {
        // The cycle ID follows the pattern based on the fiscal year
        // Default cycle: June 1 to next year May 31
        var issuedDate = code.IssuedAt;
        var year = issuedDate.Month >= 6 ? issuedDate.Year : issuedDate.Year - 1;
        return $"{year}-{year + 1}";
    }
}
