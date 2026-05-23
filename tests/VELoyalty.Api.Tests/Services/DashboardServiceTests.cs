using Moq;
using Xunit;
using VELoyalty.Api.Services;
using VELoyalty.Core;
using VELoyalty.Data;
using VELoyalty.Data.Repositories;
using Amazon.DynamoDBv2;
using Amazon.DynamoDBv2.Model;

namespace VELoyalty.Api.Tests.Services;

public class DashboardServiceTests
{
    private readonly Mock<IAmazonDynamoDB> _mockDynamoDb;
    private readonly DynamoDbContext _context;
    private readonly CustomerRepository _customerRepo;
    private readonly VerificationCodeRepository _verificationCodeRepo;
    private readonly CycleRepository _cycleRepo;
    private readonly SyncJobRepository _syncJobRepo;
    private readonly DashboardService _service;

    public DashboardServiceTests()
    {
        _mockDynamoDb = new Mock<IAmazonDynamoDB>();
        _context = new DynamoDbContext(_mockDynamoDb.Object, "TestTable");
        _customerRepo = new CustomerRepository(_context);
        _verificationCodeRepo = new VerificationCodeRepository(_context);
        _cycleRepo = new CycleRepository(_context);
        _syncJobRepo = new SyncJobRepository(_context);

        _service = new DashboardService(
            _customerRepo,
            _verificationCodeRepo,
            _cycleRepo,
            _syncJobRepo);
    }

    [Fact]
    public async Task GetDashboardSummary_NoCycleActive_ReturnsNullCycleStatus()
    {
        // Setup: no active cycle found (query returns empty)
        _mockDynamoDb.Setup(x => x.QueryAsync(It.IsAny<QueryRequest>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(new QueryResponse { Items = new List<Dictionary<string, AttributeValue>>() });

        // Setup: scan for active customers returns 0
        _mockDynamoDb.Setup(x => x.ScanAsync(It.IsAny<ScanRequest>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(new ScanResponse { Count = 0 });

        var result = await _service.GetDashboardSummaryAsync();

        Assert.NotNull(result);
        Assert.Null(result.CycleStatus);
        Assert.Equal(0, result.ActiveCustomers);
        Assert.Equal(0, result.PendingRedemptions);
    }

    [Fact]
    public async Task GetDashboardSummary_WithActiveCycle_ReturnsCycleStatusWithDaysRemaining()
    {
        var startDate = DateOnly.FromDateTime(DateTime.UtcNow.AddDays(-30));
        var endDate = DateOnly.FromDateTime(DateTime.UtcNow.AddDays(60));
        var expectedDaysRemaining = endDate.DayNumber - DateOnly.FromDateTime(DateTime.UtcNow).DayNumber;

        var queryCallCount = 0;
        _mockDynamoDb.Setup(x => x.QueryAsync(It.IsAny<QueryRequest>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync((QueryRequest req, CancellationToken _) =>
            {
                queryCallCount++;
                if (queryCallCount == 1)
                {
                    // Active cycle query
                    return new QueryResponse
                    {
                        Items = new List<Dictionary<string, AttributeValue>>
                        {
                            new()
                            {
                                ["CycleId"] = new AttributeValue { S = "cycle-2024" },
                                ["StartDate"] = new AttributeValue { S = startDate.ToString("yyyy-MM-dd") },
                                ["EndDate"] = new AttributeValue { S = endDate.ToString("yyyy-MM-dd") },
                                ["IsActive"] = new AttributeValue { BOOL = true }
                            }
                        }
                    };
                }
                // Sync job query - no recent jobs
                return new QueryResponse { Items = new List<Dictionary<string, AttributeValue>>() };
            });

        // Setup: scan for active customers and pending codes
        _mockDynamoDb.Setup(x => x.ScanAsync(It.IsAny<ScanRequest>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(new ScanResponse { Count = 5 });

        var result = await _service.GetDashboardSummaryAsync();

        Assert.NotNull(result);
        Assert.NotNull(result.CycleStatus);
        Assert.Equal("cycle-2024", result.CycleStatus.CycleId);
        Assert.Equal(startDate, result.CycleStatus.StartDate);
        Assert.Equal(endDate, result.CycleStatus.EndDate);
        Assert.Equal(expectedDaysRemaining, result.CycleStatus.DaysRemaining);
        Assert.True(result.CycleStatus.IsActive);
    }

    [Fact]
    public async Task GetDashboardSummary_WithRecentSyncJob_ReturnsSyncStatus()
    {
        var syncStartedAt = DateTime.UtcNow.AddMinutes(-5);
        var syncCompletedAt = DateTime.UtcNow.AddMinutes(-4);

        var queryCallCount = 0;
        _mockDynamoDb.Setup(x => x.QueryAsync(It.IsAny<QueryRequest>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync((QueryRequest req, CancellationToken _) =>
            {
                queryCallCount++;
                if (queryCallCount == 1)
                {
                    // Active cycle query - no active cycle
                    return new QueryResponse { Items = new List<Dictionary<string, AttributeValue>>() };
                }
                // Sync job query - returns one recent job
                return new QueryResponse
                {
                    Items = new List<Dictionary<string, AttributeValue>>
                    {
                        new()
                        {
                            ["jobId"] = new AttributeValue { S = "job-123" },
                            ["status"] = new AttributeValue { S = "Success" },
                            ["recordsFetched"] = new AttributeValue { N = "50" },
                            ["recordsStored"] = new AttributeValue { N = "48" },
                            ["recordsSkipped"] = new AttributeValue { N = "2" },
                            ["recordsRejected"] = new AttributeValue { N = "0" },
                            ["startedAt"] = new AttributeValue { S = syncStartedAt.ToString("O") },
                            ["completedAt"] = new AttributeValue { S = syncCompletedAt.ToString("O") }
                        }
                    }
                };
            });

        // Setup: scan returns 0 (no active cycle)
        _mockDynamoDb.Setup(x => x.ScanAsync(It.IsAny<ScanRequest>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(new ScanResponse { Count = 0 });

        var result = await _service.GetDashboardSummaryAsync();

        Assert.NotNull(result);
        Assert.NotNull(result.RecentSyncStatus);
        Assert.Equal("job-123", result.RecentSyncStatus.JobId);
        Assert.Equal("Success", result.RecentSyncStatus.Status);
        Assert.Equal(50, result.RecentSyncStatus.RecordsFetched);
        Assert.Equal(48, result.RecentSyncStatus.RecordsStored);
        Assert.Equal(2, result.RecentSyncStatus.RecordsSkipped);
        Assert.Equal(0, result.RecentSyncStatus.RecordsRejected);
    }

    [Fact]
    public async Task GetDashboardSummary_CycleEndedInPast_DaysRemainingIsZero()
    {
        var startDate = DateOnly.FromDateTime(DateTime.UtcNow.AddDays(-100));
        var endDate = DateOnly.FromDateTime(DateTime.UtcNow.AddDays(-5)); // Already ended

        var queryCallCount = 0;
        _mockDynamoDb.Setup(x => x.QueryAsync(It.IsAny<QueryRequest>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync((QueryRequest req, CancellationToken _) =>
            {
                queryCallCount++;
                if (queryCallCount == 1)
                {
                    return new QueryResponse
                    {
                        Items = new List<Dictionary<string, AttributeValue>>
                        {
                            new()
                            {
                                ["CycleId"] = new AttributeValue { S = "cycle-old" },
                                ["StartDate"] = new AttributeValue { S = startDate.ToString("yyyy-MM-dd") },
                                ["EndDate"] = new AttributeValue { S = endDate.ToString("yyyy-MM-dd") },
                                ["IsActive"] = new AttributeValue { BOOL = true }
                            }
                        }
                    };
                }
                return new QueryResponse { Items = new List<Dictionary<string, AttributeValue>>() };
            });

        _mockDynamoDb.Setup(x => x.ScanAsync(It.IsAny<ScanRequest>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(new ScanResponse { Count = 0 });

        var result = await _service.GetDashboardSummaryAsync();

        Assert.NotNull(result.CycleStatus);
        Assert.Equal(0, result.CycleStatus.DaysRemaining);
    }
}
