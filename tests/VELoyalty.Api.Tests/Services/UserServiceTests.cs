using Moq;
using Xunit;
using VELoyalty.Api.Services;
using VELoyalty.Auth;
using VELoyalty.Core;
using VELoyalty.Data;
using VELoyalty.Data.Repositories;
using Amazon.DynamoDBv2;
using Amazon.DynamoDBv2.Model;

namespace VELoyalty.Api.Tests.Services;

public class UserServiceTests
{
    private readonly Mock<IAmazonDynamoDB> _mockDynamoDb;
    private readonly DynamoDbContext _context;
    private readonly UserRepository _userRepository;
    private readonly Mock<IPasswordHasher> _mockPasswordHasher;
    private readonly UserService _service;

    public UserServiceTests()
    {
        _mockDynamoDb = new Mock<IAmazonDynamoDB>();
        _context = new DynamoDbContext(_mockDynamoDb.Object, "TestTable");
        _userRepository = new UserRepository(_context);
        _mockPasswordHasher = new Mock<IPasswordHasher>();

        // Default: hash returns a predictable value
        _mockPasswordHasher.Setup(x => x.HashPassword(It.IsAny<string>()))
            .Returns("$2a$12$hashedpassword");

        _service = new UserService(_userRepository, _mockPasswordHasher.Object);
    }

    // ─── ListUsersAsync Tests ───────────────────────────────────────────────────

    [Fact]
    public async Task ListUsersAsync_ReturnsAllUsers_WithoutPasswordHashes()
    {
        // Setup: GSI1 query returns two users
        _mockDynamoDb.Setup(x => x.QueryAsync(It.IsAny<QueryRequest>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(new QueryResponse
            {
                Items = new List<Dictionary<string, AttributeValue>>
                {
                    CreateUserItem("user-1", "admin@test.com", "Admin User", "Admin", null),
                    CreateUserItem("user-2", "manager@test.com", "Manager User", "Outlet_Manager", "outlet-1")
                }
            });

        var result = await _service.ListUsersAsync();

        Assert.Equal(2, result.Count);
        Assert.Equal("user-1", result[0].UserId);
        Assert.Equal("admin@test.com", result[0].Email);
        Assert.Equal("Admin", result[0].Role);
        Assert.Null(result[0].OutletId);
        Assert.Equal("user-2", result[1].UserId);
        Assert.Equal("Outlet_Manager", result[1].Role);
        Assert.Equal("outlet-1", result[1].OutletId);
    }

    [Fact]
    public async Task ListUsersAsync_EmptyTable_ReturnsEmptyList()
    {
        _mockDynamoDb.Setup(x => x.QueryAsync(It.IsAny<QueryRequest>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(new QueryResponse
            {
                Items = new List<Dictionary<string, AttributeValue>>()
            });

        var result = await _service.ListUsersAsync();

        Assert.Empty(result);
    }

    // ─── CreateUserAsync Tests ──────────────────────────────────────────────────

    [Fact]
    public async Task CreateUserAsync_ValidAdminRequest_CreatesUserSuccessfully()
    {
        // Setup: email lookup returns no existing user
        _mockDynamoDb.Setup(x => x.QueryAsync(It.IsAny<QueryRequest>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(new QueryResponse
            {
                Items = new List<Dictionary<string, AttributeValue>>()
            });

        // Setup: PutItem succeeds
        _mockDynamoDb.Setup(x => x.PutItemAsync(It.IsAny<PutItemRequest>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(new PutItemResponse());

        var request = new CreateUserRequest(
            Email: "newadmin@test.com",
            Name: "New Admin",
            Password: "SecurePass123",
            Role: "Admin",
            OutletId: null
        );

        var result = await _service.CreateUserAsync(request);

        Assert.NotNull(result.User);
        Assert.Null(result.ValidationErrors);
        Assert.Equal("newadmin@test.com", result.User.Email);
        Assert.Equal("New Admin", result.User.Name);
        Assert.Equal("Admin", result.User.Role);
        Assert.Null(result.User.OutletId);
        Assert.True(result.User.IsActive);
        _mockPasswordHasher.Verify(x => x.HashPassword("SecurePass123"), Times.Once);
    }

    [Fact]
    public async Task CreateUserAsync_ValidOutletManagerRequest_CreatesWithOutletId()
    {
        // Setup: email lookup returns no existing user
        _mockDynamoDb.Setup(x => x.QueryAsync(It.IsAny<QueryRequest>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(new QueryResponse
            {
                Items = new List<Dictionary<string, AttributeValue>>()
            });

        // Setup: PutItem succeeds
        _mockDynamoDb.Setup(x => x.PutItemAsync(It.IsAny<PutItemRequest>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(new PutItemResponse());

        var request = new CreateUserRequest(
            Email: "manager@test.com",
            Name: "Outlet Manager",
            Password: "SecurePass123",
            Role: "Outlet_Manager",
            OutletId: "outlet-1"
        );

        var result = await _service.CreateUserAsync(request);

        Assert.NotNull(result.User);
        Assert.Null(result.ValidationErrors);
        Assert.Equal("Outlet_Manager", result.User.Role);
        Assert.Equal("outlet-1", result.User.OutletId);
    }

    [Fact]
    public async Task CreateUserAsync_DuplicateEmail_ReturnsValidationError()
    {
        // Setup: email lookup returns an existing user
        _mockDynamoDb.Setup(x => x.QueryAsync(It.IsAny<QueryRequest>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(new QueryResponse
            {
                Items = new List<Dictionary<string, AttributeValue>>
                {
                    CreateUserItem("existing-user", "existing@test.com", "Existing", "Admin", null)
                }
            });

        var request = new CreateUserRequest(
            Email: "existing@test.com",
            Name: "New User",
            Password: "SecurePass123",
            Role: "Admin",
            OutletId: null
        );

        var result = await _service.CreateUserAsync(request);

        Assert.Null(result.User);
        Assert.NotNull(result.ValidationErrors);
        Assert.Contains("A user with this email already exists.", result.ValidationErrors);
    }

    [Theory]
    [InlineData("", "Name", "Password123", "Admin", null, "Email is required.")]
    [InlineData("invalid-email", "Name", "Password123", "Admin", null, "Email format is invalid.")]
    [InlineData("valid@test.com", "", "Password123", "Admin", null, "Name is required.")]
    [InlineData("valid@test.com", "Name", "", "Admin", null, "Password is required.")]
    [InlineData("valid@test.com", "Name", "short", "Admin", null, "Password must be at least 8 characters.")]
    [InlineData("valid@test.com", "Name", "Password123", "", null, "Role is required.")]
    [InlineData("valid@test.com", "Name", "Password123", "InvalidRole", null, "Role must be 'Admin' or 'Outlet_Manager'.")]
    [InlineData("valid@test.com", "Name", "Password123", "Outlet_Manager", null, "OutletId is required for Outlet_Manager role.")]
    public async Task CreateUserAsync_InvalidRequest_ReturnsValidationErrors(
        string email, string name, string password, string role, string? outletId, string expectedError)
    {
        var request = new CreateUserRequest(email, name, password, role, outletId);

        var result = await _service.CreateUserAsync(request);

        Assert.Null(result.User);
        Assert.NotNull(result.ValidationErrors);
        Assert.Contains(expectedError, result.ValidationErrors);
    }

    // ─── UpdateUserAsync Tests ──────────────────────────────────────────────────

    [Fact]
    public async Task UpdateUserAsync_UserNotFound_ReturnsNotFound()
    {
        // Setup: GetItem returns empty (user not found)
        _mockDynamoDb.Setup(x => x.GetItemAsync(It.IsAny<GetItemRequest>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(new GetItemResponse { Item = new Dictionary<string, AttributeValue>() });

        var request = new UpdateUserRequest(Name: "Updated", Role: null, OutletId: null, Password: null);

        var result = await _service.UpdateUserAsync("nonexistent-id", request);

        Assert.True(result.IsNotFound);
        Assert.Null(result.User);
    }

    [Fact]
    public async Task UpdateUserAsync_ValidUpdate_UpdatesNameAndRole()
    {
        // Setup: GetItem returns existing user
        _mockDynamoDb.Setup(x => x.GetItemAsync(It.IsAny<GetItemRequest>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(new GetItemResponse
            {
                Item = CreateUserItem("user-1", "admin@test.com", "Old Name", "Admin", null)
            });

        // Setup: PutItem succeeds
        _mockDynamoDb.Setup(x => x.PutItemAsync(It.IsAny<PutItemRequest>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(new PutItemResponse());

        var request = new UpdateUserRequest(
            Name: "New Name",
            Role: "Outlet_Manager",
            OutletId: "outlet-1",
            Password: null
        );

        var result = await _service.UpdateUserAsync("user-1", request);

        Assert.False(result.IsNotFound);
        Assert.NotNull(result.User);
        Assert.Null(result.ValidationErrors);
        Assert.Equal("New Name", result.User.Name);
        Assert.Equal("Outlet_Manager", result.User.Role);
        Assert.Equal("outlet-1", result.User.OutletId);
        // Password should not be rehashed when not provided
        _mockPasswordHasher.Verify(x => x.HashPassword(It.IsAny<string>()), Times.Never);
    }

    [Fact]
    public async Task UpdateUserAsync_WithPasswordReset_HashesNewPassword()
    {
        // Setup: GetItem returns existing user
        _mockDynamoDb.Setup(x => x.GetItemAsync(It.IsAny<GetItemRequest>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(new GetItemResponse
            {
                Item = CreateUserItem("user-1", "admin@test.com", "Admin User", "Admin", null)
            });

        // Setup: PutItem succeeds
        _mockDynamoDb.Setup(x => x.PutItemAsync(It.IsAny<PutItemRequest>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(new PutItemResponse());

        var request = new UpdateUserRequest(
            Name: null,
            Role: null,
            OutletId: null,
            Password: "NewSecurePass123"
        );

        var result = await _service.UpdateUserAsync("user-1", request);

        Assert.False(result.IsNotFound);
        Assert.NotNull(result.User);
        _mockPasswordHasher.Verify(x => x.HashPassword("NewSecurePass123"), Times.Once);
    }

    [Fact]
    public async Task UpdateUserAsync_InvalidRole_ReturnsValidationError()
    {
        // Setup: GetItem returns existing user
        _mockDynamoDb.Setup(x => x.GetItemAsync(It.IsAny<GetItemRequest>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(new GetItemResponse
            {
                Item = CreateUserItem("user-1", "admin@test.com", "Admin User", "Admin", null)
            });

        var request = new UpdateUserRequest(
            Name: null,
            Role: "SuperAdmin",
            OutletId: null,
            Password: null
        );

        var result = await _service.UpdateUserAsync("user-1", request);

        Assert.False(result.IsNotFound);
        Assert.NotNull(result.ValidationErrors);
        Assert.Contains("Role must be 'Admin' or 'Outlet_Manager'.", result.ValidationErrors);
    }

    [Fact]
    public async Task UpdateUserAsync_OutletManagerWithoutOutletId_ReturnsValidationError()
    {
        // Setup: GetItem returns existing user
        _mockDynamoDb.Setup(x => x.GetItemAsync(It.IsAny<GetItemRequest>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(new GetItemResponse
            {
                Item = CreateUserItem("user-1", "admin@test.com", "Admin User", "Admin", null)
            });

        var request = new UpdateUserRequest(
            Name: null,
            Role: "Outlet_Manager",
            OutletId: null,
            Password: null
        );

        var result = await _service.UpdateUserAsync("user-1", request);

        Assert.False(result.IsNotFound);
        Assert.NotNull(result.ValidationErrors);
        Assert.Contains("OutletId is required for Outlet_Manager role.", result.ValidationErrors);
    }

    [Fact]
    public async Task UpdateUserAsync_ShortPassword_ReturnsValidationError()
    {
        // Setup: GetItem returns existing user
        _mockDynamoDb.Setup(x => x.GetItemAsync(It.IsAny<GetItemRequest>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(new GetItemResponse
            {
                Item = CreateUserItem("user-1", "admin@test.com", "Admin User", "Admin", null)
            });

        var request = new UpdateUserRequest(
            Name: null,
            Role: null,
            OutletId: null,
            Password: "short"
        );

        var result = await _service.UpdateUserAsync("user-1", request);

        Assert.False(result.IsNotFound);
        Assert.NotNull(result.ValidationErrors);
        Assert.Contains("Password must be at least 8 characters.", result.ValidationErrors);
    }

    // ─── Helper Methods ─────────────────────────────────────────────────────────

    private static Dictionary<string, AttributeValue> CreateUserItem(
        string userId, string email, string name, string role, string? outletId)
    {
        var item = new Dictionary<string, AttributeValue>
        {
            ["PK"] = new AttributeValue { S = $"USER#{userId}" },
            ["SK"] = new AttributeValue { S = "META" },
            ["GSI1PK"] = new AttributeValue { S = "GSI1_USER" },
            ["GSI1SK"] = new AttributeValue { S = $"USER#{email}" },
            ["userId"] = new AttributeValue { S = userId },
            ["email"] = new AttributeValue { S = email },
            ["name"] = new AttributeValue { S = name },
            ["passwordHash"] = new AttributeValue { S = "$2a$12$hashedpassword" },
            ["role"] = new AttributeValue { S = role },
            ["isActive"] = new AttributeValue { BOOL = true },
            ["createdAt"] = new AttributeValue { S = DateTime.UtcNow.AddDays(-10).ToString("O") },
            ["updatedAt"] = new AttributeValue { S = DateTime.UtcNow.ToString("O") }
        };

        if (outletId is not null)
        {
            item["outletId"] = new AttributeValue { S = outletId };
        }

        return item;
    }
}
