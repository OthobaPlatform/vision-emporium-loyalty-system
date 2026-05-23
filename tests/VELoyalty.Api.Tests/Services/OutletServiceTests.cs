using Moq;
using Xunit;
using VELoyalty.Api.Services;
using VELoyalty.Core;
using VELoyalty.Data;
using VELoyalty.Data.Repositories;
using Amazon.DynamoDBv2;
using Amazon.DynamoDBv2.Model;

namespace VELoyalty.Api.Tests.Services;

public class OutletServiceTests
{
    private readonly Mock<IAmazonDynamoDB> _mockDynamoDb;
    private readonly DynamoDbContext _context;
    private readonly OutletRepository _outletRepository;
    private readonly OutletService _service;

    public OutletServiceTests()
    {
        _mockDynamoDb = new Mock<IAmazonDynamoDB>();
        _context = new DynamoDbContext(_mockDynamoDb.Object, "TestTable");
        _outletRepository = new OutletRepository(_context);
        _service = new OutletService(_outletRepository);
    }

    // ─── ListAllAsync Tests ─────────────────────────────────────────────────────

    [Fact]
    public async Task ListAllAsync_ReturnsAllOutlets()
    {
        // Arrange: GSI1 query returns two outlets
        _mockDynamoDb.Setup(x => x.QueryAsync(It.IsAny<QueryRequest>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(new QueryResponse
            {
                Items = new List<Dictionary<string, AttributeValue>>
                {
                    CreateOutletItem("OTL-001", "Main Store", "123 Main St", "+8801711111111", "mgr-1", true),
                    CreateOutletItem("OTL-002", "Branch Store", "456 Branch Ave", "+8801722222222", "mgr-2", false)
                }
            });

        // Act
        var result = await _service.ListAllAsync();

        // Assert
        Assert.Equal(2, result.Count);
        Assert.Equal("OTL-001", result[0].OutletId);
        Assert.Equal("Main Store", result[0].Name);
        Assert.True(result[0].IsActive);
        Assert.Equal("OTL-002", result[1].OutletId);
        Assert.Equal("Branch Store", result[1].Name);
        Assert.False(result[1].IsActive);
    }

    [Fact]
    public async Task ListAllAsync_EmptyTable_ReturnsEmptyList()
    {
        // Arrange
        _mockDynamoDb.Setup(x => x.QueryAsync(It.IsAny<QueryRequest>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(new QueryResponse
            {
                Items = new List<Dictionary<string, AttributeValue>>()
            });

        // Act
        var result = await _service.ListAllAsync();

        // Assert
        Assert.Empty(result);
    }

    // ─── CreateAsync Tests ──────────────────────────────────────────────────────

    [Fact]
    public async Task CreateAsync_ValidRequest_CreatesOutletWithActiveStatus()
    {
        // Arrange
        _mockDynamoDb.Setup(x => x.PutItemAsync(It.IsAny<PutItemRequest>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(new PutItemResponse());

        var request = new CreateOutletRequest(
            Name: "New Outlet",
            Address: "789 New St",
            PhoneNumber: "+8801733333333",
            AssignedManagerId: "mgr-3"
        );

        // Act
        var result = await _service.CreateAsync(request);

        // Assert
        Assert.NotNull(result);
        Assert.NotNull(result.OutletId);
        Assert.NotEmpty(result.OutletId);
        Assert.Equal("New Outlet", result.Name);
        Assert.Equal("789 New St", result.Address);
        Assert.Equal("+8801733333333", result.PhoneNumber);
        Assert.Equal("mgr-3", result.AssignedManagerId);
        Assert.True(result.IsActive); // New outlets are always active
    }

    [Fact]
    public async Task CreateAsync_GeneratesUniqueOutletId()
    {
        // Arrange
        _mockDynamoDb.Setup(x => x.PutItemAsync(It.IsAny<PutItemRequest>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(new PutItemResponse());

        var request = new CreateOutletRequest("Store", "Address", "+8801700000000", "mgr-1");

        // Act
        var result1 = await _service.CreateAsync(request);
        var result2 = await _service.CreateAsync(request);

        // Assert: IDs should be different
        Assert.NotEqual(result1.OutletId, result2.OutletId);
    }

    // ─── UpdateAsync Tests ──────────────────────────────────────────────────────

    [Fact]
    public async Task UpdateAsync_ExistingOutlet_UpdatesDetails()
    {
        // Arrange: GetItem returns existing outlet
        _mockDynamoDb.Setup(x => x.GetItemAsync(It.IsAny<GetItemRequest>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(new GetItemResponse
            {
                Item = CreateOutletItem("OTL-001", "Old Name", "Old Address", "+8801711111111", "mgr-1", true)
            });

        _mockDynamoDb.Setup(x => x.PutItemAsync(It.IsAny<PutItemRequest>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(new PutItemResponse());

        var request = new UpdateOutletRequest(
            Name: "Updated Name",
            Address: "Updated Address",
            PhoneNumber: "+8801799999999",
            AssignedManagerId: "mgr-2"
        );

        // Act
        var result = await _service.UpdateAsync("OTL-001", request);

        // Assert
        Assert.NotNull(result);
        Assert.Equal("OTL-001", result.OutletId);
        Assert.Equal("Updated Name", result.Name);
        Assert.Equal("Updated Address", result.Address);
        Assert.Equal("+8801799999999", result.PhoneNumber);
        Assert.Equal("mgr-2", result.AssignedManagerId);
        Assert.True(result.IsActive); // Status should be preserved
    }

    [Fact]
    public async Task UpdateAsync_NonExistentOutlet_ReturnsNull()
    {
        // Arrange: GetItem returns empty (not found)
        _mockDynamoDb.Setup(x => x.GetItemAsync(It.IsAny<GetItemRequest>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(new GetItemResponse
            {
                Item = new Dictionary<string, AttributeValue>()
            });

        var request = new UpdateOutletRequest("Name", "Address", "+8801700000000", "mgr-1");

        // Act
        var result = await _service.UpdateAsync("NONEXISTENT", request);

        // Assert
        Assert.Null(result);
    }

    [Fact]
    public async Task UpdateAsync_PreservesIsActiveStatus()
    {
        // Arrange: Outlet is inactive
        _mockDynamoDb.Setup(x => x.GetItemAsync(It.IsAny<GetItemRequest>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(new GetItemResponse
            {
                Item = CreateOutletItem("OTL-001", "Store", "Address", "+8801711111111", "mgr-1", false)
            });

        _mockDynamoDb.Setup(x => x.PutItemAsync(It.IsAny<PutItemRequest>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(new PutItemResponse());

        var request = new UpdateOutletRequest("New Name", "New Address", "+8801722222222", "mgr-2");

        // Act
        var result = await _service.UpdateAsync("OTL-001", request);

        // Assert: IsActive should remain false (not changed by update)
        Assert.NotNull(result);
        Assert.False(result.IsActive);
    }

    // ─── UpdateStatusAsync Tests ────────────────────────────────────────────────

    [Fact]
    public async Task UpdateStatusAsync_DeactivateWithMultipleActive_Succeeds()
    {
        // Arrange: Outlet exists and is active
        _mockDynamoDb.Setup(x => x.GetItemAsync(It.IsAny<GetItemRequest>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(new GetItemResponse
            {
                Item = CreateOutletItem("OTL-001", "Store", "Address", "+8801711111111", "mgr-1", true)
            });

        // CountActive returns 3 (multiple active outlets)
        _mockDynamoDb.Setup(x => x.QueryAsync(It.IsAny<QueryRequest>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(new QueryResponse
            {
                Items = new List<Dictionary<string, AttributeValue>>
                {
                    CreateOutletItem("OTL-001", "Store 1", "Addr 1", "+880171", "mgr-1", true),
                    CreateOutletItem("OTL-002", "Store 2", "Addr 2", "+880172", "mgr-2", true),
                    CreateOutletItem("OTL-003", "Store 3", "Addr 3", "+880173", "mgr-3", true)
                }
            });

        // UpdateItem succeeds
        _mockDynamoDb.Setup(x => x.UpdateItemAsync(It.IsAny<UpdateItemRequest>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(new UpdateItemResponse());

        // Act
        var result = await _service.UpdateStatusAsync("OTL-001", false);

        // Assert
        Assert.True(result.IsSuccess);
        Assert.NotNull(result.Outlet);
        Assert.False(result.Outlet.IsActive);
    }

    [Fact]
    public async Task UpdateStatusAsync_DeactivateLastActive_ReturnsLastActiveOutletError()
    {
        // Arrange: Outlet exists and is active
        _mockDynamoDb.Setup(x => x.GetItemAsync(It.IsAny<GetItemRequest>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(new GetItemResponse
            {
                Item = CreateOutletItem("OTL-001", "Store", "Address", "+8801711111111", "mgr-1", true)
            });

        // CountActive returns 1 (only one active outlet - the one being deactivated)
        _mockDynamoDb.Setup(x => x.QueryAsync(It.IsAny<QueryRequest>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(new QueryResponse
            {
                Items = new List<Dictionary<string, AttributeValue>>
                {
                    CreateOutletItem("OTL-001", "Store", "Address", "+8801711111111", "mgr-1", true)
                }
            });

        // Act
        var result = await _service.UpdateStatusAsync("OTL-001", false);

        // Assert
        Assert.False(result.IsSuccess);
        Assert.Equal("ValidationError", result.ErrorType);
        Assert.Equal("At least one outlet must remain active.", result.Message);
        Assert.Null(result.Outlet);
    }

    [Fact]
    public async Task UpdateStatusAsync_ActivateOutlet_Succeeds()
    {
        // Arrange: Outlet exists and is inactive
        _mockDynamoDb.Setup(x => x.GetItemAsync(It.IsAny<GetItemRequest>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(new GetItemResponse
            {
                Item = CreateOutletItem("OTL-001", "Store", "Address", "+8801711111111", "mgr-1", false)
            });

        // UpdateItem succeeds
        _mockDynamoDb.Setup(x => x.UpdateItemAsync(It.IsAny<UpdateItemRequest>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(new UpdateItemResponse());

        // Act
        var result = await _service.UpdateStatusAsync("OTL-001", true);

        // Assert
        Assert.True(result.IsSuccess);
        Assert.NotNull(result.Outlet);
        Assert.True(result.Outlet.IsActive);
    }

    [Fact]
    public async Task UpdateStatusAsync_ActivateOutlet_DoesNotCheckActiveCount()
    {
        // Arrange: Outlet exists and is inactive
        _mockDynamoDb.Setup(x => x.GetItemAsync(It.IsAny<GetItemRequest>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(new GetItemResponse
            {
                Item = CreateOutletItem("OTL-001", "Store", "Address", "+8801711111111", "mgr-1", false)
            });

        // UpdateItem succeeds
        _mockDynamoDb.Setup(x => x.UpdateItemAsync(It.IsAny<UpdateItemRequest>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(new UpdateItemResponse());

        // Act
        var result = await _service.UpdateStatusAsync("OTL-001", true);

        // Assert: Should succeed without querying active count
        Assert.True(result.IsSuccess);
        // Verify QueryAsync was NOT called (no need to check active count when activating)
        _mockDynamoDb.Verify(x => x.QueryAsync(It.IsAny<QueryRequest>(), It.IsAny<CancellationToken>()), Times.Never);
    }

    [Fact]
    public async Task UpdateStatusAsync_NonExistentOutlet_ReturnsNotFound()
    {
        // Arrange: GetItem returns empty (not found)
        _mockDynamoDb.Setup(x => x.GetItemAsync(It.IsAny<GetItemRequest>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(new GetItemResponse
            {
                Item = new Dictionary<string, AttributeValue>()
            });

        // Act
        var result = await _service.UpdateStatusAsync("NONEXISTENT", false);

        // Assert
        Assert.False(result.IsSuccess);
        Assert.Equal("NotFound", result.ErrorType);
        Assert.Equal("Outlet not found.", result.Message);
    }

    [Fact]
    public async Task UpdateStatusAsync_DeactivateAlreadyInactive_SkipsActiveCountCheck()
    {
        // Arrange: Outlet exists but is already inactive
        _mockDynamoDb.Setup(x => x.GetItemAsync(It.IsAny<GetItemRequest>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(new GetItemResponse
            {
                Item = CreateOutletItem("OTL-001", "Store", "Address", "+8801711111111", "mgr-1", false)
            });

        // UpdateItem succeeds
        _mockDynamoDb.Setup(x => x.UpdateItemAsync(It.IsAny<UpdateItemRequest>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(new UpdateItemResponse());

        // Act
        var result = await _service.UpdateStatusAsync("OTL-001", false);

        // Assert: Should succeed without checking active count (already inactive)
        Assert.True(result.IsSuccess);
        _mockDynamoDb.Verify(x => x.QueryAsync(It.IsAny<QueryRequest>(), It.IsAny<CancellationToken>()), Times.Never);
    }

    // ─── Helper Methods ─────────────────────────────────────────────────────────

    private static Dictionary<string, AttributeValue> CreateOutletItem(
        string outletId, string name, string address, string phoneNumber, string managerId, bool isActive)
    {
        return new Dictionary<string, AttributeValue>
        {
            ["PK"] = new AttributeValue { S = $"OUTLET#{outletId}" },
            ["SK"] = new AttributeValue { S = "META" },
            ["GSI1PK"] = new AttributeValue { S = "GSI1_OUTLET" },
            ["GSI1SK"] = new AttributeValue { S = $"OUTLET#{outletId}" },
            ["outletId"] = new AttributeValue { S = outletId },
            ["name"] = new AttributeValue { S = name },
            ["address"] = new AttributeValue { S = address },
            ["phoneNumber"] = new AttributeValue { S = phoneNumber },
            ["assignedManagerId"] = new AttributeValue { S = managerId },
            ["isActive"] = new AttributeValue { BOOL = isActive }
        };
    }
}
