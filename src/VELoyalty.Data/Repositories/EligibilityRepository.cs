using Amazon.DynamoDBv2.Model;
using VELoyalty.Core;

namespace VELoyalty.Data.Repositories;

/// <summary>
/// Repository for managing customer eligibility records in DynamoDB.
/// Eligibility records track which customers have reached purchase thresholds
/// and are entitled to gift redemption.
/// </summary>
public class EligibilityRepository : DynamoDbRepository
{
    public EligibilityRepository(DynamoDbContext context) : base(context)
    {
    }

    /// <summary>
    /// Creates an eligibility record for a customer who has reached a purchase threshold.
    /// Uses a condition expression to prevent duplicate eligibility for the same customer+cycle+tier.
    /// </summary>
    /// <param name="verificationCode">The verification code details associated with this eligibility.</param>
    /// <param name="cycleId">The loyalty cycle identifier.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>True if the record was created; false if eligibility already exists.</returns>
    public async Task<bool> CreateEligibilityAsync(
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
    /// Checks whether an eligibility record already exists for a given customer, cycle, and tier.
    /// </summary>
    /// <param name="customerId">The customer identifier.</param>
    /// <param name="cycleId">The loyalty cycle identifier.</param>
    /// <param name="tier">The threshold tier number.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>True if eligibility exists; false otherwise.</returns>
    public async Task<bool> ExistsAsync(
        string customerId,
        string cycleId,
        int tier,
        CancellationToken cancellationToken = default)
    {
        var pk = KeyBuilder.EligibilityPk(customerId);
        var sk = KeyBuilder.EligibilitySk(cycleId, tier);

        var result = await GetItemAsync(pk, sk, cancellationToken: cancellationToken);
        return result is not null;
    }

    /// <summary>
    /// Gets the eligibility record for a specific customer, cycle, and tier.
    /// </summary>
    /// <param name="customerId">The customer identifier.</param>
    /// <param name="cycleId">The loyalty cycle identifier.</param>
    /// <param name="tier">The threshold tier number.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>The verification code details if found; null otherwise.</returns>
    public async Task<VerificationCode?> GetEligibilityAsync(
        string customerId,
        string cycleId,
        int tier,
        CancellationToken cancellationToken = default)
    {
        var pk = KeyBuilder.EligibilityPk(customerId);
        var sk = KeyBuilder.EligibilitySk(cycleId, tier);

        var item = await GetItemAsync(pk, sk, cancellationToken: cancellationToken);
        return item is null ? null : MapToVerificationCode(item);
    }

    /// <summary>
    /// Gets all eligibility records for a customer in a specific cycle.
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
    /// Looks up an eligibility record by verification code using GSI2.
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
    /// Updates the status of an eligibility record (e.g., Active → Redeemed or Expired).
    /// </summary>
    /// <param name="customerId">The customer identifier.</param>
    /// <param name="cycleId">The loyalty cycle identifier.</param>
    /// <param name="tier">The threshold tier number.</param>
    /// <param name="newStatus">The new status value.</param>
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
