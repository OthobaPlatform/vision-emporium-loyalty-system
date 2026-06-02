using Microsoft.Extensions.Logging;
using VELoyalty.Core;
using VELoyalty.Data.Repositories;

namespace VELoyalty.Api.Services;

/// <summary>
/// Result of evaluating a single threshold eligibility for a customer.
/// </summary>
public record EligibilityResult(
    int Tier,
    string GiftType,
    string GiftDescription,
    decimal GiftValue,
    string VerificationCode
);

/// <summary>
/// Service that evaluates customer eligibility against configured purchase thresholds.
/// When a new threshold is reached, generates an OTP, stores a verification code, and triggers SMS.
/// </summary>
public class EligibilityService
{
    private readonly ConfigRepository _configRepository;
    private readonly EligibilityRepository _eligibilityRepository;
    private readonly VerificationCodeRepository _verificationCodeRepository;
    private readonly OutletRepository _outletRepository;
    private readonly SmsService _smsService;
    private readonly ILogger<EligibilityService> _logger;

    public EligibilityService(
        ConfigRepository configRepository,
        EligibilityRepository eligibilityRepository,
        VerificationCodeRepository verificationCodeRepository,
        OutletRepository outletRepository,
        SmsService smsService,
        ILogger<EligibilityService> logger)
    {
        _configRepository = configRepository;
        _eligibilityRepository = eligibilityRepository;
        _verificationCodeRepository = verificationCodeRepository;
        _outletRepository = outletRepository;
        _smsService = smsService;
        _logger = logger;
    }

    /// <summary>
    /// Evaluates whether a customer has newly reached any purchase thresholds.
    /// For each newly reached threshold: generates OTP, stores verification code, sends SMS.
    /// Also sends a progress SMS if the customer is 1 purchase away from the next threshold.
    /// </summary>
    /// <param name="customerId">The customer identifier.</param>
    /// <param name="customerName">The customer's display name.</param>
    /// <param name="phone">The customer's phone number.</param>
    /// <param name="qualifyingPurchases">Current total qualifying purchases for this customer.</param>
    /// <param name="outletId">The outlet of the customer's last purchase (used for code binding).</param>
    /// <param name="ct">Cancellation token.</param>
    /// <returns>List of newly eligible thresholds with their verification codes.</returns>
    public async Task<List<EligibilityResult>> EvaluateEligibilityAsync(
        string customerId,
        string customerName,
        string phone,
        int qualifyingPurchases,
        string outletId,
        CancellationToken ct = default)
    {
        var results = new List<EligibilityResult>();
        var cycleId = VELoyalty.Core.Constants.GetCurrentCycleId();

        // Get all configured thresholds
        var thresholds = await _configRepository.GetAllThresholdConfigsAsync(ct);
        if (thresholds.Count == 0)
        {
            _logger.LogDebug("No thresholds configured, skipping eligibility evaluation for {CustomerId}", customerId);
            return results;
        }

        // Get the outlet name for SMS messages
        var outlet = await _outletRepository.GetByIdAsync(outletId, ct);
        var outletName = outlet?.Name ?? outletId;

        // Check each enabled threshold
        foreach (var threshold in thresholds.Where(t => t.IsEnabled))
        {
            // Has the customer reached this threshold?
            if (qualifyingPurchases < threshold.RequiredPurchases)
                continue;

            // Has eligibility already been recorded for this customer+cycle+tier?
            var alreadyEligible = await _eligibilityRepository.ExistsAsync(customerId, cycleId, threshold.Tier, ct);
            if (alreadyEligible)
                continue;

            // NEW threshold reached — generate OTP and store verification code
            var otp = await _verificationCodeRepository.GenerateUniqueCodeAsync(ct);

            var verificationCode = new VerificationCode(
                Code: otp,
                CustomerId: customerId,
                OutletId: outletId,
                Tier: threshold.Tier,
                GiftType: threshold.GiftType,
                GiftDescription: threshold.GiftDescription,
                GiftValue: threshold.GiftValue,
                IssuedAt: DateTime.UtcNow,
                ExpiresAt: DateTime.UtcNow.AddDays(30),
                Status: "Active"
            );

            // Store in DynamoDB via EligibilityRepository
            var stored = await _eligibilityRepository.CreateEligibilityAsync(verificationCode, cycleId, ct);
            if (!stored)
            {
                _logger.LogWarning(
                    "Eligibility already exists for customer {CustomerId}, cycle {CycleId}, tier {Tier} (race condition)",
                    customerId, cycleId, threshold.Tier);
                continue;
            }

            _logger.LogInformation(
                "Customer {CustomerId} reached threshold tier {Tier} ({RequiredPurchases} purchases). OTP: {Otp}",
                customerId, threshold.Tier, threshold.RequiredPurchases, otp);

            // Send threshold reached SMS
            await _smsService.SendThresholdReachedSmsAsync(
                phone, customerName, threshold.GiftDescription, otp, outletName, ct);

            results.Add(new EligibilityResult(
                Tier: threshold.Tier,
                GiftType: threshold.GiftType,
                GiftDescription: threshold.GiftDescription,
                GiftValue: threshold.GiftValue,
                VerificationCode: otp
            ));
        }

        // Check if customer is 1 purchase away from the next threshold (progress SMS)
        await SendProgressUpdateIfCloseAsync(
            customerId, customerName, phone, qualifyingPurchases, thresholds, cycleId, ct);

        return results;
    }

    /// <summary>
    /// Sends a progress update SMS if the customer is exactly 1 purchase away from the next threshold.
    /// </summary>
    private async Task SendProgressUpdateIfCloseAsync(
        string customerId,
        string customerName,
        string phone,
        int qualifyingPurchases,
        List<PurchaseThreshold> thresholds,
        string cycleId,
        CancellationToken ct)
    {
        // Find the next threshold the customer hasn't reached yet
        var nextThreshold = thresholds
            .Where(t => t.IsEnabled && t.RequiredPurchases > qualifyingPurchases)
            .OrderBy(t => t.RequiredPurchases)
            .FirstOrDefault();

        if (nextThreshold is null)
            return;

        var remaining = nextThreshold.RequiredPurchases - qualifyingPurchases;
        if (remaining != 1)
            return;

        // Only send if they haven't already been notified for this threshold proximity
        // (We don't persist this, so it may send on each import — acceptable for MVP)
        _logger.LogInformation(
            "Customer {CustomerId} is 1 purchase away from tier {Tier} ({RequiredPurchases} purchases)",
            customerId, nextThreshold.Tier, nextThreshold.RequiredPurchases);

        await _smsService.SendProgressUpdateSmsAsync(
            phone, customerName, qualifyingPurchases, nextThreshold.RequiredPurchases,
            nextThreshold.GiftDescription, ct);
    }
}
