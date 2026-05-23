using VELoyalty.Core;
using VELoyalty.Data.Repositories;

namespace VELoyalty.Api.Services;

/// <summary>
/// Service for computing customer loyalty progress toward the next purchase threshold.
/// </summary>
public class CustomerService
{
    private readonly CustomerRepository _customerRepository;
    private readonly ConfigRepository _configRepository;
    private readonly CycleRepository _cycleRepository;
    private readonly VerificationCodeRepository _verificationCodeRepository;
    private readonly OutletRepository _outletRepository;

    public CustomerService(
        CustomerRepository customerRepository,
        ConfigRepository configRepository,
        CycleRepository cycleRepository,
        VerificationCodeRepository verificationCodeRepository,
        OutletRepository outletRepository)
    {
        _customerRepository = customerRepository;
        _configRepository = configRepository;
        _cycleRepository = cycleRepository;
        _verificationCodeRepository = verificationCodeRepository;
        _outletRepository = outletRepository;
    }

    /// <summary>
    /// Gets the full customer profile with progress information.
    /// </summary>
    /// <param name="phoneNumber">Customer phone number in E.164 format.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>Customer profile response, or null if not found.</returns>
    public async Task<CustomerProfileResponse?> GetCustomerProfileAsync(
        string phoneNumber,
        CancellationToken cancellationToken = default)
    {
        var customer = await _customerRepository.GetByPhoneAsync(phoneNumber, cancellationToken);
        if (customer is null)
            return null;

        var thresholds = await _configRepository.GetAllThresholdConfigsAsync(cancellationToken);
        var enabledThresholds = thresholds
            .Where(t => t.IsEnabled)
            .OrderBy(t => t.RequiredPurchases)
            .ToList();

        var progress = ComputeProgress(customer.QualifyingPurchases, enabledThresholds);

        return new CustomerProfileResponse(
            CustomerId: customer.CustomerId,
            Name: customer.Name,
            PhoneNumber: customer.PhoneNumber,
            QualifyingPurchases: customer.QualifyingPurchases,
            CurrentCycleId: customer.CurrentCycleId,
            Progress: progress
        );
    }

    /// <summary>
    /// Gets all verification codes for a customer in the current cycle with outlet names.
    /// </summary>
    /// <param name="phoneNumber">Customer phone number in E.164 format.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>List of verification code responses, or null if customer not found.</returns>
    public async Task<CustomerCodesResponse?> GetCustomerCodesAsync(
        string phoneNumber,
        CancellationToken cancellationToken = default)
    {
        var customer = await _customerRepository.GetByPhoneAsync(phoneNumber, cancellationToken);
        if (customer is null)
            return null;

        var activeCycle = await _cycleRepository.GetActiveCycleAsync(cancellationToken);
        if (activeCycle is null)
            return new CustomerCodesResponse(customer.CustomerId, customer.Name, customer.PhoneNumber, new List<VerificationCodeResponse>());

        var codes = await _verificationCodeRepository.GetByCustomerAndCycleAsync(
            customer.CustomerId, activeCycle.CycleId, cancellationToken);

        var codeResponses = new List<VerificationCodeResponse>();
        foreach (var code in codes)
        {
            var outletName = await GetOutletNameAsync(code.OutletId, cancellationToken);
            var effectiveStatus = GetEffectiveStatus(code);

            codeResponses.Add(new VerificationCodeResponse(
                Code: code.Code,
                Status: effectiveStatus,
                GiftTier: code.Tier,
                GiftType: code.GiftType,
                GiftDescription: code.GiftDescription,
                GiftValue: code.GiftValue,
                DesignatedOutlet: outletName,
                IssuedAt: code.IssuedAt
            ));
        }

        return new CustomerCodesResponse(
            customer.CustomerId,
            customer.Name,
            customer.PhoneNumber,
            codeResponses);
    }

    /// <summary>
    /// Searches for redemption information by phone number or verification code.
    /// </summary>
    /// <param name="phone">Optional phone number to search by.</param>
    /// <param name="code">Optional verification code to search by.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>List of redemption search results.</returns>
    public async Task<List<RedemptionSearchResult>> SearchRedemptionsAsync(
        string? phone,
        string? code,
        CancellationToken cancellationToken = default)
    {
        var results = new List<RedemptionSearchResult>();

        if (!string.IsNullOrWhiteSpace(code))
        {
            // Search by verification code directly (GSI2)
            var verificationCode = await _verificationCodeRepository.GetByCodeAsync(code, cancellationToken);
            if (verificationCode is not null)
            {
                var customer = await _customerRepository.GetByIdAsync(verificationCode.CustomerId, cancellationToken);
                var outletName = await GetOutletNameAsync(verificationCode.OutletId, cancellationToken);
                var effectiveStatus = GetEffectiveStatus(verificationCode);

                results.Add(new RedemptionSearchResult(
                    CustomerName: customer?.Name ?? "Unknown",
                    CustomerPhone: customer?.PhoneNumber ?? "Unknown",
                    Code: verificationCode.Code,
                    GiftTier: verificationCode.Tier,
                    GiftType: verificationCode.GiftType,
                    GiftDescription: verificationCode.GiftDescription,
                    GiftValue: verificationCode.GiftValue,
                    Status: effectiveStatus,
                    DesignatedOutlet: outletName,
                    OutletId: verificationCode.OutletId,
                    IssuedAt: verificationCode.IssuedAt,
                    ExpiresAt: verificationCode.ExpiresAt
                ));
            }
        }
        else if (!string.IsNullOrWhiteSpace(phone))
        {
            // Search by phone number - get customer, then their codes for current cycle
            var customer = await _customerRepository.GetByPhoneAsync(phone, cancellationToken);
            if (customer is not null)
            {
                var activeCycle = await _cycleRepository.GetActiveCycleAsync(cancellationToken);
                if (activeCycle is not null)
                {
                    var codes = await _verificationCodeRepository.GetByCustomerAndCycleAsync(
                        customer.CustomerId, activeCycle.CycleId, cancellationToken);

                    foreach (var vc in codes)
                    {
                        var outletName = await GetOutletNameAsync(vc.OutletId, cancellationToken);
                        var effectiveStatus = GetEffectiveStatus(vc);

                        results.Add(new RedemptionSearchResult(
                            CustomerName: customer.Name,
                            CustomerPhone: customer.PhoneNumber,
                            Code: vc.Code,
                            GiftTier: vc.Tier,
                            GiftType: vc.GiftType,
                            GiftDescription: vc.GiftDescription,
                            GiftValue: vc.GiftValue,
                            Status: effectiveStatus,
                            DesignatedOutlet: outletName,
                            OutletId: vc.OutletId,
                            IssuedAt: vc.IssuedAt,
                            ExpiresAt: vc.ExpiresAt
                        ));
                    }
                }
            }
        }

        return results;
    }

    /// <summary>
    /// Computes the customer's progress toward the next threshold.
    /// </summary>
    /// <param name="qualifyingPurchases">Current qualifying purchase count.</param>
    /// <param name="enabledThresholds">Enabled thresholds ordered by required purchases.</param>
    /// <returns>Progress information.</returns>
    public static ProgressInfo ComputeProgress(int qualifyingPurchases, List<PurchaseThreshold> enabledThresholds)
    {
        if (enabledThresholds.Count == 0)
        {
            return new ProgressInfo(
                CurrentPurchases: qualifyingPurchases,
                NextThreshold: null,
                NextThresholdTier: null,
                IsComplete: false,
                Description: "No thresholds configured"
            );
        }

        // Find the next unachieved threshold
        var nextThreshold = enabledThresholds
            .FirstOrDefault(t => t.RequiredPurchases > qualifyingPurchases);

        if (nextThreshold is null)
        {
            // All thresholds have been reached
            return new ProgressInfo(
                CurrentPurchases: qualifyingPurchases,
                NextThreshold: null,
                NextThresholdTier: null,
                IsComplete: true,
                Description: "All reward tiers achieved"
            );
        }

        return new ProgressInfo(
            CurrentPurchases: qualifyingPurchases,
            NextThreshold: nextThreshold.RequiredPurchases,
            NextThresholdTier: nextThreshold.Tier,
            IsComplete: false,
            Description: $"{qualifyingPurchases} of {nextThreshold.RequiredPurchases} purchases"
        );
    }

    private async Task<string> GetOutletNameAsync(string outletId, CancellationToken cancellationToken)
    {
        var outlet = await _outletRepository.GetByIdAsync(outletId, cancellationToken);
        return outlet?.Name ?? outletId;
    }

    /// <summary>
    /// Determines the effective status of a verification code, accounting for expiration.
    /// If the code is marked Active but has passed its expiry date, it's treated as Expired.
    /// </summary>
    private static string GetEffectiveStatus(VerificationCode code)
    {
        if (code.Status == "Active" && DateTime.UtcNow > code.ExpiresAt)
            return "Expired";
        return code.Status;
    }
}

// ─── Response DTOs ──────────────────────────────────────────────────────────────

public record CustomerProfileResponse(
    string CustomerId,
    string Name,
    string PhoneNumber,
    int QualifyingPurchases,
    string CurrentCycleId,
    ProgressInfo Progress
);

public record ProgressInfo(
    int CurrentPurchases,
    int? NextThreshold,
    int? NextThresholdTier,
    bool IsComplete,
    string Description
);

public record CustomerCodesResponse(
    string CustomerId,
    string Name,
    string PhoneNumber,
    List<VerificationCodeResponse> Codes
);

public record VerificationCodeResponse(
    string Code,
    string Status,
    int GiftTier,
    string GiftType,
    string GiftDescription,
    decimal GiftValue,
    string DesignatedOutlet,
    DateTime IssuedAt
);

public record RedemptionSearchResult(
    string CustomerName,
    string CustomerPhone,
    string Code,
    int GiftTier,
    string GiftType,
    string GiftDescription,
    decimal GiftValue,
    string Status,
    string DesignatedOutlet,
    string OutletId,
    DateTime IssuedAt,
    DateTime ExpiresAt
);
