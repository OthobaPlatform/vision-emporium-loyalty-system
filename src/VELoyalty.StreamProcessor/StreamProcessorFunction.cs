using System.Text.Json;
using Amazon.Lambda.Core;
using Amazon.Lambda.DynamoDBEvents;
using VELoyalty.Core;
using VELoyalty.Data;
using VELoyalty.Data.Repositories;

using StreamAttributeValue = Amazon.Lambda.DynamoDBEvents.DynamoDBEvent.AttributeValue;

namespace VELoyalty.StreamProcessor;

/// <summary>
/// Processes DynamoDB Stream events for INSERT operations on purchase records.
/// Evaluates customer eligibility against configured thresholds and triggers notifications.
/// </summary>
public class StreamProcessorFunction
{
    private readonly PurchaseRepository _purchaseRepository;
    private readonly CustomerRepository _customerRepository;
    private readonly ConfigRepository _configRepository;
    private readonly CycleRepository _cycleRepository;
    private readonly EligibilityRepository _eligibilityRepository;
    private readonly VerificationCodeRepository _verificationCodeRepository;
    private readonly INotificationInvoker _notificationInvoker;

    public StreamProcessorFunction(
        PurchaseRepository purchaseRepository,
        CustomerRepository customerRepository,
        ConfigRepository configRepository,
        CycleRepository cycleRepository,
        EligibilityRepository eligibilityRepository,
        VerificationCodeRepository verificationCodeRepository,
        INotificationInvoker notificationInvoker)
    {
        _purchaseRepository = purchaseRepository;
        _customerRepository = customerRepository;
        _configRepository = configRepository;
        _cycleRepository = cycleRepository;
        _eligibilityRepository = eligibilityRepository;
        _verificationCodeRepository = verificationCodeRepository;
        _notificationInvoker = notificationInvoker;
    }

    /// <summary>
    /// Handles DynamoDB Stream events. Processes only INSERT events where the SK starts with "PURCH#".
    /// For each qualifying purchase insertion:
    /// 1. Gets the customer's qualifying purchase count (filtered by min amount and excluded categories)
    /// 2. Gets enabled thresholds from configuration
    /// 3. Checks if count matches any enabled threshold
    /// 4. If threshold reached and not already eligible: creates eligibility record, generates verification code, invokes notification
    /// </summary>
    public async Task HandleAsync(DynamoDBEvent dynamoDbEvent, ILambdaContext context)
    {
        context.Logger.LogInformation($"Processing {dynamoDbEvent.Records.Count} stream record(s).");

        foreach (var record in dynamoDbEvent.Records)
        {
            try
            {
                await ProcessRecordAsync(record, context);
            }
            catch (Exception ex)
            {
                context.Logger.LogError($"Error processing record: {ex.Message}");
                // Continue processing remaining records; do not fail the entire batch
            }
        }
    }

    private async Task ProcessRecordAsync(DynamoDBEvent.DynamodbStreamRecord record, ILambdaContext context)
    {
        // Only process INSERT events
        if (!string.Equals(record.EventName, "INSERT", StringComparison.OrdinalIgnoreCase))
        {
            return;
        }

        var newImage = record.Dynamodb.NewImage;
        if (newImage is null || newImage.Count == 0)
        {
            return;
        }

        // Only process purchase records (SK starts with "PURCH#")
        if (!newImage.TryGetValue("SK", out var skValue) || skValue.S is null)
        {
            return;
        }

        if (!skValue.S.StartsWith("PURCH#", StringComparison.Ordinal))
        {
            return;
        }

        // Extract purchase details from the stream record
        var customerId = GetStreamStringAttribute(newImage, "CustomerId");
        var outletId = GetStreamStringAttribute(newImage, "OutletId");
        var amountStr = GetStreamNumberAttribute(newImage, "Amount");
        var productCategory = GetStreamStringAttribute(newImage, "ProductCategory");
        var purchaseDateStr = GetStreamStringAttribute(newImage, "PurchaseDate");

        if (string.IsNullOrEmpty(customerId))
        {
            context.Logger.LogWarning("Stream record missing CustomerId, skipping.");
            return;
        }

        context.Logger.LogInformation($"Processing purchase for customer {customerId} at outlet {outletId}.");

        // Get the active loyalty cycle
        var activeCycle = await _cycleRepository.GetActiveCycleAsync();
        if (activeCycle is null)
        {
            context.Logger.LogWarning("No active loyalty cycle found, skipping eligibility evaluation.");
            return;
        }

        // Get general config for min purchase amount and excluded categories
        var generalConfig = await _configRepository.GetGeneralConfigAsync();
        var minPurchaseAmount = generalConfig?.MinPurchaseAmount ?? Constants.MinPurchaseAmount;
        var excludedCategories = generalConfig?.ExcludedCategories ?? new List<string>();

        // Get the customer's qualifying purchase count within the current cycle
        var qualifyingCount = await _purchaseRepository.GetQualifyingPurchaseCountAsync(
            customerId,
            activeCycle.StartDate,
            activeCycle.EndDate,
            minPurchaseAmount,
            excludedCategories);

        context.Logger.LogInformation(
            $"Customer {customerId} has {qualifyingCount} qualifying purchases in cycle {activeCycle.CycleId}.");

        // Update customer's qualifying purchase count
        await _customerRepository.UpdateQualifyingPurchasesAsync(customerId, qualifyingCount);

        // Get all enabled thresholds
        var allThresholds = await _configRepository.GetAllThresholdConfigsAsync();
        var enabledThresholds = allThresholds
            .Where(t => t.IsEnabled)
            .OrderBy(t => t.RequiredPurchases)
            .ToList();

        if (enabledThresholds.Count == 0)
        {
            context.Logger.LogInformation("No enabled thresholds configured, skipping.");
            return;
        }

        // Check if the qualifying count matches any enabled threshold
        foreach (var threshold in enabledThresholds)
        {
            if (qualifyingCount != threshold.RequiredPurchases)
            {
                continue;
            }

            context.Logger.LogInformation(
                $"Customer {customerId} reached threshold tier {threshold.Tier} " +
                $"({threshold.RequiredPurchases} purchases).");

            // Check if customer is already eligible for this tier in the current cycle
            var alreadyEligible = await _eligibilityRepository.ExistsAsync(
                customerId, activeCycle.CycleId, threshold.Tier);

            if (alreadyEligible)
            {
                context.Logger.LogInformation(
                    $"Customer {customerId} already eligible for tier {threshold.Tier} in cycle {activeCycle.CycleId}, skipping.");
                continue;
            }

            // Determine the outlet for the verification code (most recent qualifying purchase outlet)
            var recentOutletId = await GetMostRecentQualifyingPurchaseOutletAsync(
                customerId, activeCycle, minPurchaseAmount, excludedCategories);

            var designatedOutletId = recentOutletId ?? outletId ?? string.Empty;

            // Generate a unique 6-digit verification code
            var code = await _verificationCodeRepository.GenerateUniqueCodeAsync();

            // Calculate code expiry
            var codeExpiryDays = generalConfig?.CodeExpiryDays ?? Constants.DefaultCodeExpiryDays;
            var now = DateTime.UtcNow;

            var verificationCode = new VerificationCode(
                Code: code,
                CustomerId: customerId,
                OutletId: designatedOutletId,
                Tier: threshold.Tier,
                GiftType: threshold.GiftType,
                GiftDescription: threshold.GiftDescription,
                GiftValue: threshold.GiftValue,
                IssuedAt: now,
                ExpiresAt: now.AddDays(codeExpiryDays),
                Status: CodeStatus.Active.ToString()
            );

            // Create eligibility record (also stores the verification code)
            var created = await _eligibilityRepository.CreateEligibilityAsync(
                verificationCode, activeCycle.CycleId);

            if (!created)
            {
                context.Logger.LogWarning(
                    $"Failed to create eligibility for customer {customerId}, tier {threshold.Tier} " +
                    $"(possible race condition). Skipping notification.");
                continue;
            }

            context.Logger.LogInformation(
                $"Created eligibility record for customer {customerId}, tier {threshold.Tier}, " +
                $"code {code}, outlet {designatedOutletId}.");

            // Get customer details for notification
            var customer = await _customerRepository.GetByIdAsync(customerId);
            if (customer is null)
            {
                context.Logger.LogWarning(
                    $"Customer {customerId} not found in database, cannot send notification.");
                continue;
            }

            // Invoke Notification Lambda
            var notificationPayload = new NotificationPayload(
                CustomerId: customerId,
                CustomerName: customer.Name,
                CustomerPhone: customer.PhoneNumber,
                VerificationCode: code,
                GiftDescription: threshold.GiftDescription,
                GiftType: threshold.GiftType,
                GiftValue: threshold.GiftValue,
                OutletId: designatedOutletId,
                Tier: threshold.Tier
            );

            await _notificationInvoker.InvokeAsync(notificationPayload);

            context.Logger.LogInformation(
                $"Notification invoked for customer {customerId}, code {code}.");
        }
    }

    /// <summary>
    /// Gets the outlet ID of the most recent qualifying purchase for the customer in the current cycle.
    /// </summary>
    private async Task<string?> GetMostRecentQualifyingPurchaseOutletAsync(
        string customerId,
        LoyaltyCycle activeCycle,
        decimal minPurchaseAmount,
        List<string> excludedCategories)
    {
        var purchases = await _purchaseRepository.GetByCustomerAndCycleAsync(
            customerId, activeCycle.StartDate, activeCycle.EndDate);

        var qualifyingPurchases = purchases
            .Where(p => p.Amount >= minPurchaseAmount &&
                        !excludedCategories.Contains(p.ProductCategory, StringComparer.OrdinalIgnoreCase))
            .OrderByDescending(p => p.PurchaseDate)
            .ThenByDescending(p => p.ProcessedAt)
            .ToList();

        return qualifyingPurchases.FirstOrDefault()?.OutletId;
    }

    private static string? GetStreamStringAttribute(Dictionary<string, StreamAttributeValue> image, string key)
    {
        return image.TryGetValue(key, out var value) ? value.S : null;
    }

    private static string? GetStreamNumberAttribute(Dictionary<string, StreamAttributeValue> image, string key)
    {
        return image.TryGetValue(key, out var value) ? value.N : null;
    }
}
