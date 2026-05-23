using System.Text.Json;
using System.Text.Json.Serialization;
using Amazon.Lambda.APIGatewayEvents;
using Amazon.Lambda.Core;
using Amazon.Lambda.RuntimeSupport;
using Amazon.Lambda.Serialization.SystemTextJson;
using VELoyalty.Auth;

// Bootstrap the Lambda runtime with Native AOT support
var serializer = new SourceGeneratorLambdaJsonSerializer<AuthorizerSerializerContext>();

var handler = async (APIGatewayCustomAuthorizerRequest request, ILambdaContext context) =>
{
    return await AuthorizerHandler.HandleAsync(request, context);
};

await LambdaBootstrapBuilder
    .Create<APIGatewayCustomAuthorizerRequest, APIGatewayCustomAuthorizerResponse>(handler, serializer)
    .Build()
    .RunAsync();

/// <summary>
/// Custom Lambda Authorizer handler for API Gateway token-based authorization.
/// Validates JWT tokens signed with HMAC-SHA256 and returns IAM policy documents.
/// </summary>
public static class AuthorizerHandler
{
    private static JwtTokenService? _tokenService;
    private static readonly object _lock = new();

    public static async Task<APIGatewayCustomAuthorizerResponse> HandleAsync(
        APIGatewayCustomAuthorizerRequest request,
        ILambdaContext context)
    {
        context.Logger.LogInformation("Authorizer invoked.");

        try
        {
            // Extract token from Authorization header (format: "Bearer {token}")
            var token = ExtractBearerToken(request.AuthorizationToken);

            if (string.IsNullOrEmpty(token))
            {
                context.Logger.LogWarning("No valid Bearer token found in request.");
                return GenerateDenyPolicy("unauthorized", request.MethodArn);
            }

            // Get or initialize the token service
            var tokenService = await GetTokenServiceAsync(context);

            // Validate the token
            var validationResult = tokenService.ValidateToken(token);

            if (!validationResult.IsValid)
            {
                context.Logger.LogWarning($"Token validation failed: {validationResult.ErrorMessage}");
                return GenerateDenyPolicy("unauthorized", request.MethodArn);
            }

            context.Logger.LogInformation($"Token validated for user: {validationResult.UserId}, role: {validationResult.Role}");

            // Return Allow policy with principal context
            return GenerateAllowPolicy(
                validationResult.UserId!,
                request.MethodArn,
                validationResult.UserId!,
                validationResult.Role!,
                validationResult.OutletId);
        }
        catch (Exception ex)
        {
            context.Logger.LogError($"Authorizer error: {ex.Message}");
            return GenerateDenyPolicy("unauthorized", request.MethodArn);
        }
    }

    /// <summary>
    /// Extracts the Bearer token from the Authorization header value.
    /// Expected format: "Bearer {token}"
    /// </summary>
    private static string? ExtractBearerToken(string? authorizationToken)
    {
        if (string.IsNullOrWhiteSpace(authorizationToken))
            return null;

        const string bearerPrefix = "Bearer ";
        if (authorizationToken.StartsWith(bearerPrefix, StringComparison.OrdinalIgnoreCase))
        {
            var token = authorizationToken[bearerPrefix.Length..].Trim();
            return string.IsNullOrEmpty(token) ? null : token;
        }

        return null;
    }

    /// <summary>
    /// Gets or initializes the JwtTokenService with the signing secret.
    /// Secret is retrieved from JWT_SECRET environment variable or AWS Secrets Manager.
    /// </summary>
    private static async Task<JwtTokenService> GetTokenServiceAsync(ILambdaContext context)
    {
        if (_tokenService != null)
            return _tokenService;

        lock (_lock)
        {
            if (_tokenService != null)
                return _tokenService;
        }

        var secret = await ResolveSecretAsync(context);

        lock (_lock)
        {
            _tokenService ??= new JwtTokenService(secret);
        }

        return _tokenService;
    }

    /// <summary>
    /// Resolves the JWT signing secret from environment variable or AWS Secrets Manager.
    /// </summary>
    private static async Task<string> ResolveSecretAsync(ILambdaContext context)
    {
        // First try environment variable (for development/testing)
        var envSecret = Environment.GetEnvironmentVariable("JWT_SECRET");
        if (!string.IsNullOrWhiteSpace(envSecret))
        {
            context.Logger.LogInformation("Using JWT secret from environment variable.");
            return envSecret;
        }

        // Fall back to AWS Secrets Manager
        var secretName = Environment.GetEnvironmentVariable("JWT_SECRET_NAME") ?? "VELoyalty/JwtSecret";
        context.Logger.LogInformation($"Retrieving JWT secret from Secrets Manager: {secretName}");

        using var client = new Amazon.SecretsManager.AmazonSecretsManagerClient();
        var response = await client.GetSecretValueAsync(
            new Amazon.SecretsManager.Model.GetSecretValueRequest
            {
                SecretId = secretName
            });

        if (string.IsNullOrWhiteSpace(response.SecretString))
        {
            throw new InvalidOperationException("JWT secret retrieved from Secrets Manager is empty.");
        }

        return response.SecretString;
    }

    /// <summary>
    /// Generates an IAM Allow policy with principal context containing user claims.
    /// </summary>
    private static APIGatewayCustomAuthorizerResponse GenerateAllowPolicy(
        string principalId,
        string methodArn,
        string userId,
        string role,
        string? outletId)
    {
        var response = new APIGatewayCustomAuthorizerResponse
        {
            PrincipalID = principalId,
            PolicyDocument = new APIGatewayCustomAuthorizerPolicy
            {
                Version = "2012-10-17",
                Statement = new List<APIGatewayCustomAuthorizerPolicy.IAMPolicyStatement>
                {
                    new()
                    {
                        Effect = "Allow",
                        Resource = new HashSet<string> { GetResourceArn(methodArn) },
                        Action = new HashSet<string> { "execute-api:Invoke" }
                    }
                }
            },
            Context = new APIGatewayCustomAuthorizerContextOutput
            {
                ["userId"] = userId,
                ["role"] = role,
                ["outletId"] = outletId ?? ""
            }
        };

        return response;
    }

    /// <summary>
    /// Generates an IAM Deny policy for unauthorized requests.
    /// </summary>
    private static APIGatewayCustomAuthorizerResponse GenerateDenyPolicy(string principalId, string? methodArn)
    {
        return new APIGatewayCustomAuthorizerResponse
        {
            PrincipalID = principalId,
            PolicyDocument = new APIGatewayCustomAuthorizerPolicy
            {
                Version = "2012-10-17",
                Statement = new List<APIGatewayCustomAuthorizerPolicy.IAMPolicyStatement>
                {
                    new()
                    {
                        Effect = "Deny",
                        Resource = new HashSet<string> { GetResourceArn(methodArn) },
                        Action = new HashSet<string> { "execute-api:Invoke" }
                    }
                }
            }
        };
    }

    /// <summary>
    /// Extracts a wildcard resource ARN from the method ARN for broader policy scope.
    /// If the method ARN is null or empty, uses a wildcard.
    /// </summary>
    private static string GetResourceArn(string? methodArn)
    {
        if (string.IsNullOrEmpty(methodArn))
            return "*";

        // Use the method ARN directly for specific resource authorization
        // Format: arn:aws:execute-api:{region}:{accountId}:{apiId}/{stage}/{method}/{resource}
        // We use a wildcard to allow access to all methods/resources under this API
        var arnParts = methodArn.Split('/');
        if (arnParts.Length >= 2)
        {
            // Return wildcard for all resources under this API stage
            return $"{arnParts[0]}/{arnParts[1]}/*";
        }

        return methodArn;
    }
}

/// <summary>
/// Source-generated JSON serializer context for Native AOT compatibility.
/// </summary>
[JsonSerializable(typeof(APIGatewayCustomAuthorizerRequest))]
[JsonSerializable(typeof(APIGatewayCustomAuthorizerResponse))]
[JsonSerializable(typeof(APIGatewayCustomAuthorizerPolicy))]
[JsonSerializable(typeof(APIGatewayCustomAuthorizerContextOutput))]
internal partial class AuthorizerSerializerContext : JsonSerializerContext
{
}
