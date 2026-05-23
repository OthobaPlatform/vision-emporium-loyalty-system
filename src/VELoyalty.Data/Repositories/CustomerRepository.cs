using Amazon.DynamoDBv2.Model;
using VELoyalty.Core;

namespace VELoyalty.Data.Repositories;

/// <summary>
/// Repository for managing Customer profiles in DynamoDB.
/// Supports create/update, get by ID, and get by phone number (GSI1 lookup).
/// </summary>
public class CustomerRepository : DynamoDbRepository
{
    public CustomerRepository(DynamoDbContext context) : base(context)
    {
    }

    /// <summary>
    /// Creates or updates a customer profile in DynamoDB.
    /// Uses PK=CUST#{customerId}, SK=PROFILE with GSI1 for phone lookups.
    /// </summary>
    /// <param name="customer">The customer record to persist.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    public async Task UpsertAsync(Customer customer, CancellationToken cancellationToken = default)
    {
        var item = AttributeValueSerializer.NewItem(
                KeyBuilder.CustomerPk(customer.CustomerId),
                KeyBuilder.CustomerSk())
            .WithGsi1(
                KeyBuilder.CustomerGsi1Pk(customer.PhoneNumber),
                KeyBuilder.CustomerGsi1Sk(customer.CustomerId))
            .WithString("CustomerId", customer.CustomerId)
            .WithString("Name", customer.Name)
            .WithString("PhoneNumber", customer.PhoneNumber)
            .WithInt("QualifyingPurchases", customer.QualifyingPurchases)
            .WithString("CurrentCycleId", customer.CurrentCycleId)
            .Build();

        await PutItemAsync(item, cancellationToken: cancellationToken);
    }

    /// <summary>
    /// Gets a customer by their unique identifier.
    /// </summary>
    /// <param name="customerId">The customer identifier.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>The customer record, or null if not found.</returns>
    public async Task<Customer?> GetByIdAsync(string customerId, CancellationToken cancellationToken = default)
    {
        var item = await GetItemAsync(
            KeyBuilder.CustomerPk(customerId),
            KeyBuilder.CustomerSk(),
            cancellationToken: cancellationToken);

        return item is null ? null : MapToCustomer(item);
    }

    /// <summary>
    /// Gets a customer by phone number using GSI1 (PHONE#{phone} → CUST#{customerId}).
    /// </summary>
    /// <param name="phoneNumber">The phone number in E.164 format.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>The customer record, or null if not found.</returns>
    public async Task<Customer?> GetByPhoneAsync(string phoneNumber, CancellationToken cancellationToken = default)
    {
        var results = await QueryAsync(
            keyConditionExpression: "GSI1PK = :gsi1pk",
            expressionAttributeValues: new Dictionary<string, AttributeValue>
            {
                [":gsi1pk"] = AttributeValueSerializer.ToS(KeyBuilder.CustomerGsi1Pk(phoneNumber))
            },
            indexName: DynamoDbContext.Gsi1IndexName,
            limit: 1,
            cancellationToken: cancellationToken);

        return results.Count == 0 ? null : MapToCustomer(results[0]);
    }

    /// <summary>
    /// Updates the qualifying purchase count for a customer.
    /// </summary>
    /// <param name="customerId">The customer identifier.</param>
    /// <param name="qualifyingPurchases">The new qualifying purchase count.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    public async Task UpdateQualifyingPurchasesAsync(
        string customerId,
        int qualifyingPurchases,
        CancellationToken cancellationToken = default)
    {
        await UpdateItemAsync(
            KeyBuilder.CustomerPk(customerId),
            KeyBuilder.CustomerSk(),
            updateExpression: "SET QualifyingPurchases = :count",
            expressionAttributeValues: new Dictionary<string, AttributeValue>
            {
                [":count"] = AttributeValueSerializer.ToN(qualifyingPurchases)
            },
            cancellationToken: cancellationToken);
    }

    /// <summary>
    /// Gets the count of active customers in a given cycle (qualifying purchases > 0).
    /// Uses a scan with filter for MVP. In production, maintain a counter.
    /// </summary>
    /// <param name="cycleId">The cycle identifier.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>Count of active customers in the cycle.</returns>
    public async Task<int> GetActiveCustomersInCycleAsync(string cycleId, CancellationToken cancellationToken = default)
    {
        // Scan for customer profiles with matching cycle and purchases > 0
        // For MVP this is acceptable; production would use a maintained counter
        var request = new Amazon.DynamoDBv2.Model.ScanRequest
        {
            TableName = Context.Table,
            FilterExpression = "begins_with(SK, :sk) AND CurrentCycleId = :cycleId AND QualifyingPurchases > :zero",
            ExpressionAttributeValues = new Dictionary<string, AttributeValue>
            {
                [":sk"] = AttributeValueSerializer.ToS("PROFILE"),
                [":cycleId"] = AttributeValueSerializer.ToS(cycleId),
                [":zero"] = AttributeValueSerializer.ToN(0)
            },
            Select = Amazon.DynamoDBv2.Select.COUNT
        };

        var response = await Context.Client.ScanAsync(request, cancellationToken);
        return response.Count;
    }

    private static Customer MapToCustomer(Dictionary<string, AttributeValue> item)
    {
        return new Customer(
            CustomerId: AttributeValueSerializer.GetRequiredString(item, "CustomerId"),
            Name: AttributeValueSerializer.GetRequiredString(item, "Name"),
            PhoneNumber: AttributeValueSerializer.GetRequiredString(item, "PhoneNumber"),
            QualifyingPurchases: AttributeValueSerializer.GetInt(item, "QualifyingPurchases"),
            CurrentCycleId: AttributeValueSerializer.GetRequiredString(item, "CurrentCycleId")
        );
    }
}
