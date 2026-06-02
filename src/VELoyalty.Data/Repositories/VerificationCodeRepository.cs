using System.Security.Cryptography;
using Amazon.DynamoDBv2.Model;
using VELoyalty.Core;

namespace VELoyalty.Data.Repositories;

/// <summary>
/// Repository for generating, storing, and managing verification codes.
/// Verification codes are 6-digit numeric codes issued to eligible customers
/// for gift redemption at designated outlets.
/// </summary>
public class VerificationCodeRepository : DynamoDbRepository
{
    public VerificationCodeRepository(DynamoDbContext context) : base(context)
    {
    }

    /// <summary>
    /// Generates a unique 6-digit numeric verification code.
    /// Uses cryptographically secure random number generation.
    /// Checks for uniqueness against existing codes in the table.
    /// </summary>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>A unique 6-digit numeric code string.</returns>
    public async Task<string> GenerateUniqueCodeAsync(CancellationToken cancellationToken = default)
    {
        const int maxAttempts = 10;

        for (int attempt = 0; attempt < maxAttempts; attempt++)
        {
            var code = GenerateCode();

            // Check if code already exists via GSI2
            var existing = await GetByCodeAsync(code, cancellationToken);
            if (existing is null)
                return code;
        }

        throw new InvalidOperationException(
            "Failed to generate a unique verification code after maximum attempts.");
    }

    /// <summary>
    /// Stores a verification code record in DynamoDB.
    /// The code is stored as an eligibility record with GSI2 for code-based lookups.
    /// </summary>
    /// <param name="verificationCode">The verification code to store.</param>
    /// <param name="cycleId">The loyalty cycle identifier.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>True if stored successfully; false if a duplicate code exists.</returns>
    public async Task<bool> StoreCodeAsync(
        VerificationCode verificationCode,
        string cycleId,
        CancellationToken cancellationToken = default)
    {
        var pk = KeyBuilder.EligibilityPk(verificationCode.CustomerId);
        var sk = KeyBuilder.EligibilitySk(cycleId, verificationCode.Tier);

        var item = AttributeValueSerializer.NewItem(pk, sk)
            .WithString("CustomerId", verificationCode.CustomerId)
            .WithString("CycleId", cycleId)
            .WithInt("Tier", verificationCode.Tier)
            .WithString("Code", verificationCode.Code)
            .WithString("OutletId", verificationCode.OutletId)
            .WithString("GiftType", verificationCode.GiftType)
            .WithString("GiftDescription", verificationCode.GiftDescription)
            .WithDecimal("GiftValue", verificationCode.GiftValue)
            .WithDateTime("IssuedAt", verificationCode.IssuedAt)
            .WithDateTime("ExpiresAt", verificationCode.ExpiresAt)
            .WithString("Status", verificationCode.Status)
            .WithGsi1(
                KeyBuilder.EligibilityGsi1Pk(verificationCode.OutletId),
                KeyBuilder.EligibilityGsi1Sk(DateOnly.FromDateTime(verificationCode.IssuedAt)))
            .WithGsi2(
                KeyBuilder.EligibilityGsi2Pk(verificationCode.Code),
                KeyBuilder.EligibilityGsi2Sk(verificationCode.CustomerId))
            .Build();

        try
        {
            await PutItemAsync(
                item,
                conditionExpression: "attribute_not_exists(PK) AND attribute_not_exists(SK)",
                cancellationToken: cancellationToken);
            return true;
        }
        catch (ConditionalCheckFailedException)
        {
            return false;
        }
    }

    /// <summary>
    /// Looks up a verification code by its 6-digit code value using GSI2.
    /// </summary>
    /// <param name="code">The 6-digit verification code.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>The verification code details if found; null otherwise.</returns>
    public async Task<VerificationCode?> GetByCodeAsync(
        string code,
        CancellationToken cancellationToken = default)
    {
        var gsi2Pk = KeyBuilder.EligibilityGsi2Pk(code);

        var items = await QueryAsync(
            keyConditionExpression: "#gsi2pk = :gsi2pk",
            expressionAttributeValues: new Dictionary<string, AttributeValue>
            {
                [":gsi2pk"] = AttributeValueSerializer.ToS(gsi2Pk)
            },
            expressionAttributeNames: new Dictionary<string, string>
            {
                ["#gsi2pk"] = DynamoDbContext.Gsi2Pk
            },
            indexName: DynamoDbContext.Gsi2IndexName,
            limit: 1,
            cancellationToken: cancellationToken);

        return items.Count > 0 ? MapToVerificationCode(items[0]) : null;
    }

    /// <summary>
    /// Updates the status of a verification code (e.g., Active → Redeemed or Expired).
    /// </summary>
    /// <param name="customerId">The customer identifier.</param>
    /// <param name="cycleId">The loyalty cycle identifier.</param>
    /// <param name="tier">The threshold tier number.</param>
    /// <param name="newStatus">The new status value (Active, Redeemed, or Expired).</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    public async Task UpdateStatusAsync(
        string customerId,
        string cycleId,
        int tier,
        string newStatus,
        CancellationToken cancellationToken = default)
    {
        var pk = KeyBuilder.EligibilityPk(customerId);
        var sk = KeyBuilder.EligibilitySk(cycleId, tier);

        await UpdateItemAsync(
            pk, sk,
            updateExpression: "SET #status = :status",
            expressionAttributeValues: new Dictionary<string, AttributeValue>
            {
                [":status"] = AttributeValueSerializer.ToS(newStatus)
            },
            expressionAttributeNames: new Dictionary<string, string>
            {
                ["#status"] = "Status"
            },
            cancellationToken: cancellationToken);
    }

    /// <summary>
    /// Gets all verification codes for a customer in a specific cycle.
    /// </summary>
    /// <param name="customerId">The customer identifier.</param>
    /// <param name="cycleId">The loyalty cycle identifier.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>List of verification codes for the customer in the cycle.</returns>
    public async Task<List<VerificationCode>> GetByCustomerAndCycleAsync(
        string customerId,
        string cycleId,
        CancellationToken cancellationToken = default)
    {
        var pk = KeyBuilder.EligibilityPk(customerId);
        var skPrefix = $"ELIG#{cycleId}#";

        var items = await QueryAsync(
            keyConditionExpression: "PK = :pk AND begins_with(SK, :skPrefix)",
            expressionAttributeValues: new Dictionary<string, AttributeValue>
            {
                [":pk"] = AttributeValueSerializer.ToS(pk),
                [":skPrefix"] = AttributeValueSerializer.ToS(skPrefix)
            },
            cancellationToken: cancellationToken);

        return items.Select(MapToVerificationCode).ToList();
    }

    /// <summary>
    /// Lists all verification codes (eligibility records) from the table.
    /// Uses a scan with filter for MVP. Optionally filters by status.
    /// </summary>
    /// <param name="status">Optional status filter (Active, Redeemed, Expired).</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>List of all verification codes.</returns>
    public async Task<List<VerificationCode>> ListAllCodesAsync(
        string? status = null,
        CancellationToken cancellationToken = default)
    {
        var results = new List<Dictionary<string, AttributeValue>>();
        Dictionary<string, AttributeValue>? lastEvaluatedKey = null;

        do
        {
            var request = new Amazon.DynamoDBv2.Model.ScanRequest
            {
                TableName = Context.Table,
                FilterExpression = "begins_with(SK, :eligPrefix)",
                ExpressionAttributeValues = new Dictionary<string, AttributeValue>
                {
                    [":eligPrefix"] = AttributeValueSerializer.ToS("ELIG#")
                }
            };

            if (!string.IsNullOrWhiteSpace(status))
            {
                request.FilterExpression += " AND #status = :status";
                request.ExpressionAttributeValues[":status"] = AttributeValueSerializer.ToS(status);
                request.ExpressionAttributeNames = new Dictionary<string, string>
                {
                    ["#status"] = "Status"
                };
            }

            if (lastEvaluatedKey is not null)
                request.ExclusiveStartKey = lastEvaluatedKey;

            var response = await Context.Client.ScanAsync(request, cancellationToken);
            results.AddRange(response.Items);
            lastEvaluatedKey = response.LastEvaluatedKey is { Count: > 0 }
                ? response.LastEvaluatedKey
                : null;

        } while (lastEvaluatedKey is not null);

        return results.Select(MapToVerificationCode).ToList();
    }

    /// <summary>
    /// Counts active (non-redeemed, non-expired) verification codes.
    /// Uses a scan with filter for MVP. In production, maintain a counter.
    /// </summary>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>Count of active verification codes.</returns>
    public async Task<int> CountActiveCodesAsync(CancellationToken cancellationToken = default)
    {
        var now = DateTime.UtcNow.ToString("yyyy-MM-ddTHH:mm:ss.fffZ");

        var request = new Amazon.DynamoDBv2.Model.ScanRequest
        {
            TableName = Context.Table,
            FilterExpression = "#status = :active AND ExpiresAt > :now AND begins_with(SK, :eligPrefix)",
            ExpressionAttributeValues = new Dictionary<string, AttributeValue>
            {
                [":active"] = AttributeValueSerializer.ToS("Active"),
                [":now"] = AttributeValueSerializer.ToS(now),
                [":eligPrefix"] = AttributeValueSerializer.ToS("ELIG#")
            },
            ExpressionAttributeNames = new Dictionary<string, string>
            {
                ["#status"] = "Status"
            },
            Select = Amazon.DynamoDBv2.Select.COUNT
        };

        var response = await Context.Client.ScanAsync(request, cancellationToken);
        return response.Count;
    }

    /// <summary>
    /// Generates a cryptographically secure 6-digit numeric code.
    /// </summary>
    /// <returns>A 6-digit numeric string (e.g., "042891").</returns>
    private static string GenerateCode()
    {
        var number = RandomNumberGenerator.GetInt32(0, 1_000_000);
        return number.ToString("D6");
    }

    private static VerificationCode MapToVerificationCode(Dictionary<string, AttributeValue> item)
    {
        return new VerificationCode(
            Code: AttributeValueSerializer.GetRequiredString(item, "Code"),
            CustomerId: AttributeValueSerializer.GetRequiredString(item, "CustomerId"),
            OutletId: AttributeValueSerializer.GetRequiredString(item, "OutletId"),
            Tier: AttributeValueSerializer.GetInt(item, "Tier"),
            GiftType: AttributeValueSerializer.GetRequiredString(item, "GiftType"),
            GiftDescription: AttributeValueSerializer.GetRequiredString(item, "GiftDescription"),
            GiftValue: AttributeValueSerializer.GetDecimal(item, "GiftValue"),
            IssuedAt: AttributeValueSerializer.GetDateTime(item, "IssuedAt"),
            ExpiresAt: AttributeValueSerializer.GetDateTime(item, "ExpiresAt"),
            Status: AttributeValueSerializer.GetRequiredString(item, "Status")
        );
    }
}
