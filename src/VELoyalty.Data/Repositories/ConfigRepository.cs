using Amazon.DynamoDBv2.Model;
using VELoyalty.Core;

namespace VELoyalty.Data.Repositories;

/// <summary>
/// Repository for managing configuration data: loyalty cycles, purchase thresholds, and general settings.
/// All config items share PK = "CONFIG" with different SK patterns.
/// </summary>
public class ConfigRepository : DynamoDbRepository
{
    private const string GeneralConfigId = "GENERAL";
    private const string GeneralConfigType = "SETTINGS";

    public ConfigRepository(DynamoDbContext context) : base(context)
    {
    }

    // ─── Cycle Config ───────────────────────────────────────────────────────────

    /// <summary>
    /// Gets a loyalty cycle by its cycle ID.
    /// </summary>
    /// <param name="cycleId">The cycle identifier.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>The loyalty cycle, or null if not found.</returns>
    public async Task<LoyaltyCycle?> GetCycleConfigAsync(string cycleId, CancellationToken cancellationToken = default)
    {
        var item = await GetItemAsync(
            KeyBuilder.CyclePk(),
            KeyBuilder.CycleSk(cycleId),
            cancellationToken: cancellationToken);

        return item is null ? null : MapToLoyaltyCycle(item);
    }

    /// <summary>
    /// Gets the currently active loyalty cycle.
    /// </summary>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>The active loyalty cycle, or null if none is active.</returns>
    public async Task<LoyaltyCycle?> GetActiveCycleAsync(CancellationToken cancellationToken = default)
    {
        var items = await QueryAsync(
            keyConditionExpression: "PK = :pk AND begins_with(SK, :prefix)",
            expressionAttributeValues: new Dictionary<string, AttributeValue>
            {
                [":pk"] = AttributeValueSerializer.ToS(KeyBuilder.CyclePk()),
                [":prefix"] = AttributeValueSerializer.ToS("CYCLE#"),
                [":active"] = AttributeValueSerializer.ToBool(true)
            },
            filterExpression: "IsActive = :active",
            cancellationToken: cancellationToken);

        var activeItem = items.FirstOrDefault();
        return activeItem is null ? null : MapToLoyaltyCycle(activeItem);
    }

    /// <summary>
    /// Creates or updates a loyalty cycle configuration.
    /// </summary>
    /// <param name="cycle">The loyalty cycle to store.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    public async Task PutCycleConfigAsync(LoyaltyCycle cycle, CancellationToken cancellationToken = default)
    {
        var item = AttributeValueSerializer.NewItem(
                KeyBuilder.CyclePk(),
                KeyBuilder.CycleSk(cycle.CycleId))
            .WithString("CycleId", cycle.CycleId)
            .WithDate("StartDate", cycle.StartDate)
            .WithDate("EndDate", cycle.EndDate)
            .WithBool("IsActive", cycle.IsActive)
            .Build();

        await PutItemAsync(item, cancellationToken: cancellationToken);
    }

    // ─── Threshold Config ───────────────────────────────────────────────────────

    /// <summary>
    /// Gets a single purchase threshold by tier number.
    /// </summary>
    /// <param name="tier">The tier number.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>The purchase threshold, or null if not found.</returns>
    public async Task<PurchaseThreshold?> GetThresholdConfigAsync(int tier, CancellationToken cancellationToken = default)
    {
        var item = await GetItemAsync(
            KeyBuilder.ThresholdPk(),
            KeyBuilder.ThresholdSk(tier),
            cancellationToken: cancellationToken);

        return item is null ? null : MapToPurchaseThreshold(item);
    }

    /// <summary>
    /// Gets all configured purchase thresholds.
    /// </summary>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>List of all purchase thresholds, ordered by tier.</returns>
    public async Task<List<PurchaseThreshold>> GetAllThresholdConfigsAsync(CancellationToken cancellationToken = default)
    {
        var items = await QueryAsync(
            keyConditionExpression: "PK = :pk AND begins_with(SK, :prefix)",
            expressionAttributeValues: new Dictionary<string, AttributeValue>
            {
                [":pk"] = AttributeValueSerializer.ToS(KeyBuilder.ThresholdPk()),
                [":prefix"] = AttributeValueSerializer.ToS("THRESH#")
            },
            scanIndexForward: true,
            cancellationToken: cancellationToken);

        return items.Select(MapToPurchaseThreshold).OrderBy(t => t.Tier).ToList();
    }

    /// <summary>
    /// Creates or updates a purchase threshold configuration.
    /// </summary>
    /// <param name="threshold">The purchase threshold to store.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    public async Task PutThresholdConfigAsync(PurchaseThreshold threshold, CancellationToken cancellationToken = default)
    {
        var item = AttributeValueSerializer.NewItem(
                KeyBuilder.ThresholdPk(),
                KeyBuilder.ThresholdSk(threshold.Tier))
            .WithInt("Tier", threshold.Tier)
            .WithInt("RequiredPurchases", threshold.RequiredPurchases)
            .WithString("GiftType", threshold.GiftType)
            .WithString("GiftDescription", threshold.GiftDescription)
            .WithDecimal("GiftValue", threshold.GiftValue)
            .WithString("GiftValueType", threshold.GiftValueType)
            .WithBool("IsEnabled", threshold.IsEnabled)
            .WithDecimal("MinPurchaseAmount", threshold.MinPurchaseAmount)
            .WithStringList("ExcludedCategories", threshold.ExcludedCategories)
            .Build();

        await PutItemAsync(item, cancellationToken: cancellationToken);
    }

    /// <summary>
    /// Stores multiple purchase threshold configurations (replaces all existing thresholds).
    /// </summary>
    /// <param name="thresholds">The list of thresholds to store.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    public async Task PutAllThresholdConfigsAsync(List<PurchaseThreshold> thresholds, CancellationToken cancellationToken = default)
    {
        var items = thresholds.Select(threshold =>
            AttributeValueSerializer.NewItem(
                    KeyBuilder.ThresholdPk(),
                    KeyBuilder.ThresholdSk(threshold.Tier))
                .WithInt("Tier", threshold.Tier)
                .WithInt("RequiredPurchases", threshold.RequiredPurchases)
                .WithString("GiftType", threshold.GiftType)
                .WithString("GiftDescription", threshold.GiftDescription)
                .WithDecimal("GiftValue", threshold.GiftValue)
                .WithString("GiftValueType", threshold.GiftValueType)
                .WithBool("IsEnabled", threshold.IsEnabled)
                .WithDecimal("MinPurchaseAmount", threshold.MinPurchaseAmount)
                .WithStringList("ExcludedCategories", threshold.ExcludedCategories)
                .Build()
        ).ToList();

        await BatchWriteAsync(items, cancellationToken);
    }

    // ─── General Config ─────────────────────────────────────────────────────────

    /// <summary>
    /// Gets the general system configuration.
    /// </summary>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>The general config, or null if not configured.</returns>
    public async Task<GeneralConfig?> GetGeneralConfigAsync(CancellationToken cancellationToken = default)
    {
        var item = await GetItemAsync(
            KeyBuilder.ConfigPk(),
            KeyBuilder.ConfigSk(GeneralConfigType, GeneralConfigId),
            cancellationToken: cancellationToken);

        return item is null ? null : MapToGeneralConfig(item);
    }

    /// <summary>
    /// Creates or updates the general system configuration.
    /// </summary>
    /// <param name="config">The general config to store.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    public async Task PutGeneralConfigAsync(GeneralConfig config, CancellationToken cancellationToken = default)
    {
        var item = AttributeValueSerializer.NewItem(
                KeyBuilder.ConfigPk(),
                KeyBuilder.ConfigSk(GeneralConfigType, GeneralConfigId))
            .WithInt("SyncIntervalMinutes", config.SyncIntervalMinutes)
            .WithInt("CodeExpiryDays", config.CodeExpiryDays)
            .WithDecimal("MinPurchaseAmount", config.MinPurchaseAmount)
            .WithStringList("ExcludedCategories", config.ExcludedCategories)
            .Build();

        await PutItemAsync(item, cancellationToken: cancellationToken);
    }

    // ─── SMS Config ────────────────────────────────────────────────────────────

    /// <summary>
    /// Gets the SMS gateway configuration.
    /// </summary>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>The SMS config, or null if not configured.</returns>
    public async Task<SmsConfig?> GetSmsConfigAsync(CancellationToken cancellationToken = default)
    {
        var item = await GetItemAsync(
            KeyBuilder.SmsConfigPk(),
            KeyBuilder.SmsConfigSk(),
            cancellationToken: cancellationToken);

        return item is null ? null : MapToSmsConfig(item);
    }

    /// <summary>
    /// Creates or updates the SMS gateway configuration.
    /// </summary>
    /// <param name="config">The SMS config to store.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    public async Task PutSmsConfigAsync(SmsConfig config, CancellationToken cancellationToken = default)
    {
        var item = AttributeValueSerializer.NewItem(
                KeyBuilder.SmsConfigPk(),
                KeyBuilder.SmsConfigSk())
            .WithBool("Enabled", config.Enabled)
            .WithString("BaseUrl", config.BaseUrl)
            .WithString("ApiKey", config.ApiKey)
            .WithString("SenderId", config.SenderId)
            .Build();

        await PutItemAsync(item, cancellationToken: cancellationToken);
    }

    // ─── Brand Config ─────────────────────────────────────────────────────────

    /// <summary>
    /// Gets the brand/theming configuration.
    /// </summary>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>The brand config, or null if not configured.</returns>
    public async Task<BrandConfig?> GetBrandConfigAsync(CancellationToken cancellationToken = default)
    {
        var item = await GetItemAsync(
            KeyBuilder.BrandConfigPk(),
            KeyBuilder.BrandConfigSk(),
            cancellationToken: cancellationToken);

        return item is null ? null : MapToBrandConfig(item);
    }

    /// <summary>
    /// Creates or updates the brand/theming configuration.
    /// </summary>
    /// <param name="config">The brand config to store.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    public async Task PutBrandConfigAsync(BrandConfig config, CancellationToken cancellationToken = default)
    {
        var item = AttributeValueSerializer.NewItem(
                KeyBuilder.BrandConfigPk(),
                KeyBuilder.BrandConfigSk())
            .WithString("CompanyName", config.CompanyName)
            .WithString("PrimaryColor", config.PrimaryColor)
            .WithString("SecondaryColor", config.SecondaryColor)
            .WithString("AccentColor", config.AccentColor)
            .WithString("LogoUrl", config.LogoUrl)
            .WithString("FaviconUrl", config.FaviconUrl)
            .Build();

        await PutItemAsync(item, cancellationToken: cancellationToken);
    }

    // ─── Mapping Helpers ────────────────────────────────────────────────────────

    private static LoyaltyCycle MapToLoyaltyCycle(Dictionary<string, AttributeValue> item) =>
        new(
            CycleId: AttributeValueSerializer.GetRequiredString(item, "CycleId"),
            StartDate: AttributeValueSerializer.GetDateOnly(item, "StartDate"),
            EndDate: AttributeValueSerializer.GetDateOnly(item, "EndDate"),
            IsActive: AttributeValueSerializer.GetBool(item, "IsActive")
        );

    private static PurchaseThreshold MapToPurchaseThreshold(Dictionary<string, AttributeValue> item) =>
        new(
            Tier: AttributeValueSerializer.GetInt(item, "Tier"),
            RequiredPurchases: AttributeValueSerializer.GetInt(item, "RequiredPurchases"),
            GiftType: AttributeValueSerializer.GetRequiredString(item, "GiftType"),
            GiftDescription: AttributeValueSerializer.GetRequiredString(item, "GiftDescription"),
            GiftValue: AttributeValueSerializer.GetDecimal(item, "GiftValue"),
            GiftValueType: AttributeValueSerializer.GetString(item, "GiftValueType") ?? "fixed",
            IsEnabled: AttributeValueSerializer.GetBool(item, "IsEnabled"),
            MinPurchaseAmount: AttributeValueSerializer.GetDecimal(item, "MinPurchaseAmount"),
            ExcludedCategories: AttributeValueSerializer.GetStringList(item, "ExcludedCategories")
        );

    private static GeneralConfig MapToGeneralConfig(Dictionary<string, AttributeValue> item) =>
        new(
            SyncIntervalMinutes: AttributeValueSerializer.GetInt(item, "SyncIntervalMinutes"),
            CodeExpiryDays: AttributeValueSerializer.GetInt(item, "CodeExpiryDays"),
            MinPurchaseAmount: AttributeValueSerializer.GetDecimal(item, "MinPurchaseAmount"),
            ExcludedCategories: AttributeValueSerializer.GetStringList(item, "ExcludedCategories")
        );

    private static SmsConfig MapToSmsConfig(Dictionary<string, AttributeValue> item) =>
        new(
            Enabled: AttributeValueSerializer.GetBool(item, "Enabled"),
            BaseUrl: AttributeValueSerializer.GetRequiredString(item, "BaseUrl"),
            ApiKey: AttributeValueSerializer.GetRequiredString(item, "ApiKey"),
            SenderId: AttributeValueSerializer.GetRequiredString(item, "SenderId")
        );

    private static BrandConfig MapToBrandConfig(Dictionary<string, AttributeValue> item) =>
        new(
            CompanyName: AttributeValueSerializer.GetString(item, "CompanyName") ?? "Vision Emporium",
            PrimaryColor: AttributeValueSerializer.GetString(item, "PrimaryColor") ?? "#E31E24",
            SecondaryColor: AttributeValueSerializer.GetString(item, "SecondaryColor") ?? "#1a1a1a",
            AccentColor: AttributeValueSerializer.GetString(item, "AccentColor") ?? "#D6E4F0",
            LogoUrl: AttributeValueSerializer.GetString(item, "LogoUrl") ?? "",
            FaviconUrl: AttributeValueSerializer.GetString(item, "FaviconUrl") ?? ""
        );
}
