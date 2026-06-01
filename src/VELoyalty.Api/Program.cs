using Amazon.Lambda.Serialization.SystemTextJson;
using VELoyalty.Api.Endpoints;
using VELoyalty.Api.Extensions;
using VELoyalty.Api.Services;

var builder = WebApplication.CreateBuilder(args);

// ─── JSON Serialization ─────────────────────────────────────────────────────────
builder.AddJsonSerialization();

// ─── Services ───────────────────────────────────────────────────────────────────
builder.Services.AddInfrastructure(builder.Configuration);
builder.Services.AddApplicationServices(builder.Configuration);
builder.Services.AddAuthServices(builder.Configuration);
builder.Services.AddApiVersioningServices();

// ─── CORS ───────────────────────────────────────────────────────────────────────
builder.Services.AddCors(options =>
{
    options.AddDefaultPolicy(policy =>
    {
        policy.AllowAnyOrigin().AllowAnyMethod().AllowAnyHeader();
    });
});

var app = builder.Build();

app.UseVELoyaltyMiddleware();
app.MapVELoyaltyEndpoints();

app.Run();

// ─── JSON Serialization Extension ───────────────────────────────────────────────
public static class WebApplicationBuilderExtensions
{
    public static WebApplicationBuilder AddJsonSerialization(this WebApplicationBuilder builder)
    {
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

        return builder;
    }
}

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
