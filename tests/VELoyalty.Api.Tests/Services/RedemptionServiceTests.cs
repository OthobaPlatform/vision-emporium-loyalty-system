using Moq;
using Xunit;
using VELoyalty.Api.Services;
using VELoyalty.Core;
using VELoyalty.Data;
using VELoyalty.Data.Repositories;
using Amazon.DynamoDBv2;
using Amazon.DynamoDBv2.Model;

namespace VELoyalty.Api.Tests.Services;

public class RedemptionServiceTests
{
    private readonly Mock<IAmazonDynamoDB> _mockDynamoDb;
    private readonly DynamoDbContext _context;
    private readonly VerificationCodeRepository _verificationCodeRepo;
    private readonly RedemptionRepository _redemptionRepo;
    private readonly RateLimitRepository _rateLimitRepo;
    private readonly OutletRepository _outletRepo;
    private readonly AuditRepository _auditRepo;
    private readonly RedemptionService _service;

    public RedemptionServiceTests()
    {
        _mockDynamoDb = new Mock<IAmazonDynamoDB>();
        _context = new DynamoDbContext(_mockDynamoDb.Object, "TestTable");
        _verificationCodeRepo = new VerificationCodeRepository(_context);
        _redemptionRepo = new RedemptionRepository(_context);
        _rateLimitRepo = new RateLimitRepository(_context);
        _outletRepo = new OutletRepository(_context);
        _auditRepo = new AuditRepository(_context);

        _service = new RedemptionService(
            _verificationCodeRepo,
            _redemptionRepo,
            _rateLimitRepo,
            _outletRepo,
            _auditRepo);
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("   ")]
    [InlineData("12345")]      // Too short
    [InlineData("1234567")]    // Too long
    [InlineData("abcdef")]     // Non-numeric
    [InlineData("12ab56")]     // Mixed
    [InlineData("12345 ")]     // Contains space
    public async Task VerifyAndRedeem_InvalidCodeFormat_ReturnsInvalid(string? code)
    {
        var request = new RedemptionVerifyRequest(code!, "outlet-1", "staff-1");

        var result = await _service.VerifyAndRedeemAsync(request);

        Assert.False(result.IsSuccess);
        Assert.Equal("InvalidCode", result.ErrorType);
        Assert.Contains("6 digits", result.Message);
    }

    [Fact]
    public async Task VerifyAndRedeem_ValidFormatButCodeNotFound_ReturnsInvalid()
    {
        // Setup: rate limit check returns not blocked (GetItem returns null)
        _mockDynamoDb.Setup(x => x.GetItemAsync(It.IsAny<GetItemRequest>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(new GetItemResponse { Item = new Dictionary<string, AttributeValue>() });

        // Setup: GSI2 query for code lookup returns empty
        _mockDynamoDb.Setup(x => x.QueryAsync(It.IsAny<QueryRequest>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(new QueryResponse { Items = new List<Dictionary<string, AttributeValue>>() });

        // Setup: rate limit increment (UpdateItem)
        _mockDynamoDb.Setup(x => x.UpdateItemAsync(It.IsAny<UpdateItemRequest>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(new UpdateItemResponse
            {
                Attributes = new Dictionary<string, AttributeValue>
                {
                    ["Attempts"] = new AttributeValue { N = "1" }
                }
            });

        var request = new RedemptionVerifyRequest("123456", "outlet-1", "staff-1");

        var result = await _service.VerifyAndRedeemAsync(request);

        Assert.False(result.IsSuccess);
        Assert.Equal("InvalidCode", result.ErrorType);
        Assert.Contains("invalid", result.Message);
    }

    [Fact]
    public async Task VerifyAndRedeem_RateLimited_Returns429()
    {
        // Setup: rate limit check returns blocked
        var blockedUntil = DateTime.UtcNow.AddMinutes(25);
        _mockDynamoDb.Setup(x => x.GetItemAsync(It.IsAny<GetItemRequest>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(new GetItemResponse
            {
                Item = new Dictionary<string, AttributeValue>
                {
                    ["PK"] = new AttributeValue { S = "RATELIMIT#123456" },
                    ["SK"] = new AttributeValue { S = "WINDOW#2024-01-01T00:00:00Z" },
                    ["Attempts"] = new AttributeValue { N = "6" },
                    ["BlockedUntil"] = new AttributeValue { S = blockedUntil.ToString("O") }
                }
            });

        var request = new RedemptionVerifyRequest("123456", "outlet-1", "staff-1");

        var result = await _service.VerifyAndRedeemAsync(request);

        Assert.False(result.IsSuccess);
        Assert.Equal("RateLimited", result.ErrorType);
        Assert.Contains("Too many failed attempts", result.Message);
        Assert.NotNull(result.RetryAfter);
    }

    [Fact]
    public async Task VerifyAndRedeem_ExpiredCode_ReturnsExpired()
    {
        var expiredAt = DateTime.UtcNow.AddDays(-1);

        // Setup: rate limit check returns not blocked (GetItem returns empty)
        SetupNotBlocked();

        // Setup: GSI2 query for code lookup returns an expired code
        SetupCodeLookup("123456", "cust-1", "outlet-1", expiredAt, "Active");

        // Setup: rate limit increment
        SetupRateLimitIncrement(1);

        var request = new RedemptionVerifyRequest("123456", "outlet-1", "staff-1");

        var result = await _service.VerifyAndRedeemAsync(request);

        Assert.False(result.IsSuccess);
        Assert.Equal("Expired", result.ErrorType);
        Assert.Contains("expired", result.Message);
        Assert.Equal(expiredAt, result.ExpiresAt);
    }

    [Fact]
    public async Task VerifyAndRedeem_AlreadyRedeemed_ReturnsAlreadyRedeemed()
    {
        var expiresAt = DateTime.UtcNow.AddDays(10);

        // Setup: rate limit check returns not blocked
        SetupNotBlocked();

        // Setup: GSI2 query for code lookup returns a redeemed code
        SetupCodeLookup("123456", "cust-1", "outlet-1", expiresAt, "Redeemed");

        // Setup: redemption lookup by code (for getting redemption date)
        var queryCallCount = 0;
        _mockDynamoDb.Setup(x => x.QueryAsync(It.IsAny<QueryRequest>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync((QueryRequest req, CancellationToken _) =>
            {
                queryCallCount++;
                if (queryCallCount == 1)
                {
                    // First query: code lookup via GSI2 (VerificationCodeRepository)
                    return new QueryResponse
                    {
                        Items = new List<Dictionary<string, AttributeValue>>
                        {
                            CreateCodeItem("123456", "cust-1", "outlet-1", expiresAt, "Redeemed")
                        }
                    };
                }
                // Second query: redemption lookup via GSI2 (RedemptionRepository)
                return new QueryResponse
                {
                    Items = new List<Dictionary<string, AttributeValue>>
                    {
                        new()
                        {
                            ["Code"] = new AttributeValue { S = "123456" },
                            ["CustomerId"] = new AttributeValue { S = "cust-1" },
                            ["OutletId"] = new AttributeValue { S = "outlet-1" },
                            ["StaffMemberId"] = new AttributeValue { S = "staff-prev" },
                            ["GiftType"] = new AttributeValue { S = "Cash_Return" },
                            ["RedeemedAt"] = new AttributeValue { S = DateTime.UtcNow.AddDays(-2).ToString("O") }
                        }
                    }
                };
            });

        // Setup: rate limit increment
        SetupRateLimitIncrement(1);

        var request = new RedemptionVerifyRequest("123456", "outlet-1", "staff-1");

        var result = await _service.VerifyAndRedeemAsync(request);

        Assert.False(result.IsSuccess);
        Assert.Equal("AlreadyRedeemed", result.ErrorType);
        Assert.Contains("already been claimed", result.Message);
    }

    [Fact]
    public async Task VerifyAndRedeem_WrongOutlet_ReturnsWrongOutlet()
    {
        var expiresAt = DateTime.UtcNow.AddDays(10);

        // Setup: rate limit check returns not blocked
        SetupNotBlocked();

        // Setup: code is bound to outlet-2 but request is from outlet-1
        var queryCallCount = 0;
        _mockDynamoDb.Setup(x => x.QueryAsync(It.IsAny<QueryRequest>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync((QueryRequest req, CancellationToken _) =>
            {
                queryCallCount++;
                if (queryCallCount == 1)
                {
                    // Code lookup - bound to outlet-2
                    return new QueryResponse
                    {
                        Items = new List<Dictionary<string, AttributeValue>>
                        {
                            CreateCodeItem("123456", "cust-1", "outlet-2", expiresAt, "Active")
                        }
                    };
                }
                // Outlet lookup via GSI1 (not used here, but just in case)
                return new QueryResponse { Items = new List<Dictionary<string, AttributeValue>>() };
            });

        // Setup: outlet lookup by ID (GetItem for outlet-2)
        var getItemCallCount = 0;
        _mockDynamoDb.Setup(x => x.GetItemAsync(It.IsAny<GetItemRequest>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync((GetItemRequest req, CancellationToken _) =>
            {
                getItemCallCount++;
                if (getItemCallCount == 1)
                {
                    // Rate limit check - not blocked
                    return new GetItemResponse { Item = new Dictionary<string, AttributeValue>() };
                }
                // Outlet lookup
                return new GetItemResponse
                {
                    Item = new Dictionary<string, AttributeValue>
                    {
                        ["PK"] = new AttributeValue { S = "OUTLET#outlet-2" },
                        ["SK"] = new AttributeValue { S = "META" },
                        ["outletId"] = new AttributeValue { S = "outlet-2" },
                        ["name"] = new AttributeValue { S = "Dhaka Main Branch" },
                        ["address"] = new AttributeValue { S = "123 Main St" },
                        ["phoneNumber"] = new AttributeValue { S = "+8801234567890" },
                        ["assignedManagerId"] = new AttributeValue { S = "mgr-1" },
                        ["isActive"] = new AttributeValue { BOOL = true }
                    }
                };
            });

        // Setup: rate limit increment
        SetupRateLimitIncrement(1);

        var request = new RedemptionVerifyRequest("123456", "outlet-1", "staff-1");

        var result = await _service.VerifyAndRedeemAsync(request);

        Assert.False(result.IsSuccess);
        Assert.Equal("WrongOutlet", result.ErrorType);
        Assert.Contains("Dhaka Main Branch", result.Message);
        Assert.Equal("Dhaka Main Branch", result.CorrectOutletName);
    }

    [Fact]
    public async Task VerifyAndRedeem_AllChecksPass_ReturnsSuccess()
    {
        var expiresAt = DateTime.UtcNow.AddDays(10);

        // Setup: rate limit check returns not blocked
        SetupNotBlocked();

        // Setup: code lookup returns valid active code bound to outlet-1
        _mockDynamoDb.Setup(x => x.QueryAsync(It.IsAny<QueryRequest>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(new QueryResponse
            {
                Items = new List<Dictionary<string, AttributeValue>>
                {
                    CreateCodeItem("123456", "cust-1", "outlet-1", expiresAt, "Active")
                }
            });

        // Setup: UpdateItem for marking code as redeemed
        _mockDynamoDb.Setup(x => x.UpdateItemAsync(It.IsAny<UpdateItemRequest>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(new UpdateItemResponse());

        // Setup: PutItem for creating redemption record and audit entry
        _mockDynamoDb.Setup(x => x.PutItemAsync(It.IsAny<PutItemRequest>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(new PutItemResponse());

        var request = new RedemptionVerifyRequest("123456", "outlet-1", "staff-1");

        var result = await _service.VerifyAndRedeemAsync(request);

        Assert.True(result.IsSuccess);
        Assert.Equal("cust-1", result.CustomerId);
        Assert.Equal("Cash_Return", result.GiftType);
        Assert.Equal("Test Gift", result.GiftDescription);
        Assert.Equal(500.00m, result.GiftValue);
        Assert.NotNull(result.RedeemedAt);
    }

    [Theory]
    [InlineData("000000")]
    [InlineData("999999")]
    [InlineData("123456")]
    public async Task VerifyAndRedeem_ValidCodeFormats_PassFormatValidation(string code)
    {
        // These should pass format validation and proceed to rate limit check
        // Setup: rate limit check returns not blocked
        SetupNotBlocked();

        // Setup: code not found
        _mockDynamoDb.Setup(x => x.QueryAsync(It.IsAny<QueryRequest>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(new QueryResponse { Items = new List<Dictionary<string, AttributeValue>>() });

        // Setup: rate limit increment
        SetupRateLimitIncrement(1);

        var request = new RedemptionVerifyRequest(code, "outlet-1", "staff-1");

        var result = await _service.VerifyAndRedeemAsync(request);

        // Should get past format validation and fail on "code not found"
        Assert.False(result.IsSuccess);
        Assert.Equal("InvalidCode", result.ErrorType);
        Assert.Contains("invalid", result.Message);
    }

    // ─── Helper Methods ─────────────────────────────────────────────────────────

    private void SetupNotBlocked()
    {
        _mockDynamoDb.Setup(x => x.GetItemAsync(It.IsAny<GetItemRequest>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(new GetItemResponse { Item = new Dictionary<string, AttributeValue>() });
    }

    private void SetupCodeLookup(string code, string customerId, string outletId, DateTime expiresAt, string status)
    {
        _mockDynamoDb.Setup(x => x.QueryAsync(It.IsAny<QueryRequest>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(new QueryResponse
            {
                Items = new List<Dictionary<string, AttributeValue>>
                {
                    CreateCodeItem(code, customerId, outletId, expiresAt, status)
                }
            });
    }

    private void SetupRateLimitIncrement(int resultCount)
    {
        _mockDynamoDb.Setup(x => x.UpdateItemAsync(It.IsAny<UpdateItemRequest>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(new UpdateItemResponse
            {
                Attributes = new Dictionary<string, AttributeValue>
                {
                    ["Attempts"] = new AttributeValue { N = resultCount.ToString() }
                }
            });
    }

    private static Dictionary<string, AttributeValue> CreateCodeItem(
        string code, string customerId, string outletId, DateTime expiresAt, string status)
    {
        return new Dictionary<string, AttributeValue>
        {
            ["Code"] = new AttributeValue { S = code },
            ["CustomerId"] = new AttributeValue { S = customerId },
            ["OutletId"] = new AttributeValue { S = outletId },
            ["Tier"] = new AttributeValue { N = "1" },
            ["GiftType"] = new AttributeValue { S = "Cash_Return" },
            ["GiftDescription"] = new AttributeValue { S = "Test Gift" },
            ["GiftValue"] = new AttributeValue { N = "500.00" },
            ["IssuedAt"] = new AttributeValue { S = DateTime.UtcNow.AddDays(-5).ToString("O") },
            ["ExpiresAt"] = new AttributeValue { S = expiresAt.ToString("O") },
            ["Status"] = new AttributeValue { S = status }
        };
    }
}
