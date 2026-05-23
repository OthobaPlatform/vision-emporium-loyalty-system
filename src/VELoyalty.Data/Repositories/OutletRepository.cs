using Amazon.DynamoDBv2.Model;
using VELoyalty.Core;

namespace VELoyalty.Data.Repositories;

/// <summary>
/// Repository for managing Outlet entities in DynamoDB.
/// Supports CRUD operations, active/inactive status management, and counting active outlets.
/// </summary>
public class OutletRepository : DynamoDbRepository
{
    public OutletRepository(DynamoDbContext context) : base(context) { }

    /// <summary>
    /// Creates a new outlet record.
    /// </summary>
    /// <param name="outlet">The outlet to create.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    public async Task CreateAsync(Outlet outlet, CancellationToken cancellationToken = default)
    {
        var item = AttributeValueSerializer.NewItem(
                KeyBuilder.OutletPk(outlet.OutletId),
                KeyBuilder.OutletSk())
            .WithGsi1(KeyBuilder.OutletGsi1Pk(), KeyBuilder.OutletGsi1Sk(outlet.OutletId))
            .WithString("outletId", outlet.OutletId)
            .WithString("name", outlet.Name)
            .WithString("address", outlet.Address)
            .WithString("phoneNumber", outlet.PhoneNumber)
            .WithString("assignedManagerId", outlet.AssignedManagerId)
            .WithBool("isActive", outlet.IsActive)
            .Build();

        await PutItemAsync(item, cancellationToken: cancellationToken);
    }

    /// <summary>
    /// Gets an outlet by its identifier.
    /// </summary>
    /// <param name="outletId">The outlet identifier.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>The outlet, or null if not found.</returns>
    public async Task<Outlet?> GetByIdAsync(string outletId, CancellationToken cancellationToken = default)
    {
        var item = await GetItemAsync(
            KeyBuilder.OutletPk(outletId),
            KeyBuilder.OutletSk(),
            cancellationToken: cancellationToken);

        return item is null ? null : MapToOutlet(item);
    }

    /// <summary>
    /// Updates an existing outlet record.
    /// </summary>
    /// <param name="outlet">The outlet with updated values.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    public async Task UpdateAsync(Outlet outlet, CancellationToken cancellationToken = default)
    {
        var item = AttributeValueSerializer.NewItem(
                KeyBuilder.OutletPk(outlet.OutletId),
                KeyBuilder.OutletSk())
            .WithGsi1(KeyBuilder.OutletGsi1Pk(), KeyBuilder.OutletGsi1Sk(outlet.OutletId))
            .WithString("outletId", outlet.OutletId)
            .WithString("name", outlet.Name)
            .WithString("address", outlet.Address)
            .WithString("phoneNumber", outlet.PhoneNumber)
            .WithString("assignedManagerId", outlet.AssignedManagerId)
            .WithBool("isActive", outlet.IsActive)
            .Build();

        await PutItemAsync(item, cancellationToken: cancellationToken);
    }

    /// <summary>
    /// Lists all outlets by querying GSI1 with the fixed partition key.
    /// </summary>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>List of all outlets.</returns>
    public async Task<List<Outlet>> ListAllAsync(CancellationToken cancellationToken = default)
    {
        var items = await QueryAsync(
            keyConditionExpression: "#gsi1pk = :gsi1pk",
            expressionAttributeValues: new Dictionary<string, AttributeValue>
            {
                [":gsi1pk"] = AttributeValueSerializer.ToS(KeyBuilder.OutletGsi1Pk())
            },
            expressionAttributeNames: new Dictionary<string, string>
            {
                ["#gsi1pk"] = DynamoDbContext.Gsi1Pk
            },
            indexName: DynamoDbContext.Gsi1IndexName,
            cancellationToken: cancellationToken);

        return items.Select(MapToOutlet).ToList();
    }

    /// <summary>
    /// Counts the number of currently active outlets.
    /// Used for last-active-outlet protection (cannot deactivate if only one remains).
    /// </summary>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>The count of active outlets.</returns>
    public async Task<int> CountActiveAsync(CancellationToken cancellationToken = default)
    {
        var items = await QueryAsync(
            keyConditionExpression: "#gsi1pk = :gsi1pk",
            expressionAttributeValues: new Dictionary<string, AttributeValue>
            {
                [":gsi1pk"] = AttributeValueSerializer.ToS(KeyBuilder.OutletGsi1Pk()),
                [":active"] = AttributeValueSerializer.ToBool(true)
            },
            expressionAttributeNames: new Dictionary<string, string>
            {
                ["#gsi1pk"] = DynamoDbContext.Gsi1Pk,
                ["#isActive"] = "isActive"
            },
            indexName: DynamoDbContext.Gsi1IndexName,
            filterExpression: "#isActive = :active",
            cancellationToken: cancellationToken);

        return items.Count;
    }

    /// <summary>
    /// Updates the active/inactive status of an outlet.
    /// </summary>
    /// <param name="outletId">The outlet identifier.</param>
    /// <param name="isActive">The new active status.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    public async Task UpdateStatusAsync(string outletId, bool isActive, CancellationToken cancellationToken = default)
    {
        await UpdateItemAsync(
            KeyBuilder.OutletPk(outletId),
            KeyBuilder.OutletSk(),
            updateExpression: "SET #isActive = :isActive",
            expressionAttributeValues: new Dictionary<string, AttributeValue>
            {
                [":isActive"] = AttributeValueSerializer.ToBool(isActive)
            },
            expressionAttributeNames: new Dictionary<string, string>
            {
                ["#isActive"] = "isActive"
            },
            cancellationToken: cancellationToken);
    }

    /// <summary>
    /// Deletes an outlet by its identifier.
    /// </summary>
    /// <param name="outletId">The outlet identifier.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    public async Task DeleteAsync(string outletId, CancellationToken cancellationToken = default)
    {
        await DeleteItemAsync(
            KeyBuilder.OutletPk(outletId),
            KeyBuilder.OutletSk(),
            cancellationToken);
    }

    private static Outlet MapToOutlet(Dictionary<string, AttributeValue> item) =>
        new(
            OutletId: AttributeValueSerializer.GetRequiredString(item, "outletId"),
            Name: AttributeValueSerializer.GetRequiredString(item, "name"),
            Address: AttributeValueSerializer.GetRequiredString(item, "address"),
            PhoneNumber: AttributeValueSerializer.GetRequiredString(item, "phoneNumber"),
            AssignedManagerId: AttributeValueSerializer.GetRequiredString(item, "assignedManagerId"),
            IsActive: AttributeValueSerializer.GetBool(item, "isActive")
        );
}
