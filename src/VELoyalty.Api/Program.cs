using Amazon.DynamoDBv2;
using Amazon.Lambda.Serialization.SystemTextJson;
using VELoyalty.Api.Services;
using VELoyalty.Auth;
using VELoyalty.Data;
using VELoyalty.Data.Repositories;

var builder = WebApplication.CreateBuilder(args);

// Add AWS Lambda hosting support for Native AOT with source-generated JSON serializer
if (builder.Environment.IsProduction())
{
    builder.Services.AddAWSLambdaHosting(LambdaEventSource.HttpApi,
        new SourceGeneratorLambdaJsonSerializer<AppJsonSerializerContext>());
}
else
{
    // Local development: use reflection-based JSON serialization (supports anonymous types)
    builder.Services.ConfigureHttpJsonOptions(options =>
    {
        options.SerializerOptions.TypeInfoResolverChain.Insert(0, AppJsonSerializerContext.Default);
        options.SerializerOptions.TypeInfoResolverChain.Add(new System.Text.Json.Serialization.Metadata.DefaultJsonTypeInfoResolver());
    });
}

// Register DynamoDB client and context
builder.Services.AddSingleton<IAmazonDynamoDB>(sp =>
{
    var dynamoDbUrl = builder.Configuration["DynamoDB:ServiceURL"]
        ?? Environment.GetEnvironmentVariable("DYNAMODB_SERVICE_URL");

    if (!string.IsNullOrEmpty(dynamoDbUrl))
    {
        // Local development: use DynamoDB Local
        var config = new AmazonDynamoDBConfig
        {
            ServiceURL = dynamoDbUrl
        };
        return new AmazonDynamoDBClient("fakeAccessKey", "fakeSecretKey", config);
    }

    // Production: use real AWS DynamoDB
    var prodConfig = new AmazonDynamoDBConfig
    {
        RegionEndpoint = Amazon.RegionEndpoint.APSouth1
    };
    return new AmazonDynamoDBClient(prodConfig);
});
builder.Services.AddSingleton<DynamoDbContext>();

// Register repositories
builder.Services.AddSingleton<CustomerRepository>();
builder.Services.AddSingleton<VerificationCodeRepository>();
builder.Services.AddSingleton<CycleRepository>();
builder.Services.AddSingleton<ConfigRepository>();
builder.Services.AddSingleton<OutletRepository>();
builder.Services.AddSingleton<UserRepository>();
builder.Services.AddSingleton<AuditRepository>();
builder.Services.AddSingleton<SyncJobRepository>();

// Register auth services
var jwtSecret = builder.Configuration["JWT_SECRET"] ?? Environment.GetEnvironmentVariable("JWT_SECRET") ?? "dev-secret-key-for-local-development-only";
builder.Services.AddVELoyaltyAuth(jwtSecret);
builder.Services.AddSingleton<IPasswordHasher, PasswordHasher>();

// Register services
builder.Services.AddSingleton<CustomerService>();
builder.Services.AddSingleton<OutletService>();
builder.Services.AddSingleton<UserService>();
builder.Services.AddSingleton<ConfigurationService>();
builder.Services.AddSingleton<DashboardService>();

// Add CORS for local development
builder.Services.AddCors(options =>
{
    options.AddDefaultPolicy(policy =>
    {
        policy.AllowAnyOrigin()
              .AllowAnyMethod()
              .AllowAnyHeader();
    });
});

var app = builder.Build();

// Enable CORS
app.UseCors();

// Add authorization middleware to extract user identity from API Gateway context or JWT
app.UseVELoyaltyAuthorization();

app.MapGet("/api/v1/health", () => Results.Ok(new { Status = "Healthy", Service = "VELoyalty.Api" }));

// ─── Auth Login Endpoint (for local development - in production this is a separate Lambda) ──
app.MapPost("/api/v1/auth/login", async (
    HttpContext httpContext,
    UserRepository userRepository,
    IPasswordHasher passwordHasher,
    IJwtTokenService jwtTokenService) =>
{
    var request = await httpContext.Request.ReadFromJsonAsync<LoginRequestDto>();
    if (request is null || string.IsNullOrWhiteSpace(request.Email) || string.IsNullOrWhiteSpace(request.Password))
    {
        return Results.Json(new { error = "Unauthorized", message = "Invalid email or password" }, statusCode: 401);
    }

    var user = await userRepository.GetByEmailAsync(request.Email.Trim().ToLowerInvariant());
    if (user is null || !user.IsActive)
    {
        return Results.Json(new { error = "Unauthorized", message = "Invalid email or password" }, statusCode: 401);
    }

    if (!passwordHasher.VerifyPassword(request.Password, user.PasswordHash))
    {
        return Results.Json(new { error = "Unauthorized", message = "Invalid email or password" }, statusCode: 401);
    }

    var authToken = jwtTokenService.GenerateToken(user.UserId, user.Role, user.OutletId);
    return Results.Ok(new { token = authToken.Token, expiresAt = authToken.ExpiresAt });
});

// ─── Redemption Search Endpoint ─────────────────────────────────────────────────
app.MapGet("/api/v1/redemptions/search", async (
    string? phone,
    string? code,
    HttpContext httpContext,
    CustomerService customerService,
    CancellationToken cancellationToken) =>
{
    if (string.IsNullOrWhiteSpace(phone) && string.IsNullOrWhiteSpace(code))
    {
        return Results.BadRequest(new
        {
            error = "ValidationError",
            message = "Either 'phone' or 'code' query parameter is required."
        });
    }

    var results = await customerService.SearchRedemptionsAsync(phone, code, cancellationToken);

    // Outlet_Manager: filter results to their assigned outlet
    if (httpContext.IsOutletManager())
    {
        var userOutletId = httpContext.GetUserOutletId();
        if (!string.IsNullOrWhiteSpace(userOutletId))
        {
            results = results.Where(r => string.Equals(r.OutletId, userOutletId, StringComparison.Ordinal)).ToList();
        }
    }

    return Results.Ok(new { results });
}).RequireAnyRole();

// ─── Customer Profile Endpoint ──────────────────────────────────────────────────
app.MapGet("/api/v1/customers/{phone}", async (
    string phone,
    CustomerService customerService,
    CancellationToken cancellationToken) =>
{
    var profile = await customerService.GetCustomerProfileAsync(phone, cancellationToken);

    if (profile is null)
    {
        return Results.NotFound(new
        {
            error = "NotFound",
            message = "No customer found with the specified phone number."
        });
    }

    return Results.Ok(profile);
}).RequireAnyRole();

// ─── Customer Verification Codes Endpoint ───────────────────────────────────────
app.MapGet("/api/v1/customers/{phone}/codes", async (
    string phone,
    CustomerService customerService,
    CancellationToken cancellationToken) =>
{
    var codesResponse = await customerService.GetCustomerCodesAsync(phone, cancellationToken);

    if (codesResponse is null)
    {
        return Results.NotFound(new
        {
            error = "NotFound",
            message = "No customer found with the specified phone number."
        });
    }

    return Results.Ok(codesResponse);
}).RequireAnyRole();

// ─── Outlet Management Endpoints ────────────────────────────────────────────────

// GET /api/v1/outlets - List all outlets with status
app.MapGet("/api/v1/outlets", async (
    OutletService outletService,
    CancellationToken cancellationToken) =>
{
    var outlets = await outletService.ListAllAsync(cancellationToken);
    return Results.Ok(new { outlets });
}).RequireAdmin();

// POST /api/v1/outlets - Create a new outlet
app.MapPost("/api/v1/outlets", async (
    CreateOutletRequest request,
    OutletService outletService,
    CancellationToken cancellationToken) =>
{
    // Validate required fields
    if (string.IsNullOrWhiteSpace(request.Name))
    {
        return Results.BadRequest(new
        {
            error = "ValidationError",
            message = "Outlet name is required."
        });
    }

    if (string.IsNullOrWhiteSpace(request.Address))
    {
        return Results.BadRequest(new
        {
            error = "ValidationError",
            message = "Outlet address is required."
        });
    }

    if (string.IsNullOrWhiteSpace(request.PhoneNumber))
    {
        return Results.BadRequest(new
        {
            error = "ValidationError",
            message = "Outlet phone number is required."
        });
    }

    if (string.IsNullOrWhiteSpace(request.AssignedManagerId))
    {
        return Results.BadRequest(new
        {
            error = "ValidationError",
            message = "Assigned manager ID is required."
        });
    }

    var outlet = await outletService.CreateAsync(request, cancellationToken);
    return Results.Created($"/api/v1/outlets/{outlet.OutletId}", outlet);
}).RequireAdmin();

// PUT /api/v1/outlets/{id} - Update outlet details
app.MapPut("/api/v1/outlets/{id}", async (
    string id,
    UpdateOutletRequest request,
    OutletService outletService,
    CancellationToken cancellationToken) =>
{
    // Validate required fields
    if (string.IsNullOrWhiteSpace(request.Name))
    {
        return Results.BadRequest(new
        {
            error = "ValidationError",
            message = "Outlet name is required."
        });
    }

    if (string.IsNullOrWhiteSpace(request.Address))
    {
        return Results.BadRequest(new
        {
            error = "ValidationError",
            message = "Outlet address is required."
        });
    }

    if (string.IsNullOrWhiteSpace(request.PhoneNumber))
    {
        return Results.BadRequest(new
        {
            error = "ValidationError",
            message = "Outlet phone number is required."
        });
    }

    if (string.IsNullOrWhiteSpace(request.AssignedManagerId))
    {
        return Results.BadRequest(new
        {
            error = "ValidationError",
            message = "Assigned manager ID is required."
        });
    }

    var result = await outletService.UpdateAsync(id, request, cancellationToken);

    if (result is null)
    {
        return Results.NotFound(new
        {
            error = "NotFound",
            message = "Outlet not found."
        });
    }

    return Results.Ok(result);
}).RequireAdmin();

// PATCH /api/v1/outlets/{id}/status - Activate/deactivate outlet
app.MapPatch("/api/v1/outlets/{id}/status", async (
    string id,
    UpdateOutletStatusRequest request,
    OutletService outletService,
    CancellationToken cancellationToken) =>
{
    var result = await outletService.UpdateStatusAsync(id, request.IsActive, cancellationToken);

    if (!result.IsSuccess)
    {
        return result.ErrorType switch
        {
            "NotFound" => Results.NotFound(new
            {
                error = "NotFound",
                message = result.Message
            }),
            "ValidationError" => Results.BadRequest(new
            {
                error = "ValidationError",
                message = result.Message
            }),
            _ => Results.StatusCode(500)
        };
    }

    return Results.Ok(result.Outlet);
}).RequireAdmin();

// ─── User Management Endpoints (Admin only) ────────────────────────────────────

app.MapGet("/api/v1/users", async (
    UserService userService,
    CancellationToken cancellationToken) =>
{
    var users = await userService.ListUsersAsync(cancellationToken);
    return Results.Ok(new { users });
}).RequireAdmin();

app.MapPost("/api/v1/users", async (
    CreateUserRequest request,
    UserService userService,
    CancellationToken cancellationToken) =>
{
    var result = await userService.CreateUserAsync(request, cancellationToken);

    if (result.ValidationErrors is { Count: > 0 })
    {
        return Results.BadRequest(new
        {
            error = "ValidationError",
            details = result.ValidationErrors
        });
    }

    return Results.Created($"/api/v1/users/{result.User!.UserId}", result.User);
}).RequireAdmin();

app.MapPut("/api/v1/users/{id}", async (
    string id,
    UpdateUserRequest request,
    UserService userService,
    CancellationToken cancellationToken) =>
{
    var result = await userService.UpdateUserAsync(id, request, cancellationToken);

    if (result.IsNotFound)
    {
        return Results.NotFound(new
        {
            error = "NotFound",
            message = "User not found."
        });
    }

    if (result.ValidationErrors is { Count: > 0 })
    {
        return Results.BadRequest(new
        {
            error = "ValidationError",
            details = result.ValidationErrors
        });
    }

    return Results.Ok(result.User);
}).RequireAdmin();

// ─── Configuration Management Endpoints (Admin only) ────────────────────────────

// GET /api/v1/config/cycle - Return current/active loyalty cycle config
app.MapGet("/api/v1/config/cycle", async (
    HttpContext httpContext,
    ConfigurationService configService,
    CancellationToken cancellationToken) =>
{
    var cycle = await configService.GetCycleConfigAsync(cancellationToken);

    if (cycle is null)
    {
        return Results.NotFound(new
        {
            error = "NotFound",
            message = "No active loyalty cycle configured."
        });
    }

    return Results.Ok(cycle);
}).RequireAdmin();

// PUT /api/v1/config/cycle - Update cycle config (applies to next cycle only)
app.MapPut("/api/v1/config/cycle", async (
    HttpContext httpContext,
    UpdateCycleRequest request,
    ConfigurationService configService,
    CancellationToken cancellationToken) =>
{
    var actorId = httpContext.GetUserId() ?? "system";

    var result = await configService.UpdateCycleConfigAsync(request, actorId, cancellationToken);

    if (!result.IsSuccess)
    {
        return Results.BadRequest(new
        {
            error = "ValidationError",
            details = result.Errors
        });
    }

    return Results.Ok(result.Data);
}).RequireAdmin();

// GET /api/v1/config/thresholds - Return all threshold configs
app.MapGet("/api/v1/config/thresholds", async (
    HttpContext httpContext,
    ConfigurationService configService,
    CancellationToken cancellationToken) =>
{
    var thresholds = await configService.GetThresholdConfigsAsync(cancellationToken);
    return Results.Ok(new { thresholds });
}).RequireAdmin();

// PUT /api/v1/config/thresholds - Update thresholds (applies to future purchases only)
app.MapPut("/api/v1/config/thresholds", async (
    HttpContext httpContext,
    UpdateThresholdsRequest request,
    ConfigurationService configService,
    CancellationToken cancellationToken) =>
{
    var actorId = httpContext.GetUserId() ?? "system";

    var result = await configService.UpdateThresholdConfigsAsync(request, actorId, cancellationToken);

    if (!result.IsSuccess)
    {
        return Results.BadRequest(new
        {
            error = "ValidationError",
            details = result.Errors
        });
    }

    return Results.Ok(new { thresholds = result.Data });
}).RequireAdmin();

// GET /api/v1/config/general - Return general settings
app.MapGet("/api/v1/config/general", async (
    HttpContext httpContext,
    ConfigurationService configService,
    CancellationToken cancellationToken) =>
{
    var config = await configService.GetGeneralConfigAsync(cancellationToken);
    return Results.Ok(config);
}).RequireAdmin();

// PUT /api/v1/config/general - Update general settings
app.MapPut("/api/v1/config/general", async (
    HttpContext httpContext,
    UpdateGeneralConfigRequest request,
    ConfigurationService configService,
    CancellationToken cancellationToken) =>
{
    var actorId = httpContext.GetUserId() ?? "system";

    var result = await configService.UpdateGeneralConfigAsync(request, actorId, cancellationToken);

    if (!result.IsSuccess)
    {
        return Results.BadRequest(new
        {
            error = "ValidationError",
            details = result.Errors
        });
    }

    return Results.Ok(result.Data);
}).RequireAdmin();

// ─── Audit Log Endpoint (Admin only) ────────────────────────────────────────────

// GET /api/v1/audit - Query audit records with time range and event type filters
app.MapGet("/api/v1/audit", async (
    DateTime? startDate,
    DateTime? endDate,
    string? eventType,
    AuditRepository auditRepository,
    CancellationToken cancellationToken) =>
{
    var from = startDate ?? DateTime.UtcNow.AddDays(-30);
    var to = endDate ?? DateTime.UtcNow;

    List<VELoyalty.Core.AuditEntry> entries;

    if (!string.IsNullOrWhiteSpace(eventType))
    {
        entries = await auditRepository.QueryByEventTypeAsync(
            eventType, from, to, scanIndexForward: false, limit: 100, cancellationToken: cancellationToken);
    }
    else
    {
        entries = await auditRepository.QueryByTimeRangeAsync(
            from, to, scanIndexForward: false, limit: 100, cancellationToken: cancellationToken);
    }

    var results = entries.Select(e => new AuditEntryResponse(
        Timestamp: e.Timestamp,
        EventType: e.EventType,
        ActorId: e.ActorId,
        EntityType: e.EntityType,
        EntityId: e.EntityId,
        Details: e.Details
    )).ToList();

    return Results.Ok(new { entries = results });
}).RequireAdmin();

// ─── Dashboard Endpoint (Admin only) ────────────────────────────────────────────

// GET /api/v1/dashboard - Return admin dashboard summary
app.MapGet("/api/v1/dashboard", async (
    DashboardService dashboardService,
    CancellationToken cancellationToken) =>
{
    var summary = await dashboardService.GetDashboardSummaryAsync(cancellationToken);
    return Results.Ok(summary);
}).RequireAdmin();

// ─── Ingestion Sync Endpoints (Admin only) ──────────────────────────────────────

// POST /api/v1/ingestion/sync - Trigger manual sync job
app.MapPost("/api/v1/ingestion/sync", async (
    HttpContext httpContext,
    SyncJobRepository syncJobRepository,
    AuditRepository auditRepository,
    CancellationToken cancellationToken) =>
{
    var jobId = Guid.NewGuid().ToString("N")[..12];
    var now = DateTime.UtcNow;
    var actorId = httpContext.GetUserId() ?? "admin";

    // Create a sync job record with InProgress status
    var syncJob = new VELoyalty.Core.SyncJobResult(
        JobId: jobId,
        Status: "InProgress",
        RecordsFetched: 0,
        RecordsStored: 0,
        RecordsSkipped: 0,
        RecordsRejected: 0,
        StartedAt: now,
        CompletedAt: now
    );

    await syncJobRepository.CreateAsync(syncJob, cancellationToken);

    // Log audit entry for the manual sync trigger
    var auditEntry = new VELoyalty.Core.AuditEntry(
        EventType: "IngestionJob",
        ActorId: actorId,
        EntityType: "SyncJob",
        EntityId: jobId,
        Details: new Dictionary<string, string>
        {
            ["action"] = "ManualTrigger",
            ["jobType"] = "API"
        },
        Timestamp: now
    );

    await auditRepository.AppendAsync(auditEntry, cancellationToken);

    return Results.Accepted($"/api/v1/ingestion/sync/status", new TriggerSyncResponse(
        JobId: jobId,
        Status: "InProgress",
        Message: "Sync job has been triggered successfully."
    ));
}).RequireAdmin();

// GET /api/v1/ingestion/sync/status - Return sync job history
app.MapGet("/api/v1/ingestion/sync/status", async (
    SyncJobRepository syncJobRepository,
    CancellationToken cancellationToken) =>
{
    var jobs = await syncJobRepository.ListRecentAsync(limit: 20, cancellationToken: cancellationToken);

    var results = jobs.Select(j => new SyncJobHistoryResponse(
        JobId: j.JobId,
        Status: j.Status,
        RecordsFetched: j.RecordsFetched,
        RecordsStored: j.RecordsStored,
        RecordsSkipped: j.RecordsSkipped,
        RecordsRejected: j.RecordsRejected,
        StartedAt: j.StartedAt,
        CompletedAt: j.CompletedAt
    )).ToList();

    return Results.Ok(new { jobs = results });
}).RequireAdmin();

app.Run();

/// <summary>
/// Login request DTO for local development auth endpoint.
/// </summary>
public record LoginRequestDto(string Email, string Password);

[System.Text.Json.Serialization.JsonSerializable(typeof(object))]
[System.Text.Json.Serialization.JsonSerializable(typeof(LoginRequestDto))]
[System.Text.Json.Serialization.JsonSerializable(typeof(CustomerProfileResponse))]
[System.Text.Json.Serialization.JsonSerializable(typeof(CustomerCodesResponse))]
[System.Text.Json.Serialization.JsonSerializable(typeof(ProgressInfo))]
[System.Text.Json.Serialization.JsonSerializable(typeof(VerificationCodeResponse))]
[System.Text.Json.Serialization.JsonSerializable(typeof(RedemptionSearchResult))]
[System.Text.Json.Serialization.JsonSerializable(typeof(UserResponse))]
[System.Text.Json.Serialization.JsonSerializable(typeof(CreateUserRequest))]
[System.Text.Json.Serialization.JsonSerializable(typeof(UpdateUserRequest))]
[System.Text.Json.Serialization.JsonSerializable(typeof(CreateOutletRequest))]
[System.Text.Json.Serialization.JsonSerializable(typeof(UpdateOutletRequest))]
[System.Text.Json.Serialization.JsonSerializable(typeof(UpdateOutletStatusRequest))]
[System.Text.Json.Serialization.JsonSerializable(typeof(OutletResponse))]
[System.Text.Json.Serialization.JsonSerializable(typeof(List<OutletResponse>))]
[System.Text.Json.Serialization.JsonSerializable(typeof(CycleConfigResponse))]
[System.Text.Json.Serialization.JsonSerializable(typeof(UpdateCycleRequest))]
[System.Text.Json.Serialization.JsonSerializable(typeof(ThresholdConfigResponse))]
[System.Text.Json.Serialization.JsonSerializable(typeof(List<ThresholdConfigResponse>))]
[System.Text.Json.Serialization.JsonSerializable(typeof(UpdateThresholdsRequest))]
[System.Text.Json.Serialization.JsonSerializable(typeof(ThresholdInput))]
[System.Text.Json.Serialization.JsonSerializable(typeof(List<ThresholdInput>))]
[System.Text.Json.Serialization.JsonSerializable(typeof(GeneralConfigResponse))]
[System.Text.Json.Serialization.JsonSerializable(typeof(UpdateGeneralConfigRequest))]
[System.Text.Json.Serialization.JsonSerializable(typeof(List<string>))]
[System.Text.Json.Serialization.JsonSerializable(typeof(DashboardSummaryResponse))]
[System.Text.Json.Serialization.JsonSerializable(typeof(CycleStatusResponse))]
[System.Text.Json.Serialization.JsonSerializable(typeof(SyncStatusResponse))]
[System.Text.Json.Serialization.JsonSerializable(typeof(AuditEntryResponse))]
[System.Text.Json.Serialization.JsonSerializable(typeof(List<AuditEntryResponse>))]
[System.Text.Json.Serialization.JsonSerializable(typeof(TriggerSyncResponse))]
[System.Text.Json.Serialization.JsonSerializable(typeof(SyncJobHistoryResponse))]
[System.Text.Json.Serialization.JsonSerializable(typeof(List<SyncJobHistoryResponse>))]
[System.Text.Json.Serialization.JsonSerializable(typeof(Dictionary<string, string>))]
internal partial class AppJsonSerializerContext : System.Text.Json.Serialization.JsonSerializerContext
{
}
