using Amazon.DynamoDBv2;
using Amazon.Lambda.Serialization.SystemTextJson;
using VELoyalty.Api.Endpoints;
using VELoyalty.Api.Services;
using VELoyalty.Auth;
using VELoyalty.Data;
using VELoyalty.Data.Repositories;

var builder = WebApplication.CreateBuilder(args);

// ─── JSON Serialization ─────────────────────────────────────────────────────────
if (builder.Environment.IsProduction())
{
    builder.Services.AddAWSLambdaHosting(LambdaEventSource.HttpApi,
        new SourceGeneratorLambdaJsonSerializer<AppJsonSerializerContext>());
}
else
{
    builder.Services.ConfigureHttpJsonOptions(options =>
    {
        options.SerializerOptions.TypeInfoResolverChain.Insert(0, AppJsonSerializerContext.Default);
        options.SerializerOptions.TypeInfoResolverChain.Add(new System.Text.Json.Serialization.Metadata.DefaultJsonTypeInfoResolver());
    });
}

// ─── DynamoDB ───────────────────────────────────────────────────────────────────
builder.Services.AddSingleton<IAmazonDynamoDB>(sp =>
{
    var dynamoDbUrl = builder.Configuration["DynamoDB:ServiceURL"]
        ?? Environment.GetEnvironmentVariable("DYNAMODB_SERVICE_URL");

    if (!string.IsNullOrEmpty(dynamoDbUrl))
    {
        var config = new AmazonDynamoDBConfig { ServiceURL = dynamoDbUrl };
        return new AmazonDynamoDBClient("fakeAccessKey", "fakeSecretKey", config);
    }

    var prodConfig = new AmazonDynamoDBConfig { RegionEndpoint = Amazon.RegionEndpoint.APSouth1 };
    return new AmazonDynamoDBClient(prodConfig);
});
builder.Services.AddSingleton<DynamoDbContext>();

// ─── Repositories ───────────────────────────────────────────────────────────────
builder.Services.AddSingleton<CustomerRepository>();
builder.Services.AddSingleton<PurchaseRepository>();
builder.Services.AddSingleton<VerificationCodeRepository>();
builder.Services.AddSingleton<CycleRepository>();
builder.Services.AddSingleton<ConfigRepository>();
builder.Services.AddSingleton<OutletRepository>();
builder.Services.AddSingleton<UserRepository>();
builder.Services.AddSingleton<AuditRepository>();
builder.Services.AddSingleton<SyncJobRepository>();
builder.Services.AddSingleton<ImportJobRepository>();
builder.Services.AddSingleton<RedemptionRepository>();
builder.Services.AddSingleton<RateLimitRepository>();
builder.Services.AddSingleton<EligibilityRepository>();

// ─── Auth ───────────────────────────────────────────────────────────────────────
var jwtSecret = builder.Configuration["JWT_SECRET"]
    ?? Environment.GetEnvironmentVariable("JWT_SECRET")
    ?? "dev-secret-key-for-local-development-only";
builder.Services.AddVELoyaltyAuth(jwtSecret);
builder.Services.AddSingleton<IPasswordHasher, PasswordHasher>();

// ─── Services ───────────────────────────────────────────────────────────────────
builder.Services.AddSingleton<CustomerService>();
builder.Services.AddSingleton<OutletService>();
builder.Services.AddSingleton<UserService>();
builder.Services.AddSingleton<ConfigurationService>();
builder.Services.AddSingleton<DashboardService>();
builder.Services.AddSingleton<RedemptionService>();

// ─── CORS ───────────────────────────────────────────────────────────────────────
builder.Services.AddCors(options =>
{
    options.AddDefaultPolicy(policy =>
    {
        policy.AllowAnyOrigin().AllowAnyMethod().AllowAnyHeader();
    });
});

var app = builder.Build();

app.UseCors();
app.UseVELoyaltyAuthorization();

// ─── Health Check ───────────────────────────────────────────────────────────────
app.MapGet("/api/v1/health", () => Results.Ok(new { Status = "Healthy", Service = "VELoyalty.Api" }));

// ─── Endpoint Groups ────────────────────────────────────────────────────────────
app.MapAuthEndpoints();
app.MapRedemptionEndpoints();
app.MapCustomerEndpoints();
app.MapOutletEndpoints();
app.MapUserEndpoints();
app.MapConfigurationEndpoints();
app.MapDashboardEndpoints();
app.MapIngestionEndpoints();

app.Run();

// ─── Source-Generated JSON Context (for Native AOT in production) ────────────────
[System.Text.Json.Serialization.JsonSerializable(typeof(object))]
[System.Text.Json.Serialization.JsonSerializable(typeof(LoginRequestDto))]
[System.Text.Json.Serialization.JsonSerializable(typeof(RedemptionCodeInput))]
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
