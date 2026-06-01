using Amazon.DynamoDBv2;
using Asp.Versioning;
using VELoyalty.Api.Services;
using VELoyalty.Auth;
using VELoyalty.Data;
using VELoyalty.Data.Repositories;

namespace VELoyalty.Api.Extensions;

public static class ServiceCollectionExtensions
{
    /// <summary>
    /// Registers DynamoDB client, DynamoDbContext, and all repositories.
    /// </summary>
    public static IServiceCollection AddInfrastructure(this IServiceCollection services, IConfiguration config)
    {
        services.AddSingleton<IAmazonDynamoDB>(sp =>
        {
            var dynamoDbUrl = config["DynamoDB:ServiceURL"]
                ?? Environment.GetEnvironmentVariable("DYNAMODB_SERVICE_URL");

            if (!string.IsNullOrEmpty(dynamoDbUrl))
            {
                var dbConfig = new AmazonDynamoDBConfig { ServiceURL = dynamoDbUrl };
                return new AmazonDynamoDBClient("fakeAccessKey", "fakeSecretKey", dbConfig);
            }

            var prodConfig = new AmazonDynamoDBConfig { RegionEndpoint = Amazon.RegionEndpoint.APSouth1 };
            return new AmazonDynamoDBClient(prodConfig);
        });

        services.AddSingleton<DynamoDbContext>();

        // Repositories
        services.AddSingleton<CustomerRepository>();
        services.AddSingleton<PurchaseRepository>();
        services.AddSingleton<VerificationCodeRepository>();
        services.AddSingleton<CycleRepository>();
        services.AddSingleton<ConfigRepository>();
        services.AddSingleton<OutletRepository>();
        services.AddSingleton<UserRepository>();
        services.AddSingleton<AuditRepository>();
        services.AddSingleton<SyncJobRepository>();
        services.AddSingleton<ImportJobRepository>();
        services.AddSingleton<RedemptionRepository>();
        services.AddSingleton<RateLimitRepository>();
        services.AddSingleton<EligibilityRepository>();

        return services;
    }

    /// <summary>
    /// Registers all business/application services.
    /// </summary>
    public static IServiceCollection AddApplicationServices(this IServiceCollection services)
    {
        services.AddSingleton<CustomerService>();
        services.AddSingleton<OutletService>();
        services.AddSingleton<UserService>();
        services.AddSingleton<ConfigurationService>();
        services.AddSingleton<DashboardService>();
        services.AddSingleton<RedemptionService>();

        return services;
    }

    /// <summary>
    /// Registers JWT authentication and password hasher.
    /// </summary>
    public static IServiceCollection AddAuthServices(this IServiceCollection services, IConfiguration config)
    {
        var jwtSecret = config["JWT_SECRET"]
            ?? Environment.GetEnvironmentVariable("JWT_SECRET")
            ?? "dev-secret-key-for-local-development-only";

        services.AddVELoyaltyAuth(jwtSecret);
        services.AddSingleton<IPasswordHasher, PasswordHasher>();

        return services;
    }

    /// <summary>
    /// Registers API versioning with URL segment reader. Default version: 1.0.
    /// </summary>
    public static IServiceCollection AddApiVersioningServices(this IServiceCollection services)
    {
        services.AddApiVersioning(options =>
        {
            options.DefaultApiVersion = new ApiVersion(1, 0);
            options.AssumeDefaultVersionWhenUnspecified = true;
            options.ReportApiVersions = true;
            options.ApiVersionReader = new UrlSegmentApiVersionReader();
        });

        return services;
    }
}
