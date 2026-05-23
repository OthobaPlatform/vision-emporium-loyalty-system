using Amazon.DynamoDBv2.Model;
using VELoyalty.Core;

namespace VELoyalty.Data.Repositories;

/// <summary>
/// Repository for managing gift redemption records in DynamoDB.
/// Each redemption represents a one-time gift claim event at a designated outlet.
/// </summary>
public class RedemptionRepository : DynamoDbRepository
{
    public RedemptionRepository(DynamoDbContext context) : base(context)
    {
    }

    /// <summary>
    /// Creates a redemption record for a successfully redeemed verification code.
    /// Uses a condition expression to ensure one-time redemption (idempotence).
    /// </summary>
    /// <param name="redemption">The redemption details to record.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>True if the redemption was recorded; false if the code was already redeemed.</returns>
    public async Task<bool> CreateRedemptionAsync(
        Redemption redemption,
        CancellationToken cancellationToken = default)
    {
        var pk = KeyBuilder.RedemptionPk(redemption.CustomerId);
        var sk = KeyBuilder.RedemptionSk(redemption.Code);

        var item = AttributeValueSerializer.NewItem(pk, sk)
            .WithString("Code", redemption.Code)
            .WithString("CustomerId", redemption.CustomerId)
            .WithString("OutletId", redemption.OutletId)
            .WithString("StaffMemberId", redemption.StaffMemberId)
            .WithString("GiftType", redemption.GiftType)
            .WithDateTime("RedeemedAt", redemption.RedeemedAt)
            .WithGsi1(
                KeyBuilder.RedemptionGsi1Pk(redemption.OutletId),
                KeyBuilder.RedemptionGsi1Sk(redemption.RedeemedAt))
            .WithGsi2(
                KeyBuilder.RedemptionGsi2Pk(redemption.Code),
                KeyBuilder.RedemptionGsi2Sk(redemption.RedeemedAt))
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
    /// Checks whether a verification code has already been redeemed.
    /// Looks up the redemption record by code using GSI2.
    /// </summary>
    /// <param name="code">The 6-digit verification code.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>True if the code has been redeemed; false otherwise.</returns>
    public async Task<bool> IsCodeRedeemedAsync(
        string code,
        CancellationToken cancellationToken = default)
    {
        var redemption = await GetRedemptionByCodeAsync(code, cancellationToken);
        return redemption is not null;
    }

    /// <summary>
    /// Gets the redemption record for a specific verification code using GSI2.
    /// </summary>
    /// <param name="code">The 6-digit verification code.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>The redemption details if found; null otherwise.</returns>
    public async Task<Redemption?> GetRedemptionByCodeAsync(
        string code,
        CancellationToken cancellationToken = default)
    {
        var gsi2Pk = KeyBuilder.RedemptionGsi2Pk(code);

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

        return items.Count > 0 ? MapToRedemption(items[0]) : null;
    }

    /// <summary>
    /// Gets all redemption records for a customer.
    /// </summary>
    /// <param name="customerId">The customer identifier.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>List of redemptions for the customer.</returns>
    public async Task<List<Redemption>> GetByCustomerAsync(
        string customerId,
        CancellationToken cancellationToken = default)
    {
        var pk = KeyBuilder.RedemptionPk(customerId);

        var items = await QueryAsync(
            keyConditionExpression: "PK = :pk AND begins_with(SK, :skPrefix)",
            expressionAttributeValues: new Dictionary<string, AttributeValue>
            {
                [":pk"] = AttributeValueSerializer.ToS(pk),
                [":skPrefix"] = AttributeValueSerializer.ToS("REDM#")
            },
            cancellationToken: cancellationToken);

        return items.Select(MapToRedemption).ToList();
    }

    /// <summary>
    /// Gets all redemption records for a specific outlet within a date range.
    /// Uses GSI1 for outlet-based queries.
    /// </summary>
    /// <param name="outletId">The outlet identifier.</param>
    /// <param name="fromDate">Start date (inclusive).</param>
    /// <param name="toDate">End date (inclusive).</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>List of redemptions at the outlet within the date range.</returns>
    public async Task<List<Redemption>> GetByOutletAndDateRangeAsync(
        string outletId,
        DateOnly fromDate,
        DateOnly toDate,
        CancellationToken cancellationToken = default)
    {
        var gsi1Pk = KeyBuilder.RedemptionGsi1Pk(outletId);
        var fromSk = $"REDM#{fromDate:yyyy-MM-dd}";
        var toSk = $"REDM#{toDate:yyyy-MM-dd}\uffff"; // Unicode max to include all entries on toDate

        var items = await QueryAsync(
            keyConditionExpression: "#gsi1pk = :gsi1pk AND #gsi1sk BETWEEN :fromSk AND :toSk",
            expressionAttributeValues: new Dictionary<string, AttributeValue>
            {
                [":gsi1pk"] = AttributeValueSerializer.ToS(gsi1Pk),
                [":fromSk"] = AttributeValueSerializer.ToS(fromSk),
                [":toSk"] = AttributeValueSerializer.ToS(toSk)
            },
            expressionAttributeNames: new Dictionary<string, string>
            {
                ["#gsi1pk"] = DynamoDbContext.Gsi1Pk,
                ["#gsi1sk"] = DynamoDbContext.Gsi1Sk
            },
            indexName: DynamoDbContext.Gsi1IndexName,
            cancellationToken: cancellationToken);

        return items.Select(MapToRedemption).ToList();
    }

    private static Redemption MapToRedemption(Dictionary<string, AttributeValue> item)
    {
        return new Redemption(
            Code: AttributeValueSerializer.GetRequiredString(item, "Code"),
            CustomerId: AttributeValueSerializer.GetRequiredString(item, "CustomerId"),
            OutletId: AttributeValueSerializer.GetRequiredString(item, "OutletId"),
            StaffMemberId: AttributeValueSerializer.GetRequiredString(item, "StaffMemberId"),
            GiftType: AttributeValueSerializer.GetRequiredString(item, "GiftType"),
            RedeemedAt: AttributeValueSerializer.GetDateTime(item, "RedeemedAt")
        );
    }
}
