using System.Text.Json.Serialization;
using Amazon.DynamoDBv2;
using Amazon.Lambda.Serialization.SystemTextJson;
using VELoyalty.Auth;
using VELoyalty.Data;
using VELoyalty.Data.Repositories;

var builder = WebApplication.CreateBuilder(args);

// Register AWS services
builder.Services.AddSingleton<IAmazonDynamoDB>(sp =>
{
    var config = new AmazonDynamoDBConfig
    {
        RegionEndpoint = Amazon.RegionEndpoint.APSouth1
    };
    return new AmazonDynamoDBClient(config);
});

// Register DynamoDB context and repositories
builder.Services.AddSingleton<DynamoDbContext>(sp =>
{
    var client = sp.GetRequiredService<IAmazonDynamoDB>();
    var tableName = Environment.GetEnvironmentVariable("DYNAMODB_TABLE") ?? DynamoDbContext.TableName;
    return new DynamoDbContext(client, tableName);
});
builder.Services.AddSingleton<UserRepository>();

// Register Auth services
builder.Services.AddSingleton<IPasswordHasher, PasswordHasher>();
builder.Services.AddSingleton<IJwtTokenService>(sp =>
{
    var secret = Environment.GetEnvironmentVariable("JWT_SECRET")
        ?? throw new InvalidOperationException("JWT_SECRET environment variable is not set.");
    var expiryHoursStr = Environment.GetEnvironmentVariable("JWT_EXPIRY_HOURS");
    var expiryHours = int.TryParse(expiryHoursStr, out var hours) ? hours : VELoyalty.Core.Constants.DefaultTokenExpiryHours;
    return new JwtTokenService(secret, expiryHours);
});

// Add AWS Lambda hosting support for Native AOT with source-generated JSON serializer
builder.Services.AddAWSLambdaHosting(LambdaEventSource.HttpApi,
    new SourceGeneratorLambdaJsonSerializer<AppJsonSerializerContext>());

var app = builder.Build();

app.MapPost("/api/v1/auth/login", async (LoginRequest request, UserRepository userRepository, IPasswordHasher passwordHasher, IJwtTokenService jwtTokenService) =>
{
    // Validate request
    if (string.IsNullOrWhiteSpace(request.Email) || string.IsNullOrWhiteSpace(request.Password))
    {
        return Results.Json(
            new ErrorResponse("Unauthorized", "Invalid email or password"),
            AppJsonSerializerContext.Default.ErrorResponse,
            statusCode: 401);
    }

    // Look up user by email via GSI1
    var user = await userRepository.GetByEmailAsync(request.Email.Trim().ToLowerInvariant());

    if (user is null || !user.IsActive)
    {
        return Results.Json(
            new ErrorResponse("Unauthorized", "Invalid email or password"),
            AppJsonSerializerContext.Default.ErrorResponse,
            statusCode: 401);
    }

    // Verify password against stored bcrypt hash
    if (!passwordHasher.VerifyPassword(request.Password, user.PasswordHash))
    {
        return Results.Json(
            new ErrorResponse("Unauthorized", "Invalid email or password"),
            AppJsonSerializerContext.Default.ErrorResponse,
            statusCode: 401);
    }

    // Generate JWT token
    var authToken = jwtTokenService.GenerateToken(user.UserId, user.Role, user.OutletId);

    return Results.Json(
        new LoginResponse(authToken.Token, authToken.ExpiresAt),
        AppJsonSerializerContext.Default.LoginResponse,
        statusCode: 200);
});

app.Run();

// ─── Request/Response Models ────────────────────────────────────────────────────

/// <summary>
/// Login request body.
/// </summary>
public record LoginRequest(string Email, string Password);

/// <summary>
/// Successful login response containing the JWT token and expiration.
/// </summary>
public record LoginResponse(string Token, DateTime ExpiresAt);

/// <summary>
/// Error response body.
/// </summary>
public record ErrorResponse(string Error, string Message);

// ─── Source-Generated JSON Serializer Context for Native AOT ────────────────────

[JsonSerializable(typeof(LoginRequest))]
[JsonSerializable(typeof(LoginResponse))]
[JsonSerializable(typeof(ErrorResponse))]
internal partial class AppJsonSerializerContext : JsonSerializerContext
{
}
