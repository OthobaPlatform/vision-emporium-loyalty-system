using Amazon.DynamoDBv2.Model;
using VELoyalty.Core;

namespace VELoyalty.Data.Repositories;

/// <summary>
/// Repository for managing Purchase records in DynamoDB.
/// Supports storing individual line items, grouping by CHALLAN_NO for purchase counting,
/// checking duplicates, and calculating qualifying purchase counts.
///
/// Key design: Each line item is stored individually (PK=CUST#{customerId}, SK=PURCH#{challanNo}#{itemId}).
/// Purchase count toward thresholds is based on unique CHALLAN_NO values (1 challan = 1 purchase).
/// </summary>
public class PurchaseRepository : DynamoDbRepository
{
    public PurchaseRepository(DynamoDbContext context) : base(context)
    {
    }

    /// <summary>
    /// Stores a purchase line item in DynamoDB.
    /// Uses CHALLAN_NO + ITEM_ID as the deduplication key.
    /// </summary>
    /// <param name="purchase">The purchase line item to store.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>True if stored successfully; false if duplicate.</returns>
    public async Task<bool> StorePurchaseAsync(Purchase purchase, CancellationToken cancellationToken = default)
    {
        var pk = KeyBuilder.PurchasePk(purchase.CustomerId);
        var sk = KeyBuilder.PurchaseSk(purchase.ChallanNo, purchase.ItemId);

        var itemBuilder = AttributeValueSerializer.NewItem(pk, sk)
            .WithGsi1(
                KeyBuilder.PurchaseGsi1Pk(purchase.OutletId),
                KeyBuilder.PurchaseGsi1Sk(purchase.PurchaseDate))
            .WithString("CustomerId", purchase.CustomerId)
            .WithString("OutletId", purchase.OutletId)
            .WithDate("PurchaseDate", purchase.PurchaseDate)
            .WithDecimal("Amount", purchase.Amount)
            .WithString("ProductCategory", purchase.ProductCategory)
            .WithDateTime("ProcessedAt", purchase.ProcessedAt)
            .WithString("ChallanNo", purchase.ChallanNo);

        if (purchase.ItemId != null)
            itemBuilder.WithString("ItemId", purchase.ItemId);
        if (purchase.Quantity != 1)
            itemBuilder.WithInt("Quantity", purchase.Quantity);

        // Add GSI2 for challan-based lookups
        itemBuilder.WithGsi2(
            KeyBuilder.PurchaseGsi2Pk(purchase.ChallanNo),
            KeyBuilder.PurchaseGsi2Sk(purchase.ItemId ?? "UNKNOWN"));

        var item = itemBuilder.Build();

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
    /// Checks whether a purchase line item with the same challan+item already exists.
    /// </summary>
    public async Task<bool> ExistsByChallanAsync(
        string customerId,
        string challanNo,
        string? itemId = null,
        CancellationToken cancellationToken = default)
    {
        var pk = KeyBuilder.PurchasePk(customerId);
        var sk = KeyBuilder.PurchaseSk(challanNo, itemId);

        var item = await GetItemAsync(pk, sk, cancellationToken: cancellationToken);
        return item is not null;
    }

    /// <summary>
    /// Queries all purchase line items for a customer with SK prefix "PURCH#".
    /// </summary>
    public async Task<List<Purchase>> GetByCustomerAsync(
        string customerId,
        CancellationToken cancellationToken = default)
    {
        var results = await QueryAsync(
            keyConditionExpression: "PK = :pk AND begins_with(SK, :skPrefix)",
            expressionAttributeValues: new Dictionary<string, AttributeValue>
            {
                [":pk"] = AttributeValueSerializer.ToS(KeyBuilder.PurchasePk(customerId)),
                [":skPrefix"] = AttributeValueSerializer.ToS("PURCH#")
            },
            cancellationToken: cancellationToken);

        return results.Select(MapToPurchase).ToList();
    }

    /// <summary>
    /// Queries all purchases for a customer within a given date range (representing a loyalty cycle).
    /// Filters by PurchaseDate attribute after query.
    /// </summary>
    public async Task<List<Purchase>> GetByCustomerAndCycleAsync(
        string customerId,
        DateOnly cycleStartDate,
        DateOnly cycleEndDate,
        CancellationToken cancellationToken = default)
    {
        var allPurchases = await GetByCustomerAsync(customerId, cancellationToken);

        return allPurchases
            .Where(p => p.PurchaseDate >= cycleStartDate && p.PurchaseDate <= cycleEndDate)
            .ToList();
    }

    /// <summary>
    /// Calculates the number of qualifying purchases (unique challans) for a customer within a cycle.
    /// Groups line items by CHALLAN_NO — each unique challan counts as 1 purchase.
    /// The total challan amount (sum of line items) must meet the minimum purchase amount.
    /// </summary>
    /// <param name="customerId">Customer identifier.</param>
    /// <param name="cycleStartDate">Cycle start date (inclusive).</param>
    /// <param name="cycleEndDate">Cycle end date (inclusive).</param>
    /// <param name="minPurchaseAmount">Minimum total challan amount to qualify (in BDT).</param>
    /// <param name="excludedCategories">Product categories excluded (if ALL items in a challan are excluded, it doesn't count).</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>The count of qualifying purchases (unique challans meeting criteria).</returns>
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
    /// Counts qualifying purchases from a list by grouping on CHALLAN_NO.
    /// A challan qualifies if its total amount (sum of all line items) >= minPurchaseAmount
    /// AND at least one item in the challan is NOT in the excluded categories.
    /// </summary>
    public static int CountQualifyingPurchases(
        List<Purchase> purchases,
        decimal minPurchaseAmount,
        List<string> excludedCategories)
    {
        // Group by ChallanNo — each group is one purchase
        var challanGroups = purchases.GroupBy(p => p.ChallanNo);

        return challanGroups.Count(group =>
        {
            // Total amount for the challan (sum of all line items)
            var totalAmount = group.Sum(p => p.Amount);

            // At least one item must NOT be in excluded categories
            var hasNonExcludedItem = group.Any(p =>
                !excludedCategories.Contains(p.ProductCategory, StringComparer.OrdinalIgnoreCase));

            return totalAmount >= minPurchaseAmount && hasNonExcludedItem;
        });
    }

    private static Purchase MapToPurchase(Dictionary<string, AttributeValue> item)
    {
        return new Purchase(
            CustomerId: AttributeValueSerializer.GetRequiredString(item, "CustomerId"),
            OutletId: AttributeValueSerializer.GetRequiredString(item, "OutletId"),
            PurchaseDate: AttributeValueSerializer.GetDateOnly(item, "PurchaseDate"),
            Amount: AttributeValueSerializer.GetDecimal(item, "Amount"),
            ProductCategory: AttributeValueSerializer.GetRequiredString(item, "ProductCategory"),
            ProcessedAt: AttributeValueSerializer.GetDateTime(item, "ProcessedAt"),
            ChallanNo: AttributeValueSerializer.GetRequiredString(item, "ChallanNo"),
            ItemId: AttributeValueSerializer.GetString(item, "ItemId"),
            Quantity: item.ContainsKey("Quantity") ? AttributeValueSerializer.GetInt(item, "Quantity") : 1
        );
    }
}
