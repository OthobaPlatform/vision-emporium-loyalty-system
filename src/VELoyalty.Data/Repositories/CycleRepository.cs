using Amazon.DynamoDBv2.Model;
using VELoyalty.Core;

namespace VELoyalty.Data.Repositories;

/// <summary>
/// Repository for managing loyalty cycle lifecycle operations: retrieving the active cycle,
/// archiving cycle data for historical reference, and resetting purchase counts for a new cycle.
/// </summary>
public class CycleRepository : DynamoDbRepository
{
    private const string ArchivePrefix = "ARCHIVE#";

    public CycleRepository(DynamoDbContext context) : base(context)
    {
    }

    /// <summary>
    /// Gets the currently active loyalty cycle.
    /// </summary>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>The active loyalty cycle, or null if none is active.</returns>
    public async Task<LoyaltyCycle?> GetActiveCycleAsync(CancellationToken cancellationToken = default)
    {
        var items = await QueryAsync(
            keyConditionExpression: "PK = :pk AND begins_with(SK, :prefix)",
            expressionAttributeValues: new Dictionary<string, AttributeValue>
            {
                [":pk"] = AttributeValueSerializer.ToS(KeyBuilder.CyclePk()),
                [":prefix"] = AttributeValueSerializer.ToS("CYCLE#"),
                [":active"] = AttributeValueSerializer.ToBool(true)
            },
            filterExpression: "IsActive = :active",
            cancellationToken: cancellationToken);

        var activeItem = items.FirstOrDefault();
        return activeItem is null ? null : MapToLoyaltyCycle(activeItem);
    }

    /// <summary>
    /// Archives cycle data by copying customer purchase counts and eligibility records
    /// to archive items for historical reference. This preserves the state of the cycle
    /// before reset.
    /// </summary>
    /// <param name="cycleId">The cycle ID being archived.</param>
    /// <param name="customers">Customer records with their purchase counts to archive.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    public async Task ArchiveCycleDataAsync(
        string cycleId,
        List<Customer> customers,
        CancellationToken cancellationToken = default)
    {
        var archiveItems = customers.Select(customer =>
            AttributeValueSerializer.NewItem(
                    $"{ArchivePrefix}{cycleId}",
                    $"CUST#{customer.CustomerId}")
                .WithString("CustomerId", customer.CustomerId)
                .WithString("Name", customer.Name)
                .WithString("PhoneNumber", customer.PhoneNumber)
                .WithInt("QualifyingPurchases", customer.QualifyingPurchases)
                .WithString("CycleId", cycleId)
                .WithDateTime("ArchivedAt", DateTime.UtcNow)
                .Build()
        ).ToList();

        if (archiveItems.Count > 0)
        {
            await BatchWriteAsync(archiveItems, cancellationToken);
        }

        // Mark the cycle as inactive (archived)
        await UpdateItemAsync(
            KeyBuilder.CyclePk(),
            KeyBuilder.CycleSk(cycleId),
            updateExpression: "SET IsActive = :inactive, ArchivedAt = :archivedAt",
            expressionAttributeValues: new Dictionary<string, AttributeValue>
            {
                [":inactive"] = AttributeValueSerializer.ToBool(false),
                [":archivedAt"] = AttributeValueSerializer.ToDateTime(DateTime.UtcNow)
            },
            cancellationToken: cancellationToken);
    }

    /// <summary>
    /// Resets all customer purchase counts to zero for a new cycle by updating
    /// each customer's QualifyingPurchases to 0 and setting the new cycle ID.
    /// </summary>
    /// <param name="newCycleId">The new cycle ID to assign to customers.</param>
    /// <param name="customerIds">List of customer IDs to reset.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    public async Task ResetPurchaseCountsAsync(
        string newCycleId,
        List<string> customerIds,
        CancellationToken cancellationToken = default)
    {
        foreach (var customerId in customerIds)
        {
            await UpdateItemAsync(
                KeyBuilder.CustomerPk(customerId),
                KeyBuilder.CustomerSk(),
                updateExpression: "SET QualifyingPurchases = :zero, CurrentCycleId = :cycleId",
                expressionAttributeValues: new Dictionary<string, AttributeValue>
                {
                    [":zero"] = AttributeValueSerializer.ToN(0),
                    [":cycleId"] = AttributeValueSerializer.ToS(newCycleId)
                },
                cancellationToken: cancellationToken);
        }
    }

    /// <summary>
    /// Gets archived customer data for a specific cycle.
    /// </summary>
    /// <param name="cycleId">The cycle ID to retrieve archive data for.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>List of archived customer records for the specified cycle.</returns>
    public async Task<List<Customer>> GetArchivedCycleDataAsync(
        string cycleId,
        CancellationToken cancellationToken = default)
    {
        var items = await QueryAsync(
            keyConditionExpression: "PK = :pk AND begins_with(SK, :prefix)",
            expressionAttributeValues: new Dictionary<string, AttributeValue>
            {
                [":pk"] = AttributeValueSerializer.ToS($"{ArchivePrefix}{cycleId}"),
                [":prefix"] = AttributeValueSerializer.ToS("CUST#")
            },
            cancellationToken: cancellationToken);

        return items.Select(item => new Customer(
            CustomerId: AttributeValueSerializer.GetRequiredString(item, "CustomerId"),
            Name: AttributeValueSerializer.GetRequiredString(item, "Name"),
            PhoneNumber: AttributeValueSerializer.GetRequiredString(item, "PhoneNumber"),
            QualifyingPurchases: AttributeValueSerializer.GetInt(item, "QualifyingPurchases"),
            CurrentCycleId: AttributeValueSerializer.GetRequiredString(item, "CycleId")
        )).ToList();
    }

    /// <summary>
    /// Gets all loyalty cycles (active and archived).
    /// </summary>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>List of all loyalty cycles.</returns>
    public async Task<List<LoyaltyCycle>> GetAllCyclesAsync(CancellationToken cancellationToken = default)
    {
        var items = await QueryAsync(
            keyConditionExpression: "PK = :pk AND begins_with(SK, :prefix)",
            expressionAttributeValues: new Dictionary<string, AttributeValue>
            {
                [":pk"] = AttributeValueSerializer.ToS(KeyBuilder.CyclePk()),
                [":prefix"] = AttributeValueSerializer.ToS("CYCLE#")
            },
            cancellationToken: cancellationToken);

        return items.Select(MapToLoyaltyCycle).ToList();
    }

    // ─── Mapping Helpers ────────────────────────────────────────────────────────

    private static LoyaltyCycle MapToLoyaltyCycle(Dictionary<string, AttributeValue> item) =>
        new(
            CycleId: AttributeValueSerializer.GetRequiredString(item, "CycleId"),
            StartDate: AttributeValueSerializer.GetDateOnly(item, "StartDate"),
            EndDate: AttributeValueSerializer.GetDateOnly(item, "EndDate"),
            IsActive: AttributeValueSerializer.GetBool(item, "IsActive")
        );
}
