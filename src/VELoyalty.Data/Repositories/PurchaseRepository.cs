using Amazon.DynamoDBv2.Model;
using VELoyalty.Core;

namespace VELoyalty.Data.Repositories;

/// <summary>
/// Repository for managing Purchase records in DynamoDB.
/// Supports storing purchases, querying by customer+cycle, checking duplicates,
/// and calculating qualifying purchase counts with filtering.
/// </summary>
public class PurchaseRepository : DynamoDbRepository
{
    public PurchaseRepository(DynamoDbContext context) : base(context)
    {
    }

    /// <summary>
    /// Stores a purchase record in DynamoDB.
    /// The SK (PURCH#{date}#{outletId}#{amount}) serves as the composite deduplication key.
    /// Uses a condition expression to prevent overwriting existing records (deduplication).
    /// </summary>
    /// <param name="purchase">The purchase record to store.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>True if the purchase was stored successfully; false if it already exists (duplicate).</returns>
    public async Task<bool> StorePurchaseAsync(Purchase purchase, CancellationToken cancellationToken = default)
    {
        var pk = KeyBuilder.PurchasePk(purchase.CustomerId);
        var sk = KeyBuilder.PurchaseSk(purchase.PurchaseDate, purchase.OutletId, purchase.Amount);

        var item = AttributeValueSerializer.NewItem(pk, sk)
            .WithGsi1(
                KeyBuilder.PurchaseGsi1Pk(purchase.OutletId),
                KeyBuilder.PurchaseGsi1Sk(purchase.PurchaseDate))
            .WithString("CustomerId", purchase.CustomerId)
            .WithString("OutletId", purchase.OutletId)
            .WithDate("PurchaseDate", purchase.PurchaseDate)
            .WithDecimal("Amount", purchase.Amount)
            .WithString("ProductCategory", purchase.ProductCategory)
            .WithDateTime("ProcessedAt", purchase.ProcessedAt)
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
            // Item already exists — this is a duplicate
            return false;
        }
    }

    /// <summary>
    /// Checks whether a purchase with the same composite key already exists (deduplication check).
    /// The composite key is: customerId + outletId + purchaseDate + amount.
    /// </summary>
    /// <param name="customerId">Customer identifier.</param>
    /// <param name="outletId">Outlet identifier.</param>
    /// <param name="purchaseDate">Date of the purchase.</param>
    /// <param name="amount">Purchase amount.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>True if a duplicate exists; false otherwise.</returns>
    public async Task<bool> ExistsAsync(
        string customerId,
        string outletId,
        DateOnly purchaseDate,
        decimal amount,
        CancellationToken cancellationToken = default)
    {
        var pk = KeyBuilder.PurchasePk(customerId);
        var sk = KeyBuilder.PurchaseSk(purchaseDate, outletId, amount);

        var item = await GetItemAsync(pk, sk, cancellationToken: cancellationToken);
        return item is not null;
    }

    /// <summary>
    /// Queries all purchases for a customer within a given date range (representing a loyalty cycle).
    /// </summary>
    /// <param name="customerId">Customer identifier.</param>
    /// <param name="cycleStartDate">Cycle start date (inclusive).</param>
    /// <param name="cycleEndDate">Cycle end date (inclusive).</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>List of purchases within the cycle period.</returns>
    public async Task<List<Purchase>> GetByCustomerAndCycleAsync(
        string customerId,
        DateOnly cycleStartDate,
        DateOnly cycleEndDate,
        CancellationToken cancellationToken = default)
    {
        // SK pattern: PURCH#{date}#{outletId}#{amount}
        // We query with begins_with for the PURCH# prefix and filter by date range
        var skStart = $"PURCH#{cycleStartDate:yyyy-MM-dd}";
        var skEnd = $"PURCH#{cycleEndDate:yyyy-MM-dd}~"; // ~ is after all valid chars to include the end date

        var results = await QueryAsync(
            keyConditionExpression: "PK = :pk AND SK BETWEEN :skStart AND :skEnd",
            expressionAttributeValues: new Dictionary<string, AttributeValue>
            {
                [":pk"] = AttributeValueSerializer.ToS(KeyBuilder.PurchasePk(customerId)),
                [":skStart"] = AttributeValueSerializer.ToS(skStart),
                [":skEnd"] = AttributeValueSerializer.ToS(skEnd)
            },
            cancellationToken: cancellationToken);

        return results.Select(MapToPurchase).ToList();
    }

    /// <summary>
    /// Calculates the number of qualifying purchases for a customer within a cycle,
    /// filtering by minimum purchase amount and excluded product categories.
    /// </summary>
    /// <param name="customerId">Customer identifier.</param>
    /// <param name="cycleStartDate">Cycle start date (inclusive).</param>
    /// <param name="cycleEndDate">Cycle end date (inclusive).</param>
    /// <param name="minPurchaseAmount">Minimum purchase amount to qualify.</param>
    /// <param name="excludedCategories">Product categories that do not count toward thresholds.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>The count of qualifying purchases.</returns>
    public async Task<int> GetQualifyingPurchaseCountAsync(
        string customerId,
        DateOnly cycleStartDate,
        DateOnly cycleEndDate,
        decimal minPurchaseAmount,
        List<string> excludedCategories,
        CancellationToken cancellationToken = default)
    {
        var purchases = await GetByCustomerAndCycleAsync(
            customerId, cycleStartDate, cycleEndDate, cancellationToken);

        return CountQualifyingPurchases(purchases, minPurchaseAmount, excludedCategories);
    }

    /// <summary>
    /// Counts qualifying purchases from a list, applying the minimum amount and excluded category filters.
    /// A purchase qualifies if its amount >= minPurchaseAmount AND its category is NOT in the excluded list.
    /// </summary>
    /// <param name="purchases">List of purchases to evaluate.</param>
    /// <param name="minPurchaseAmount">Minimum purchase amount to qualify.</param>
    /// <param name="excludedCategories">Product categories that do not count toward thresholds.</param>
    /// <returns>The count of qualifying purchases.</returns>
    public static int CountQualifyingPurchases(
        List<Purchase> purchases,
        decimal minPurchaseAmount,
        List<string> excludedCategories)
    {
        return purchases.Count(p =>
            p.Amount >= minPurchaseAmount &&
            !excludedCategories.Contains(p.ProductCategory, StringComparer.OrdinalIgnoreCase));
    }

    private static Purchase MapToPurchase(Dictionary<string, AttributeValue> item)
    {
        return new Purchase(
            CustomerId: AttributeValueSerializer.GetRequiredString(item, "CustomerId"),
            OutletId: AttributeValueSerializer.GetRequiredString(item, "OutletId"),
            PurchaseDate: AttributeValueSerializer.GetDateOnly(item, "PurchaseDate"),
            Amount: AttributeValueSerializer.GetDecimal(item, "Amount"),
            ProductCategory: AttributeValueSerializer.GetRequiredString(item, "ProductCategory"),
            ProcessedAt: AttributeValueSerializer.GetDateTime(item, "ProcessedAt")
        );
    }
}
