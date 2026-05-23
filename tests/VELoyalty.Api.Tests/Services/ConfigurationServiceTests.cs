using Moq;
using Xunit;
using VELoyalty.Api.Services;
using VELoyalty.Core;
using VELoyalty.Data;
using VELoyalty.Data.Repositories;
using Amazon.DynamoDBv2;
using Amazon.DynamoDBv2.Model;

namespace VELoyalty.Api.Tests.Services;

public class ConfigurationServiceTests
{
    private readonly Mock<IAmazonDynamoDB> _mockDynamoDb;
    private readonly DynamoDbContext _context;
    private readonly ConfigRepository _configRepository;
    private readonly CycleRepository _cycleRepository;
    private readonly AuditRepository _auditRepository;
    private readonly ConfigurationService _service;

    public ConfigurationServiceTests()
    {
        _mockDynamoDb = new Mock<IAmazonDynamoDB>();
        _context = new DynamoDbContext(_mockDynamoDb.Object, "TestTable");
        _configRepository = new ConfigRepository(_context);
        _cycleRepository = new CycleRepository(_context);
        _auditRepository = new AuditRepository(_context);
        _service = new ConfigurationService(_configRepository, _cycleRepository, _auditRepository);
    }

    // ─── GetCycleConfigAsync Tests ──────────────────────────────────────────────

    [Fact]
    public async Task GetCycleConfigAsync_ActiveCycleExists_ReturnsCycleWithDaysRemaining()
    {
        // Arrange: Query returns an active cycle
        var startDate = DateOnly.FromDateTime(DateTime.UtcNow.AddDays(-30));
        var endDate = DateOnly.FromDateTime(DateTime.UtcNow.AddDays(60));

        _mockDynamoDb.Setup(x => x.QueryAsync(It.IsAny<QueryRequest>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(new QueryResponse
            {
                Items = new List<Dictionary<string, AttributeValue>>
                {
                    CreateCycleItem("2024-2025", startDate, endDate, true)
                }
            });

        // Act
        var result = await _service.GetCycleConfigAsync();

        // Assert
        Assert.NotNull(result);
        Assert.Equal("2024-2025", result.CycleId);
        Assert.Equal(startDate, result.StartDate);
        Assert.Equal(endDate, result.EndDate);
        Assert.True(result.IsActive);
        Assert.NotNull(result.DaysRemaining);
        Assert.True(result.DaysRemaining >= 0);
    }

    [Fact]
    public async Task GetCycleConfigAsync_NoCycleExists_ReturnsNull()
    {
        // Arrange: Query returns empty
        _mockDynamoDb.Setup(x => x.QueryAsync(It.IsAny<QueryRequest>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(new QueryResponse
            {
                Items = new List<Dictionary<string, AttributeValue>>()
            });

        // Act
        var result = await _service.GetCycleConfigAsync();

        // Assert
        Assert.Null(result);
    }

    // ─── UpdateCycleConfigAsync Tests ───────────────────────────────────────────

    [Fact]
    public async Task UpdateCycleConfigAsync_ValidDates_CreatesNextCycleAndAudits()
    {
        // Arrange
        var startDate = DateOnly.FromDateTime(DateTime.UtcNow.AddDays(100));
        var endDate = startDate.AddDays(365);

        // CycleRepository.GetActiveCycleAsync returns current active cycle
        _mockDynamoDb.Setup(x => x.QueryAsync(It.IsAny<QueryRequest>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(new QueryResponse
            {
                Items = new List<Dictionary<string, AttributeValue>>
                {
                    CreateCycleItem("2024-2025", DateOnly.Parse("2024-06-01"), DateOnly.Parse("2025-05-31"), true)
                }
            });

        // GetCycleConfigAsync for the next cycle (not found)
        _mockDynamoDb.Setup(x => x.GetItemAsync(It.IsAny<GetItemRequest>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(new GetItemResponse
            {
                Item = new Dictionary<string, AttributeValue>()
            });

        // PutItem for saving cycle and audit
        _mockDynamoDb.Setup(x => x.PutItemAsync(It.IsAny<PutItemRequest>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(new PutItemResponse());

        var request = new UpdateCycleRequest(startDate, endDate);

        // Act
        var result = await _service.UpdateCycleConfigAsync(request, "admin-1");

        // Assert
        Assert.True(result.IsSuccess);
        Assert.NotNull(result.Data);

        // Verify PutItem was called at least twice (cycle + audit)
        _mockDynamoDb.Verify(x => x.PutItemAsync(It.IsAny<PutItemRequest>(), It.IsAny<CancellationToken>()), Times.AtLeast(2));
    }

    [Fact]
    public async Task UpdateCycleConfigAsync_EndDateBeforeStartDate_ReturnsValidationError()
    {
        // Arrange
        var startDate = DateOnly.Parse("2025-06-01");
        var endDate = DateOnly.Parse("2025-05-01"); // Before start

        var request = new UpdateCycleRequest(startDate, endDate);

        // Act
        var result = await _service.UpdateCycleConfigAsync(request, "admin-1");

        // Assert
        Assert.False(result.IsSuccess);
        Assert.NotNull(result.Errors);
        Assert.Contains(result.Errors, e => e.Contains("End date must be after the start date"));
    }

    [Fact]
    public async Task UpdateCycleConfigAsync_DurationTooShort_ReturnsValidationError()
    {
        // Arrange: Duration less than 30 days
        var startDate = DateOnly.Parse("2025-06-01");
        var endDate = DateOnly.Parse("2025-06-20"); // Only 19 days

        var request = new UpdateCycleRequest(startDate, endDate);

        // Act
        var result = await _service.UpdateCycleConfigAsync(request, "admin-1");

        // Assert
        Assert.False(result.IsSuccess);
        Assert.NotNull(result.Errors);
        Assert.Contains(result.Errors, e => e.Contains("at least 30 days"));
    }

    [Fact]
    public async Task UpdateCycleConfigAsync_DurationTooLong_ReturnsValidationError()
    {
        // Arrange: Duration more than 730 days
        var startDate = DateOnly.Parse("2025-06-01");
        var endDate = DateOnly.Parse("2028-06-01"); // ~1096 days

        var request = new UpdateCycleRequest(startDate, endDate);

        // Act
        var result = await _service.UpdateCycleConfigAsync(request, "admin-1");

        // Assert
        Assert.False(result.IsSuccess);
        Assert.NotNull(result.Errors);
        Assert.Contains(result.Errors, e => e.Contains("730 days"));
    }

    [Fact]
    public async Task UpdateCycleConfigAsync_NewCycleIsNotActive()
    {
        // Arrange
        var startDate = DateOnly.FromDateTime(DateTime.UtcNow.AddDays(100));
        var endDate = startDate.AddDays(365);

        _mockDynamoDb.Setup(x => x.QueryAsync(It.IsAny<QueryRequest>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(new QueryResponse
            {
                Items = new List<Dictionary<string, AttributeValue>>
                {
                    CreateCycleItem("2024-2025", DateOnly.Parse("2024-06-01"), DateOnly.Parse("2025-05-31"), true)
                }
            });

        _mockDynamoDb.Setup(x => x.GetItemAsync(It.IsAny<GetItemRequest>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(new GetItemResponse
            {
                Item = new Dictionary<string, AttributeValue>()
            });

        _mockDynamoDb.Setup(x => x.PutItemAsync(It.IsAny<PutItemRequest>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(new PutItemResponse());

        var request = new UpdateCycleRequest(startDate, endDate);

        // Act
        var result = await _service.UpdateCycleConfigAsync(request, "admin-1");

        // Assert: The new cycle should not be active (applies to next cycle only)
        Assert.True(result.IsSuccess);
        var cycleResponse = result.Data as CycleConfigResponse;
        Assert.NotNull(cycleResponse);
        Assert.False(cycleResponse.IsActive);
    }

    // ─── GetThresholdConfigsAsync Tests ─────────────────────────────────────────

    [Fact]
    public async Task GetThresholdConfigsAsync_ReturnsAllThresholds()
    {
        // Arrange
        _mockDynamoDb.Setup(x => x.QueryAsync(It.IsAny<QueryRequest>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(new QueryResponse
            {
                Items = new List<Dictionary<string, AttributeValue>>
                {
                    CreateThresholdItem(1, 3, "Cash_Return", "Cash back 500 BDT", 500m, true, 100m, new List<string>()),
                    CreateThresholdItem(2, 6, "Gift_Item", "Free headphones", 1500m, true, 100m, new List<string> { "Accessories" })
                }
            });

        // Act
        var result = await _service.GetThresholdConfigsAsync();

        // Assert
        Assert.Equal(2, result.Count);
        Assert.Equal(3, result[0].RequiredPurchases);
        Assert.Equal("Cash_Return", result[0].GiftType);
        Assert.Equal(6, result[1].RequiredPurchases);
        Assert.Equal("Gift_Item", result[1].GiftType);
        Assert.Contains("Accessories", result[1].ExcludedCategories);
    }

    [Fact]
    public async Task GetThresholdConfigsAsync_NoThresholds_ReturnsEmptyList()
    {
        // Arrange
        _mockDynamoDb.Setup(x => x.QueryAsync(It.IsAny<QueryRequest>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(new QueryResponse
            {
                Items = new List<Dictionary<string, AttributeValue>>()
            });

        // Act
        var result = await _service.GetThresholdConfigsAsync();

        // Assert
        Assert.Empty(result);
    }

    // ─── UpdateThresholdConfigsAsync Tests ──────────────────────────────────────

    [Fact]
    public async Task UpdateThresholdConfigsAsync_ValidThresholds_SavesAndAudits()
    {
        // Arrange: Existing thresholds query
        _mockDynamoDb.Setup(x => x.QueryAsync(It.IsAny<QueryRequest>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(new QueryResponse
            {
                Items = new List<Dictionary<string, AttributeValue>>()
            });

        // BatchWrite for thresholds
        _mockDynamoDb.Setup(x => x.BatchWriteItemAsync(It.IsAny<BatchWriteItemRequest>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(new BatchWriteItemResponse
            {
                UnprocessedItems = new Dictionary<string, List<WriteRequest>>()
            });

        // PutItem for audit
        _mockDynamoDb.Setup(x => x.PutItemAsync(It.IsAny<PutItemRequest>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(new PutItemResponse());

        var request = new UpdateThresholdsRequest(new List<ThresholdInput>
        {
            new(3, "Cash_Return", "Cash back 500 BDT", 500m, "fixed", true, 100m, null),
            new(6, "Gift_Item", "Free headphones", 1500m, null, true, 100m, new List<string> { "Accessories" })
        });

        // Act
        var result = await _service.UpdateThresholdConfigsAsync(request, "admin-1");

        // Assert
        Assert.True(result.IsSuccess);
        Assert.NotNull(result.Data);
    }

    [Fact]
    public async Task UpdateThresholdConfigsAsync_DuplicateValues_ReturnsValidationError()
    {
        // Arrange: Two thresholds with same RequiredPurchases value
        var request = new UpdateThresholdsRequest(new List<ThresholdInput>
        {
            new(3, "Cash_Return", "Cash back", 500m, "fixed", true, 100m, null),
            new(3, "Gift_Item", "Gift", 1000m, null, true, 100m, null) // Duplicate value 3
        });

        // Act
        var result = await _service.UpdateThresholdConfigsAsync(request, "admin-1");

        // Assert
        Assert.False(result.IsSuccess);
        Assert.NotNull(result.Errors);
        Assert.Contains(result.Errors, e => e.Contains("Duplicate"));
    }

    [Fact]
    public async Task UpdateThresholdConfigsAsync_TooManyThresholds_ReturnsValidationError()
    {
        // Arrange: 11 thresholds (max is 10)
        var thresholds = Enumerable.Range(1, 11)
            .Select(i => new ThresholdInput(i, "Cash_Return", $"Gift {i}", 100m, "fixed", true, 50m, null))
            .ToList();

        var request = new UpdateThresholdsRequest(thresholds);

        // Act
        var result = await _service.UpdateThresholdConfigsAsync(request, "admin-1");

        // Assert
        Assert.False(result.IsSuccess);
        Assert.NotNull(result.Errors);
        Assert.Contains(result.Errors, e => e.Contains("10"));
    }

    [Fact]
    public async Task UpdateThresholdConfigsAsync_ValueOutOfRange_ReturnsValidationError()
    {
        // Arrange: Threshold value > 100
        var request = new UpdateThresholdsRequest(new List<ThresholdInput>
        {
            new(101, "Cash_Return", "Cash back", 500m, "fixed", true, 100m, null)
        });

        // Act
        var result = await _service.UpdateThresholdConfigsAsync(request, "admin-1");

        // Assert
        Assert.False(result.IsSuccess);
        Assert.NotNull(result.Errors);
        Assert.Contains(result.Errors, e => e.Contains("between 1 and 100"));
    }

    [Fact]
    public async Task UpdateThresholdConfigsAsync_InvalidGiftType_ReturnsValidationError()
    {
        // Arrange: Invalid GiftType
        _mockDynamoDb.Setup(x => x.QueryAsync(It.IsAny<QueryRequest>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(new QueryResponse
            {
                Items = new List<Dictionary<string, AttributeValue>>()
            });

        var request = new UpdateThresholdsRequest(new List<ThresholdInput>
        {
            new(3, "InvalidType", "Cash back", 500m, "fixed", true, 100m, null)
        });

        // Act
        var result = await _service.UpdateThresholdConfigsAsync(request, "admin-1");

        // Assert
        Assert.False(result.IsSuccess);
        Assert.NotNull(result.Errors);
        Assert.Contains(result.Errors, e => e.Contains("GiftType"));
    }

    [Fact]
    public async Task UpdateThresholdConfigsAsync_EmptyGiftDescription_ReturnsValidationError()
    {
        // Arrange
        _mockDynamoDb.Setup(x => x.QueryAsync(It.IsAny<QueryRequest>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(new QueryResponse
            {
                Items = new List<Dictionary<string, AttributeValue>>()
            });

        var request = new UpdateThresholdsRequest(new List<ThresholdInput>
        {
            new(3, "Cash_Return", "", 500m, "fixed", true, 100m, null)
        });

        // Act
        var result = await _service.UpdateThresholdConfigsAsync(request, "admin-1");

        // Assert
        Assert.False(result.IsSuccess);
        Assert.NotNull(result.Errors);
        Assert.Contains(result.Errors, e => e.Contains("GiftDescription"));
    }

    [Fact]
    public async Task UpdateThresholdConfigsAsync_GiftValueOutOfRange_ReturnsValidationError()
    {
        // Arrange: Gift value exceeds max
        _mockDynamoDb.Setup(x => x.QueryAsync(It.IsAny<QueryRequest>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(new QueryResponse
            {
                Items = new List<Dictionary<string, AttributeValue>>()
            });

        var request = new UpdateThresholdsRequest(new List<ThresholdInput>
        {
            new(3, "Cash_Return", "Cash back", 1_000_000m, "fixed", true, 100m, null) // Exceeds 999,999.99
        });

        // Act
        var result = await _service.UpdateThresholdConfigsAsync(request, "admin-1");

        // Assert
        Assert.False(result.IsSuccess);
        Assert.NotNull(result.Errors);
        Assert.Contains(result.Errors, e => e.Contains("GiftValue"));
    }

    [Fact]
    public async Task UpdateThresholdConfigsAsync_DisabledThreshold_SavesSuccessfully()
    {
        // Arrange
        _mockDynamoDb.Setup(x => x.QueryAsync(It.IsAny<QueryRequest>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(new QueryResponse
            {
                Items = new List<Dictionary<string, AttributeValue>>()
            });

        _mockDynamoDb.Setup(x => x.BatchWriteItemAsync(It.IsAny<BatchWriteItemRequest>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(new BatchWriteItemResponse
            {
                UnprocessedItems = new Dictionary<string, List<WriteRequest>>()
            });

        _mockDynamoDb.Setup(x => x.PutItemAsync(It.IsAny<PutItemRequest>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(new PutItemResponse());

        var request = new UpdateThresholdsRequest(new List<ThresholdInput>
        {
            new(3, "Cash_Return", "Cash back 500 BDT", 500m, "fixed", false, 100m, null) // IsEnabled = false
        });

        // Act
        var result = await _service.UpdateThresholdConfigsAsync(request, "admin-1");

        // Assert
        Assert.True(result.IsSuccess);
    }

    // ─── GetGeneralConfigAsync Tests ────────────────────────────────────────────

    [Fact]
    public async Task GetGeneralConfigAsync_ConfigExists_ReturnsConfig()
    {
        // Arrange
        _mockDynamoDb.Setup(x => x.GetItemAsync(It.IsAny<GetItemRequest>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(new GetItemResponse
            {
                Item = CreateGeneralConfigItem(60, 30, 100m, new List<string> { "Accessories" })
            });

        // Act
        var result = await _service.GetGeneralConfigAsync();

        // Assert
        Assert.Equal(60, result.SyncIntervalMinutes);
        Assert.Equal(30, result.CodeExpiryDays);
        Assert.Equal(100m, result.MinPurchaseAmount);
        Assert.Contains("Accessories", result.ExcludedCategories);
    }

    [Fact]
    public async Task GetGeneralConfigAsync_NoConfigExists_ReturnsDefaults()
    {
        // Arrange: GetItem returns empty (no config)
        _mockDynamoDb.Setup(x => x.GetItemAsync(It.IsAny<GetItemRequest>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(new GetItemResponse
            {
                Item = new Dictionary<string, AttributeValue>()
            });

        // Act
        var result = await _service.GetGeneralConfigAsync();

        // Assert: Should return defaults
        Assert.Equal(Constants.DefaultSyncIntervalMinutes, result.SyncIntervalMinutes);
        Assert.Equal(Constants.DefaultCodeExpiryDays, result.CodeExpiryDays);
        Assert.Equal(Constants.MinPurchaseAmount, result.MinPurchaseAmount);
        Assert.Empty(result.ExcludedCategories);
    }

    // ─── UpdateGeneralConfigAsync Tests ─────────────────────────────────────────

    [Fact]
    public async Task UpdateGeneralConfigAsync_ValidConfig_SavesAndAudits()
    {
        // Arrange
        _mockDynamoDb.Setup(x => x.GetItemAsync(It.IsAny<GetItemRequest>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(new GetItemResponse
            {
                Item = new Dictionary<string, AttributeValue>()
            });

        _mockDynamoDb.Setup(x => x.PutItemAsync(It.IsAny<PutItemRequest>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(new PutItemResponse());

        var request = new UpdateGeneralConfigRequest(
            SyncIntervalMinutes: 30,
            CodeExpiryDays: 45,
            MinPurchaseAmount: 200m,
            ExcludedCategories: new List<string> { "Accessories", "Services" }
        );

        // Act
        var result = await _service.UpdateGeneralConfigAsync(request, "admin-1");

        // Assert
        Assert.True(result.IsSuccess);
        var config = result.Data as GeneralConfigResponse;
        Assert.NotNull(config);
        Assert.Equal(30, config.SyncIntervalMinutes);
        Assert.Equal(45, config.CodeExpiryDays);
        Assert.Equal(200m, config.MinPurchaseAmount);
        Assert.Equal(2, config.ExcludedCategories.Count);

        // Verify PutItem was called at least twice (config + audit)
        _mockDynamoDb.Verify(x => x.PutItemAsync(It.IsAny<PutItemRequest>(), It.IsAny<CancellationToken>()), Times.AtLeast(2));
    }

    [Fact]
    public async Task UpdateGeneralConfigAsync_SyncIntervalBelowMinimum_ReturnsValidationError()
    {
        // Arrange: Sync interval < 15 minutes
        var request = new UpdateGeneralConfigRequest(
            SyncIntervalMinutes: 10,
            CodeExpiryDays: 30,
            MinPurchaseAmount: 100m,
            ExcludedCategories: null
        );

        // Act
        var result = await _service.UpdateGeneralConfigAsync(request, "admin-1");

        // Assert
        Assert.False(result.IsSuccess);
        Assert.NotNull(result.Errors);
        Assert.Contains(result.Errors, e => e.Contains("15 minutes"));
    }

    [Fact]
    public async Task UpdateGeneralConfigAsync_CodeExpiryBelowMinimum_ReturnsValidationError()
    {
        // Arrange: Code expiry < 7 days
        var request = new UpdateGeneralConfigRequest(
            SyncIntervalMinutes: 60,
            CodeExpiryDays: 5,
            MinPurchaseAmount: 100m,
            ExcludedCategories: null
        );

        // Act
        var result = await _service.UpdateGeneralConfigAsync(request, "admin-1");

        // Assert
        Assert.False(result.IsSuccess);
        Assert.NotNull(result.Errors);
        Assert.Contains(result.Errors, e => e.Contains("between 7 and 90"));
    }

    [Fact]
    public async Task UpdateGeneralConfigAsync_CodeExpiryAboveMaximum_ReturnsValidationError()
    {
        // Arrange: Code expiry > 90 days
        var request = new UpdateGeneralConfigRequest(
            SyncIntervalMinutes: 60,
            CodeExpiryDays: 100,
            MinPurchaseAmount: 100m,
            ExcludedCategories: null
        );

        // Act
        var result = await _service.UpdateGeneralConfigAsync(request, "admin-1");

        // Assert
        Assert.False(result.IsSuccess);
        Assert.NotNull(result.Errors);
        Assert.Contains(result.Errors, e => e.Contains("between 7 and 90"));
    }

    [Fact]
    public async Task UpdateGeneralConfigAsync_MinPurchaseAmountBelowMinimum_ReturnsValidationError()
    {
        // Arrange: Min purchase amount < 0.01
        var request = new UpdateGeneralConfigRequest(
            SyncIntervalMinutes: 60,
            CodeExpiryDays: 30,
            MinPurchaseAmount: 0m,
            ExcludedCategories: null
        );

        // Act
        var result = await _service.UpdateGeneralConfigAsync(request, "admin-1");

        // Assert
        Assert.False(result.IsSuccess);
        Assert.NotNull(result.Errors);
        Assert.Contains(result.Errors, e => e.Contains("Minimum purchase amount"));
    }

    [Fact]
    public async Task UpdateGeneralConfigAsync_NullExcludedCategories_DefaultsToEmptyList()
    {
        // Arrange
        _mockDynamoDb.Setup(x => x.GetItemAsync(It.IsAny<GetItemRequest>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(new GetItemResponse
            {
                Item = new Dictionary<string, AttributeValue>()
            });

        _mockDynamoDb.Setup(x => x.PutItemAsync(It.IsAny<PutItemRequest>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(new PutItemResponse());

        var request = new UpdateGeneralConfigRequest(
            SyncIntervalMinutes: 60,
            CodeExpiryDays: 30,
            MinPurchaseAmount: 100m,
            ExcludedCategories: null
        );

        // Act
        var result = await _service.UpdateGeneralConfigAsync(request, "admin-1");

        // Assert
        Assert.True(result.IsSuccess);
        var config = result.Data as GeneralConfigResponse;
        Assert.NotNull(config);
        Assert.Empty(config.ExcludedCategories);
    }

    // ─── Helper Methods ─────────────────────────────────────────────────────────

    private static Dictionary<string, AttributeValue> CreateCycleItem(
        string cycleId, DateOnly startDate, DateOnly endDate, bool isActive)
    {
        return new Dictionary<string, AttributeValue>
        {
            ["PK"] = new AttributeValue { S = "CONFIG" },
            ["SK"] = new AttributeValue { S = $"CYCLE#{cycleId}" },
            ["CycleId"] = new AttributeValue { S = cycleId },
            ["StartDate"] = new AttributeValue { S = startDate.ToString("yyyy-MM-dd") },
            ["EndDate"] = new AttributeValue { S = endDate.ToString("yyyy-MM-dd") },
            ["IsActive"] = new AttributeValue { BOOL = isActive }
        };
    }

    private static Dictionary<string, AttributeValue> CreateThresholdItem(
        int tier, int requiredPurchases, string giftType, string giftDescription,
        decimal giftValue, bool isEnabled, decimal minPurchaseAmount, List<string> excludedCategories)
    {
        var item = new Dictionary<string, AttributeValue>
        {
            ["PK"] = new AttributeValue { S = "CONFIG" },
            ["SK"] = new AttributeValue { S = $"THRESH#{tier}" },
            ["Tier"] = new AttributeValue { N = tier.ToString() },
            ["RequiredPurchases"] = new AttributeValue { N = requiredPurchases.ToString() },
            ["GiftType"] = new AttributeValue { S = giftType },
            ["GiftDescription"] = new AttributeValue { S = giftDescription },
            ["GiftValue"] = new AttributeValue { N = giftValue.ToString() },
            ["IsEnabled"] = new AttributeValue { BOOL = isEnabled },
            ["MinPurchaseAmount"] = new AttributeValue { N = minPurchaseAmount.ToString() },
            ["ExcludedCategories"] = new AttributeValue
            {
                L = excludedCategories.Select(c => new AttributeValue { S = c }).ToList()
            }
        };

        return item;
    }

    private static Dictionary<string, AttributeValue> CreateGeneralConfigItem(
        int syncInterval, int codeExpiry, decimal minPurchaseAmount, List<string> excludedCategories)
    {
        return new Dictionary<string, AttributeValue>
        {
            ["PK"] = new AttributeValue { S = "CONFIG" },
            ["SK"] = new AttributeValue { S = "SETTINGS#GENERAL" },
            ["SyncIntervalMinutes"] = new AttributeValue { N = syncInterval.ToString() },
            ["CodeExpiryDays"] = new AttributeValue { N = codeExpiry.ToString() },
            ["MinPurchaseAmount"] = new AttributeValue { N = minPurchaseAmount.ToString() },
            ["ExcludedCategories"] = new AttributeValue
            {
                L = excludedCategories.Select(c => new AttributeValue { S = c }).ToList()
            }
        };
    }
}
