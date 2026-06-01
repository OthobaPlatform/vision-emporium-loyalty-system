using Amazon.DynamoDBv2;
using Amazon.DynamoDBv2.Model;
using Moq;
using VELoyalty.Core;
using VELoyalty.Data.Repositories;
using Xunit;

namespace VELoyalty.Data.Tests.Repositories;

public class PurchaseRepositoryTests
{
    private readonly Mock<IAmazonDynamoDB> _mockClient;
    private readonly DynamoDbContext _context;
    private readonly PurchaseRepository _repository;

    public PurchaseRepositoryTests()
    {
        _mockClient = new Mock<IAmazonDynamoDB>();
        _context = new DynamoDbContext(_mockClient.Object, "TestTable");
        _repository = new PurchaseRepository(_context);
    }

    [Fact]
    public async Task StorePurchaseAsync_StoresWithCorrectKeysAndGsi()
    {
        // Arrange
        var purchase = new Purchase(
            CustomerId: "C001",
            OutletId: "OUT01",
            PurchaseDate: new DateOnly(2024, 7, 15),
            Amount: 5000.00m,
            ProductCategory: "Electronics",
            ProcessedAt: new DateTime(2024, 7, 15, 10, 30, 0, DateTimeKind.Utc),
            ChallanNo: "CHN-001",
            ItemId: "ITEM-01"
        );

        Dictionary<string, AttributeValue>? capturedItem = null;
        _mockClient
            .Setup(c => c.PutItemAsync(It.IsAny<PutItemRequest>(), It.IsAny<CancellationToken>()))
            .Callback<PutItemRequest, CancellationToken>((req, _) => capturedItem = req.Item)
            .ReturnsAsync(new PutItemResponse());

        // Act
        var result = await _repository.StorePurchaseAsync(purchase);

        // Assert
        Assert.True(result);
        Assert.NotNull(capturedItem);
        Assert.Equal("CUST#C001", capturedItem["PK"].S);
        Assert.Equal("PURCH#CHN-001#ITEM-01", capturedItem["SK"].S);
        Assert.Equal("OUTLET#OUT01", capturedItem["GSI1PK"].S);
        Assert.Equal("PURCH#2024-07-15", capturedItem["GSI1SK"].S);
        Assert.Equal("C001", capturedItem["CustomerId"].S);
        Assert.Equal("OUT01", capturedItem["OutletId"].S);
        Assert.Equal("2024-07-15", capturedItem["PurchaseDate"].S);
        Assert.Equal("5000.00", capturedItem["Amount"].N);
        Assert.Equal("Electronics", capturedItem["ProductCategory"].S);
        Assert.Equal("CHN-001", capturedItem["ChallanNo"].S);
    }

    [Fact]
    public async Task StorePurchaseAsync_ReturnsFalse_WhenDuplicateExists()
    {
        // Arrange
        var purchase = new Purchase(
            CustomerId: "C001",
            OutletId: "OUT01",
            PurchaseDate: new DateOnly(2024, 7, 15),
            Amount: 5000.00m,
            ProductCategory: "Electronics",
            ProcessedAt: DateTime.UtcNow,
            ChallanNo: "CHN-001"
        );

        _mockClient
            .Setup(c => c.PutItemAsync(It.IsAny<PutItemRequest>(), It.IsAny<CancellationToken>()))
            .ThrowsAsync(new ConditionalCheckFailedException("Item already exists"));

        // Act
        var result = await _repository.StorePurchaseAsync(purchase);

        // Assert
        Assert.False(result);
    }

    [Fact]
    public async Task StorePurchaseAsync_UsesConditionExpression_ForDeduplication()
    {
        // Arrange
        var purchase = new Purchase(
            CustomerId: "C001",
            OutletId: "OUT01",
            PurchaseDate: new DateOnly(2024, 7, 15),
            Amount: 1000.00m,
            ProductCategory: "Appliances",
            ProcessedAt: DateTime.UtcNow,
            ChallanNo: "CHN-002"
        );

        PutItemRequest? capturedRequest = null;
        _mockClient
            .Setup(c => c.PutItemAsync(It.IsAny<PutItemRequest>(), It.IsAny<CancellationToken>()))
            .Callback<PutItemRequest, CancellationToken>((req, _) => capturedRequest = req)
            .ReturnsAsync(new PutItemResponse());

        // Act
        await _repository.StorePurchaseAsync(purchase);

        // Assert
        Assert.NotNull(capturedRequest);
        Assert.Equal("attribute_not_exists(PK) AND attribute_not_exists(SK)", capturedRequest.ConditionExpression);
    }

    [Fact]
    public async Task ExistsByChallanAsync_ReturnsTrue_WhenItemExists()
    {
        // Arrange
        var item = new Dictionary<string, AttributeValue>
        {
            ["PK"] = new() { S = "CUST#C001" },
            ["SK"] = new() { S = "PURCH#CHN-001" }
        };

        _mockClient
            .Setup(c => c.GetItemAsync(It.IsAny<GetItemRequest>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(new GetItemResponse { Item = item });

        // Act
        var result = await _repository.ExistsByChallanAsync("C001", "CHN-001");

        // Assert
        Assert.True(result);
    }

    [Fact]
    public async Task ExistsByChallanAsync_ReturnsFalse_WhenItemDoesNotExist()
    {
        // Arrange
        _mockClient
            .Setup(c => c.GetItemAsync(It.IsAny<GetItemRequest>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(new GetItemResponse { Item = new Dictionary<string, AttributeValue>() });

        // Act
        var result = await _repository.ExistsByChallanAsync("C001", "CHN-001");

        // Assert
        Assert.False(result);
    }

    [Fact]
    public async Task GetByCustomerAndCycleAsync_QueriesWithBeginsWithAndFiltersInMemory()
    {
        // Arrange — return items with various dates; only one falls within the cycle range
        var items = new List<Dictionary<string, AttributeValue>>
        {
            new()
            {
                ["CustomerId"] = new() { S = "C001" },
                ["OutletId"] = new() { S = "OUT01" },
                ["PurchaseDate"] = new() { S = "2024-07-15" },
                ["Amount"] = new() { N = "3000.00" },
                ["ProductCategory"] = new() { S = "Electronics" },
                ["ProcessedAt"] = new() { S = "2024-07-15T10:00:00.0000000Z" },
                ["ChallanNo"] = new() { S = "CHN-001" }
            },
            new()
            {
                ["CustomerId"] = new() { S = "C001" },
                ["OutletId"] = new() { S = "OUT01" },
                ["PurchaseDate"] = new() { S = "2023-01-10" },
                ["Amount"] = new() { N = "1000.00" },
                ["ProductCategory"] = new() { S = "Appliances" },
                ["ProcessedAt"] = new() { S = "2023-01-10T08:00:00.0000000Z" },
                ["ChallanNo"] = new() { S = "CHN-OLD" }
            }
        };

        QueryRequest? capturedRequest = null;
        _mockClient
            .Setup(c => c.QueryAsync(It.IsAny<QueryRequest>(), It.IsAny<CancellationToken>()))
            .Callback<QueryRequest, CancellationToken>((req, _) => capturedRequest = req)
            .ReturnsAsync(new QueryResponse { Items = items });

        // Act
        var result = await _repository.GetByCustomerAndCycleAsync(
            "C001",
            new DateOnly(2024, 6, 1),
            new DateOnly(2025, 5, 31));

        // Assert — only the item within the date range is returned
        Assert.Single(result);
        Assert.Equal("C001", result[0].CustomerId);
        Assert.Equal(new DateOnly(2024, 7, 15), result[0].PurchaseDate);

        // Verify the query uses begins_with pattern (not BETWEEN)
        Assert.NotNull(capturedRequest);
        Assert.Equal("CUST#C001", capturedRequest.ExpressionAttributeValues[":pk"].S);
        Assert.Equal("PURCH#", capturedRequest.ExpressionAttributeValues[":skPrefix"].S);
        Assert.Contains("begins_with", capturedRequest.KeyConditionExpression);
    }

    [Fact]
    public void CountQualifyingPurchases_FiltersCorrectly()
    {
        // Arrange
        var purchases = new List<Purchase>
        {
            // Qualifies: amount >= 500 and category not excluded
            new("C001", "OUT01", new DateOnly(2024, 7, 1), 1000m, "Electronics", DateTime.UtcNow, "CHN-001"),
            // Does NOT qualify: amount below minimum
            new("C001", "OUT01", new DateOnly(2024, 7, 2), 200m, "Electronics", DateTime.UtcNow, "CHN-002"),
            // Does NOT qualify: excluded category
            new("C001", "OUT01", new DateOnly(2024, 7, 3), 5000m, "Accessories", DateTime.UtcNow, "CHN-003"),
            // Qualifies: amount >= 500 and category not excluded
            new("C001", "OUT01", new DateOnly(2024, 7, 4), 500m, "Appliances", DateTime.UtcNow, "CHN-004"),
            // Does NOT qualify: both below minimum AND excluded category
            new("C001", "OUT01", new DateOnly(2024, 7, 5), 100m, "Accessories", DateTime.UtcNow, "CHN-005"),
            // Qualifies: exactly at minimum amount
            new("C001", "OUT01", new DateOnly(2024, 7, 6), 500m, "Electronics", DateTime.UtcNow, "CHN-006"),
        };

        var excludedCategories = new List<string> { "Accessories" };
        decimal minAmount = 500m;

        // Act
        var count = PurchaseRepository.CountQualifyingPurchases(purchases, minAmount, excludedCategories);

        // Assert
        Assert.Equal(3, count);
    }

    [Fact]
    public void CountQualifyingPurchases_ReturnsZero_WhenNoPurchasesQualify()
    {
        // Arrange
        var purchases = new List<Purchase>
        {
            new("C001", "OUT01", new DateOnly(2024, 7, 1), 100m, "Electronics", DateTime.UtcNow, "CHN-001"),
            new("C001", "OUT01", new DateOnly(2024, 7, 2), 200m, "Accessories", DateTime.UtcNow, "CHN-002"),
        };

        var excludedCategories = new List<string> { "Accessories" };
        decimal minAmount = 500m;

        // Act
        var count = PurchaseRepository.CountQualifyingPurchases(purchases, minAmount, excludedCategories);

        // Assert
        Assert.Equal(0, count);
    }

    [Fact]
    public void CountQualifyingPurchases_ReturnsAll_WhenNoFiltersApply()
    {
        // Arrange
        var purchases = new List<Purchase>
        {
            new("C001", "OUT01", new DateOnly(2024, 7, 1), 1000m, "Electronics", DateTime.UtcNow, "CHN-001"),
            new("C001", "OUT01", new DateOnly(2024, 7, 2), 2000m, "Appliances", DateTime.UtcNow, "CHN-002"),
            new("C001", "OUT01", new DateOnly(2024, 7, 3), 500m, "Gadgets", DateTime.UtcNow, "CHN-003"),
        };

        var excludedCategories = new List<string>();
        decimal minAmount = 0.01m;

        // Act
        var count = PurchaseRepository.CountQualifyingPurchases(purchases, minAmount, excludedCategories);

        // Assert
        Assert.Equal(3, count);
    }

    [Fact]
    public void CountQualifyingPurchases_IsCaseInsensitive_ForExcludedCategories()
    {
        // Arrange
        var purchases = new List<Purchase>
        {
            new("C001", "OUT01", new DateOnly(2024, 7, 1), 1000m, "ACCESSORIES", DateTime.UtcNow, "CHN-001"),
            new("C001", "OUT01", new DateOnly(2024, 7, 2), 1000m, "accessories", DateTime.UtcNow, "CHN-002"),
            new("C001", "OUT01", new DateOnly(2024, 7, 3), 1000m, "Accessories", DateTime.UtcNow, "CHN-003"),
            new("C001", "OUT01", new DateOnly(2024, 7, 4), 1000m, "Electronics", DateTime.UtcNow, "CHN-004"),
        };

        var excludedCategories = new List<string> { "Accessories" };
        decimal minAmount = 0.01m;

        // Act
        var count = PurchaseRepository.CountQualifyingPurchases(purchases, minAmount, excludedCategories);

        // Assert
        Assert.Equal(1, count); // Only "Electronics" qualifies
    }

    [Fact]
    public void CountQualifyingPurchases_HandlesEmptyList()
    {
        // Arrange
        var purchases = new List<Purchase>();
        var excludedCategories = new List<string> { "Accessories" };
        decimal minAmount = 500m;

        // Act
        var count = PurchaseRepository.CountQualifyingPurchases(purchases, minAmount, excludedCategories);

        // Assert
        Assert.Equal(0, count);
    }
}
