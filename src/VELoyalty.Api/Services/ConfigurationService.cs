using VELoyalty.Core;
using VELoyalty.Core.Validation;
using VELoyalty.Data.Repositories;

namespace VELoyalty.Api.Services;

/// <summary>
/// Service for managing loyalty system configuration: cycles, thresholds, and general settings.
/// Enforces business rules: cycle changes apply to next cycle only, threshold changes are non-retroactive.
/// Records all configuration changes in the audit log.
/// </summary>
public class ConfigurationService
{
    private readonly ConfigRepository _configRepository;
    private readonly CycleRepository _cycleRepository;
    private readonly AuditRepository _auditRepository;

    public ConfigurationService(
        ConfigRepository configRepository,
        CycleRepository cycleRepository,
        AuditRepository auditRepository)
    {
        _configRepository = configRepository;
        _cycleRepository = cycleRepository;
        _auditRepository = auditRepository;
    }

    // ─── Cycle Configuration ────────────────────────────────────────────────────

    /// <summary>
    /// Gets the current/active loyalty cycle configuration.
    /// </summary>
    public async Task<CycleConfigResponse?> GetCycleConfigAsync(CancellationToken cancellationToken = default)
    {
        var activeCycle = await _cycleRepository.GetActiveCycleAsync(cancellationToken);
        if (activeCycle is null)
            return null;

        var today = DateOnly.FromDateTime(TimeZoneInfo.ConvertTimeFromUtc(DateTime.UtcNow, Constants.SystemTimeZone));
        var daysRemaining = activeCycle.EndDate.DayNumber - today.DayNumber;
        if (daysRemaining < 0) daysRemaining = 0;

        return new CycleConfigResponse(
            CycleId: activeCycle.CycleId,
            StartDate: activeCycle.StartDate,
            EndDate: activeCycle.EndDate,
            IsActive: activeCycle.IsActive,
            DaysRemaining: daysRemaining
        );
    }

    /// <summary>
    /// Updates the loyalty cycle configuration. Changes apply to the next cycle only,
    /// not the current active cycle.
    /// </summary>
    public async Task<ConfigUpdateResult> UpdateCycleConfigAsync(
        UpdateCycleRequest request,
        string actorId,
        CancellationToken cancellationToken = default)
    {
        // Validate dates
        var validation = CycleValidator.Validate(request.StartDate, request.EndDate);
        if (!validation.IsValid)
        {
            return ConfigUpdateResult.ValidationFailed(validation.Errors);
        }

        // Get the active cycle to ensure we don't modify it
        var activeCycle = await _cycleRepository.GetActiveCycleAsync(cancellationToken);

        // Generate a new cycle ID for the next cycle
        var nextCycleId = $"{request.StartDate.Year}-{request.EndDate.Year}";

        // If there's an active cycle, ensure we're not modifying it
        if (activeCycle is not null && activeCycle.CycleId == nextCycleId && activeCycle.IsActive)
        {
            // The requested dates overlap with the active cycle - create as next cycle
            nextCycleId = $"{request.StartDate.Year}-{request.EndDate.Year}-next";
        }

        var oldCycle = await _configRepository.GetCycleConfigAsync(nextCycleId, cancellationToken);

        // Create the next cycle configuration (not active until current cycle ends)
        var newCycle = new LoyaltyCycle(
            CycleId: nextCycleId,
            StartDate: request.StartDate,
            EndDate: request.EndDate,
            IsActive: false
        );

        await _configRepository.PutCycleConfigAsync(newCycle, cancellationToken);

        // Record audit entry
        var details = new Dictionary<string, string>
        {
            ["cycleId"] = nextCycleId,
            ["startDate"] = request.StartDate.ToString("yyyy-MM-dd"),
            ["endDate"] = request.EndDate.ToString("yyyy-MM-dd"),
            ["appliesTo"] = "next_cycle"
        };

        if (oldCycle is not null)
        {
            details["previousStartDate"] = oldCycle.StartDate.ToString("yyyy-MM-dd");
            details["previousEndDate"] = oldCycle.EndDate.ToString("yyyy-MM-dd");
        }

        await _auditRepository.AppendAsync(new AuditEntry(
            EventType: nameof(AuditEventType.ConfigChange),
            ActorId: actorId,
            EntityType: "LoyaltyCycle",
            EntityId: nextCycleId,
            Details: details,
            Timestamp: DateTime.UtcNow
        ), cancellationToken);

        return ConfigUpdateResult.Succeeded(new CycleConfigResponse(
            CycleId: newCycle.CycleId,
            StartDate: newCycle.StartDate,
            EndDate: newCycle.EndDate,
            IsActive: newCycle.IsActive,
            DaysRemaining: null
        ));
    }

    // ─── Threshold Configuration ────────────────────────────────────────────────

    /// <summary>
    /// Gets all configured purchase thresholds.
    /// </summary>
    public async Task<List<ThresholdConfigResponse>> GetThresholdConfigsAsync(CancellationToken cancellationToken = default)
    {
        var thresholds = await _configRepository.GetAllThresholdConfigsAsync(cancellationToken);
        return thresholds.Select(t => new ThresholdConfigResponse(
            Tier: t.Tier,
            RequiredPurchases: t.RequiredPurchases,
            GiftType: t.GiftType,
            GiftDescription: t.GiftDescription,
            GiftValue: t.GiftValue,
            GiftValueType: t.GiftValueType,
            IsEnabled: t.IsEnabled,
            MinPurchaseAmount: t.MinPurchaseAmount,
            ExcludedCategories: t.ExcludedCategories
        )).ToList();
    }

    /// <summary>
    /// Updates purchase threshold configurations. Changes apply to future purchases only (non-retroactive).
    /// </summary>
    public async Task<ConfigUpdateResult> UpdateThresholdConfigsAsync(
        UpdateThresholdsRequest request,
        string actorId,
        CancellationToken cancellationToken = default)
    {
        // Validate threshold values
        var thresholdValues = request.Thresholds.Select(t => t.RequiredPurchases).ToList();
        var validation = ThresholdValidator.Validate(thresholdValues);
        if (!validation.IsValid)
        {
            return ConfigUpdateResult.ValidationFailed(validation.Errors);
        }

        // Additional validation for each threshold
        var errors = new List<string>();
        for (int i = 0; i < request.Thresholds.Count; i++)
        {
            var threshold = request.Thresholds[i];

            if (string.IsNullOrWhiteSpace(threshold.GiftType) ||
                (threshold.GiftType != nameof(GiftType.Cash_Return) && threshold.GiftType != nameof(GiftType.Gift_Item)))
            {
                errors.Add($"Threshold at position {i + 1}: GiftType must be 'Cash_Return' or 'Gift_Item'.");
            }

            if (string.IsNullOrWhiteSpace(threshold.GiftDescription))
            {
                errors.Add($"Threshold at position {i + 1}: GiftDescription is required.");
            }
            else if (threshold.GiftDescription.Length > Constants.MaxGiftDescriptionLength)
            {
                errors.Add($"Threshold at position {i + 1}: GiftDescription must not exceed {Constants.MaxGiftDescriptionLength} characters.");
            }

            // Gift value validation only applies to Cash_Return
            if (threshold.GiftType == nameof(GiftType.Cash_Return))
            {
                var valueType = threshold.GiftValueType ?? "fixed";
                if (valueType != "fixed" && valueType != "percentage")
                {
                    errors.Add($"Threshold at position {i + 1}: GiftValueType must be 'fixed' or 'percentage'.");
                }
                else if (valueType == "percentage")
                {
                    if (threshold.GiftValue < 0.01m || threshold.GiftValue > 100m)
                    {
                        errors.Add($"Threshold at position {i + 1}: GiftValue percentage must be between 0.01 and 100.");
                    }
                }
                else
                {
                    if (threshold.GiftValue < Constants.MinGiftValue || threshold.GiftValue > Constants.MaxGiftValue)
                    {
                        errors.Add($"Threshold at position {i + 1}: GiftValue must be between {Constants.MinGiftValue} and {Constants.MaxGiftValue} BDT.");
                    }
                }
            }
        }

        if (errors.Count > 0)
        {
            return ConfigUpdateResult.ValidationFailed(errors);
        }

        // Get existing thresholds for audit comparison
        var existingThresholds = await _configRepository.GetAllThresholdConfigsAsync(cancellationToken);

        // Build new threshold list
        var newThresholds = request.Thresholds.Select((t, index) => new PurchaseThreshold(
            Tier: index + 1,
            RequiredPurchases: t.RequiredPurchases,
            GiftType: t.GiftType,
            GiftDescription: t.GiftDescription,
            GiftValue: t.GiftType == nameof(GiftType.Gift_Item) ? 0m : t.GiftValue,
            GiftValueType: t.GiftType == nameof(GiftType.Gift_Item) ? "fixed" : (t.GiftValueType ?? "fixed"),
            IsEnabled: t.IsEnabled,
            MinPurchaseAmount: t.MinPurchaseAmount ?? Constants.MinPurchaseAmount,
            ExcludedCategories: t.ExcludedCategories ?? new List<string>()
        )).ToList();

        // Save all thresholds (replaces existing)
        await _configRepository.PutAllThresholdConfigsAsync(newThresholds, cancellationToken);

        // Record audit entry
        var details = new Dictionary<string, string>
        {
            ["thresholdCount"] = newThresholds.Count.ToString(),
            ["previousThresholdCount"] = existingThresholds.Count.ToString(),
            ["appliesTo"] = "future_purchases_only",
            ["thresholdValues"] = string.Join(",", newThresholds.Select(t => t.RequiredPurchases))
        };

        await _auditRepository.AppendAsync(new AuditEntry(
            EventType: nameof(AuditEventType.ConfigChange),
            ActorId: actorId,
            EntityType: "PurchaseThreshold",
            EntityId: "all",
            Details: details,
            Timestamp: DateTime.UtcNow
        ), cancellationToken);

        var response = newThresholds.Select(t => new ThresholdConfigResponse(
            Tier: t.Tier,
            RequiredPurchases: t.RequiredPurchases,
            GiftType: t.GiftType,
            GiftDescription: t.GiftDescription,
            GiftValue: t.GiftValue,
            GiftValueType: t.GiftValueType,
            IsEnabled: t.IsEnabled,
            MinPurchaseAmount: t.MinPurchaseAmount,
            ExcludedCategories: t.ExcludedCategories
        )).ToList();

        return ConfigUpdateResult.Succeeded(response);
    }

    // ─── General Configuration ──────────────────────────────────────────────────

    /// <summary>
    /// Gets the general system configuration settings.
    /// </summary>
    public async Task<GeneralConfigResponse> GetGeneralConfigAsync(CancellationToken cancellationToken = default)
    {
        var config = await _configRepository.GetGeneralConfigAsync(cancellationToken);

        // Return defaults if no config exists yet
        if (config is null)
        {
            return new GeneralConfigResponse(
                SyncIntervalMinutes: Constants.DefaultSyncIntervalMinutes,
                CodeExpiryDays: Constants.DefaultCodeExpiryDays,
                MinPurchaseAmount: Constants.MinPurchaseAmount,
                ExcludedCategories: new List<string>()
            );
        }

        return new GeneralConfigResponse(
            SyncIntervalMinutes: config.SyncIntervalMinutes,
            CodeExpiryDays: config.CodeExpiryDays,
            MinPurchaseAmount: config.MinPurchaseAmount,
            ExcludedCategories: config.ExcludedCategories
        );
    }

    /// <summary>
    /// Updates the general system configuration settings.
    /// </summary>
    public async Task<ConfigUpdateResult> UpdateGeneralConfigAsync(
        UpdateGeneralConfigRequest request,
        string actorId,
        CancellationToken cancellationToken = default)
    {
        var errors = new List<string>();

        // Validate sync interval (minimum 15 minutes)
        if (request.SyncIntervalMinutes < Constants.MinSyncIntervalMinutes)
        {
            errors.Add($"Sync interval must be at least {Constants.MinSyncIntervalMinutes} minutes.");
        }

        // Validate code expiry days (7-90)
        if (request.CodeExpiryDays < Constants.MinCodeExpiryDays || request.CodeExpiryDays > Constants.MaxCodeExpiryDays)
        {
            errors.Add($"Code expiry days must be between {Constants.MinCodeExpiryDays} and {Constants.MaxCodeExpiryDays}.");
        }

        // Validate min purchase amount
        if (request.MinPurchaseAmount < Constants.MinPurchaseAmount || request.MinPurchaseAmount > Constants.MaxGiftValue)
        {
            errors.Add($"Minimum purchase amount must be between {Constants.MinPurchaseAmount} and {Constants.MaxGiftValue} BDT.");
        }

        if (errors.Count > 0)
        {
            return ConfigUpdateResult.ValidationFailed(errors);
        }

        // Get existing config for audit comparison
        var existingConfig = await _configRepository.GetGeneralConfigAsync(cancellationToken);

        var newConfig = new GeneralConfig(
            SyncIntervalMinutes: request.SyncIntervalMinutes,
            CodeExpiryDays: request.CodeExpiryDays,
            MinPurchaseAmount: request.MinPurchaseAmount,
            ExcludedCategories: request.ExcludedCategories ?? new List<string>()
        );

        await _configRepository.PutGeneralConfigAsync(newConfig, cancellationToken);

        // Record audit entry
        var details = new Dictionary<string, string>
        {
            ["syncIntervalMinutes"] = request.SyncIntervalMinutes.ToString(),
            ["codeExpiryDays"] = request.CodeExpiryDays.ToString(),
            ["minPurchaseAmount"] = request.MinPurchaseAmount.ToString("F2"),
            ["excludedCategories"] = string.Join(",", newConfig.ExcludedCategories)
        };

        if (existingConfig is not null)
        {
            details["previousSyncIntervalMinutes"] = existingConfig.SyncIntervalMinutes.ToString();
            details["previousCodeExpiryDays"] = existingConfig.CodeExpiryDays.ToString();
            details["previousMinPurchaseAmount"] = existingConfig.MinPurchaseAmount.ToString("F2");
            details["previousExcludedCategories"] = string.Join(",", existingConfig.ExcludedCategories);
        }

        await _auditRepository.AppendAsync(new AuditEntry(
            EventType: nameof(AuditEventType.ConfigChange),
            ActorId: actorId,
            EntityType: "GeneralConfig",
            EntityId: "general",
            Details: details,
            Timestamp: DateTime.UtcNow
        ), cancellationToken);

        return ConfigUpdateResult.Succeeded(new GeneralConfigResponse(
            SyncIntervalMinutes: newConfig.SyncIntervalMinutes,
            CodeExpiryDays: newConfig.CodeExpiryDays,
            MinPurchaseAmount: newConfig.MinPurchaseAmount,
            ExcludedCategories: newConfig.ExcludedCategories
        ));
    }
}

// ─── Request DTOs ───────────────────────────────────────────────────────────────

/// <summary>
/// Request to update the loyalty cycle configuration.
/// </summary>
public record UpdateCycleRequest(
    DateOnly StartDate,
    DateOnly EndDate
);

/// <summary>
/// Request to update purchase threshold configurations.
/// </summary>
public record UpdateThresholdsRequest(
    List<ThresholdInput> Thresholds
);

/// <summary>
/// Input model for a single threshold in an update request.
/// </summary>
public record ThresholdInput(
    int RequiredPurchases,
    string GiftType,
    string GiftDescription,
    decimal GiftValue,
    string? GiftValueType,
    bool IsEnabled,
    decimal? MinPurchaseAmount,
    List<string>? ExcludedCategories
);

/// <summary>
/// Request to update general system configuration.
/// </summary>
public record UpdateGeneralConfigRequest(
    int SyncIntervalMinutes,
    int CodeExpiryDays,
    decimal MinPurchaseAmount,
    List<string>? ExcludedCategories
);

// ─── Response DTOs ──────────────────────────────────────────────────────────────

/// <summary>
/// Response for loyalty cycle configuration.
/// </summary>
public record CycleConfigResponse(
    string CycleId,
    DateOnly StartDate,
    DateOnly EndDate,
    bool IsActive,
    int? DaysRemaining
);

/// <summary>
/// Response for a single threshold configuration.
/// </summary>
public record ThresholdConfigResponse(
    int Tier,
    int RequiredPurchases,
    string GiftType,
    string GiftDescription,
    decimal GiftValue,
    string GiftValueType,
    bool IsEnabled,
    decimal MinPurchaseAmount,
    List<string> ExcludedCategories
);

/// <summary>
/// Response for general system configuration.
/// </summary>
public record GeneralConfigResponse(
    int SyncIntervalMinutes,
    int CodeExpiryDays,
    decimal MinPurchaseAmount,
    List<string> ExcludedCategories
);

/// <summary>
/// Result of a configuration update operation.
/// </summary>
public class ConfigUpdateResult
{
    public bool IsSuccess { get; private init; }
    public List<string>? Errors { get; private init; }
    public object? Data { get; private init; }

    public static ConfigUpdateResult ValidationFailed(List<string> errors) =>
        new() { IsSuccess = false, Errors = errors };

    public static ConfigUpdateResult Succeeded(object data) =>
        new() { IsSuccess = true, Data = data };
}
