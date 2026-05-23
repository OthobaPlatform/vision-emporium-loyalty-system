using Amazon.DynamoDBv2;
using Amazon.DynamoDBv2.Model;
using Moq;
using VELoyalty.Core;
using VELoyalty.Data.Repositories;
using Xunit;

namespace VELoyalty.Data.Tests.Repositories;

public class CustomerRepositoryTests
{
    private readonly Mock<IAmazonDynamoDB> _mockClient;
    private readonly DynamoDbContext _context;
    private readonly CustomerRepository _repository;

    public CustomerRepositoryTests()
    {
        _mockClient = new Mock<IAmazonDynamoDB>();
        _context = new DynamoDbContext(_mockClient.Object, "TestTable");
        _repository = new CustomerRepository(_context);
    }

    [Fact]
    public async Task UpsertAsync_StoresCustomerWithCorrectKeys()
    {
        // Arrange
        var customer = new Customer(
            CustomerId: "C001",
            Name: "John Doe",
            PhoneNumber: "+8801712345678",
            QualifyingPurchases: 3,
            CurrentCycleId: "2024-2025"
        );

        Dictionary<string, AttributeValue>? capturedItem = null;
        _mockClient
            .Setup(c => c.PutItemAsync(It.IsAny<PutItemRequest>(), It.IsAny<CancellationToken>()))
            .Callback<PutItemRequest, CancellationToken>((req, _) => capturedItem = req.Item)
            .ReturnsAsync(new PutItemResponse());

        // Act
        await _repository.UpsertAsync(customer);

        // Assert
        Assert.NotNull(capturedItem);
        Assert.Equal("CUST#C001", capturedItem["PK"].S);
        Assert.Equal("PROFILE", capturedItem["SK"].S);
        Assert.Equal("PHONE#+8801712345678", capturedItem["GSI1PK"].S);
        Assert.Equal("CUST#C001", capturedItem["GSI1SK"].S);
        Assert.Equal("C001", capturedItem["CustomerId"].S);
        Assert.Equal("John Doe", capturedItem["Name"].S);
        Assert.Equal("+8801712345678", capturedItem["PhoneNumber"].S);
        Assert.Equal("3", capturedItem["QualifyingPurchases"].N);
        Assert.Equal("2024-2025", capturedItem["CurrentCycleId"].S);
    }

    [Fact]
    public async Task GetByIdAsync_ReturnsCustomer_WhenExists()
    {
        // Arrange
        var item = new Dictionary<string, AttributeValue>
        {
            ["PK"] = new() { S = "CUST#C001" },
            ["SK"] = new() { S = "PROFILE" },
            ["CustomerId"] = new() { S = "C001" },
            ["Name"] = new() { S = "Jane Smith" },
            ["PhoneNumber"] = new() { S = "+8801798765432" },
            ["QualifyingPurchases"] = new() { N = "5" },
            ["CurrentCycleId"] = new() { S = "2024-2025" }
        };

        _mockClient
            .Setup(c => c.GetItemAsync(It.IsAny<GetItemRequest>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(new GetItemResponse { Item = item });

        // Act
        var result = await _repository.GetByIdAsync("C001");

        // Assert
        Assert.NotNull(result);
        Assert.Equal("C001", result.CustomerId);
        Assert.Equal("Jane Smith", result.Name);
        Assert.Equal("+8801798765432", result.PhoneNumber);
        Assert.Equal(5, result.QualifyingPurchases);
        Assert.Equal("2024-2025", result.CurrentCycleId);
    }

    [Fact]
    public async Task GetByIdAsync_ReturnsNull_WhenNotFound()
    {
        // Arrange
        _mockClient
            .Setup(c => c.GetItemAsync(It.IsAny<GetItemRequest>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(new GetItemResponse { Item = new Dictionary<string, AttributeValue>() });

        // Act
        var result = await _repository.GetByIdAsync("NONEXISTENT");

        // Assert
        Assert.Null(result);
    }

    [Fact]
    public async Task GetByPhoneAsync_QueriesGsi1WithCorrectKey()
    {
        // Arrange
        var items = new List<Dictionary<string, AttributeValue>>
        {
            new()
            {
                ["PK"] = new() { S = "CUST#C002" },
                ["SK"] = new() { S = "PROFILE" },
                ["CustomerId"] = new() { S = "C002" },
                ["Name"] = new() { S = "Alice" },
                ["PhoneNumber"] = new() { S = "+8801555000111" },
                ["QualifyingPurchases"] = new() { N = "2" },
                ["CurrentCycleId"] = new() { S = "2024-2025" }
            }
        };

        _mockClient
            .Setup(c => c.QueryAsync(It.IsAny<QueryRequest>(), It.IsAny<CancellationToken>()))
            .Callback<QueryRequest, CancellationToken>((req, _) =>
            {
                Assert.Equal("GSI1", req.IndexName);
                Assert.Contains(":gsi1pk", req.ExpressionAttributeValues.Keys);
                Assert.Equal("PHONE#+8801555000111", req.ExpressionAttributeValues[":gsi1pk"].S);
            })
            .ReturnsAsync(new QueryResponse { Items = items });

        // Act
        var result = await _repository.GetByPhoneAsync("+8801555000111");

        // Assert
        Assert.NotNull(result);
        Assert.Equal("C002", result.CustomerId);
        Assert.Equal("Alice", result.Name);
    }

    [Fact]
    public async Task GetByPhoneAsync_ReturnsNull_WhenNoMatch()
    {
        // Arrange
        _mockClient
            .Setup(c => c.QueryAsync(It.IsAny<QueryRequest>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(new QueryResponse { Items = new List<Dictionary<string, AttributeValue>>() });

        // Act
        var result = await _repository.GetByPhoneAsync("+8801999999999");

        // Assert
        Assert.Null(result);
    }

    [Fact]
    public async Task UpdateQualifyingPurchasesAsync_SendsCorrectUpdateExpression()
    {
        // Arrange
        UpdateItemRequest? capturedRequest = null;
        _mockClient
            .Setup(c => c.UpdateItemAsync(It.IsAny<UpdateItemRequest>(), It.IsAny<CancellationToken>()))
            .Callback<UpdateItemRequest, CancellationToken>((req, _) => capturedRequest = req)
            .ReturnsAsync(new UpdateItemResponse());

        // Act
        await _repository.UpdateQualifyingPurchasesAsync("C001", 7);

        // Assert
        Assert.NotNull(capturedRequest);
        Assert.Equal("CUST#C001", capturedRequest.Key["PK"].S);
        Assert.Equal("PROFILE", capturedRequest.Key["SK"].S);
        Assert.Equal("SET QualifyingPurchases = :count", capturedRequest.UpdateExpression);
        Assert.Equal("7", capturedRequest.ExpressionAttributeValues[":count"].N);
    }
}
