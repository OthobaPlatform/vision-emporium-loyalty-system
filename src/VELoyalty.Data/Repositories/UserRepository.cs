using Amazon.DynamoDBv2.Model;
using VELoyalty.Core;

namespace VELoyalty.Data.Repositories;

/// <summary>
/// Repository for managing User entities in DynamoDB.
/// Supports CRUD operations and email-based lookup via GSI1 (GSI1_USER / USER#{email}).
/// Password hashing is done at the service layer; this repository stores the bcrypt hash as-is.
/// </summary>
public class UserRepository : DynamoDbRepository
{
    public UserRepository(DynamoDbContext context) : base(context) { }

    /// <summary>
    /// Creates a new user record.
    /// </summary>
    /// <param name="user">The user to create.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    public async Task CreateAsync(User user, CancellationToken cancellationToken = default)
    {
        var item = BuildUserItem(user);

        // Condition: prevent overwriting an existing user with the same PK
        await PutItemAsync(
            item,
            conditionExpression: "attribute_not_exists(PK)",
            cancellationToken: cancellationToken);
    }

    /// <summary>
    /// Gets a user by their unique identifier.
    /// </summary>
    /// <param name="userId">The user identifier.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>The user, or null if not found.</returns>
    public async Task<User?> GetByIdAsync(string userId, CancellationToken cancellationToken = default)
    {
        var item = await GetItemAsync(
            KeyBuilder.UserPk(userId),
            KeyBuilder.UserSk(),
            cancellationToken: cancellationToken);

        return item is null ? null : MapToUser(item);
    }

    /// <summary>
    /// Looks up a user by email address using GSI1 (GSI1PK=GSI1_USER, GSI1SK=USER#{email}).
    /// Used during authentication to find the user record for credential verification.
    /// </summary>
    /// <param name="email">The email address to look up.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>The user, or null if not found.</returns>
    public async Task<User?> GetByEmailAsync(string email, CancellationToken cancellationToken = default)
    {
        var items = await QueryAsync(
            keyConditionExpression: "#gsi1pk = :gsi1pk AND #gsi1sk = :gsi1sk",
            expressionAttributeValues: new Dictionary<string, AttributeValue>
            {
                [":gsi1pk"] = AttributeValueSerializer.ToS(KeyBuilder.UserGsi1Pk()),
                [":gsi1sk"] = AttributeValueSerializer.ToS(KeyBuilder.UserGsi1Sk(email))
            },
            expressionAttributeNames: new Dictionary<string, string>
            {
                ["#gsi1pk"] = DynamoDbContext.Gsi1Pk,
                ["#gsi1sk"] = DynamoDbContext.Gsi1Sk
            },
            indexName: DynamoDbContext.Gsi1IndexName,
            limit: 1,
            cancellationToken: cancellationToken);

        return items.Count == 0 ? null : MapToUser(items[0]);
    }

    /// <summary>
    /// Updates an existing user record (full replacement).
    /// </summary>
    /// <param name="user">The user with updated values.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    public async Task UpdateAsync(User user, CancellationToken cancellationToken = default)
    {
        var item = BuildUserItem(user);
        await PutItemAsync(item, cancellationToken: cancellationToken);
    }

    /// <summary>
    /// Lists all users by querying GSI1 with the fixed partition key GSI1_USER.
    /// </summary>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>List of all users.</returns>
    public async Task<List<User>> ListAllAsync(CancellationToken cancellationToken = default)
    {
        var items = await QueryAsync(
            keyConditionExpression: "#gsi1pk = :gsi1pk",
            expressionAttributeValues: new Dictionary<string, AttributeValue>
            {
                [":gsi1pk"] = AttributeValueSerializer.ToS(KeyBuilder.UserGsi1Pk())
            },
            expressionAttributeNames: new Dictionary<string, string>
            {
                ["#gsi1pk"] = DynamoDbContext.Gsi1Pk
            },
            indexName: DynamoDbContext.Gsi1IndexName,
            cancellationToken: cancellationToken);

        return items.Select(MapToUser).ToList();
    }

    /// <summary>
    /// Deletes a user by their identifier.
    /// </summary>
    /// <param name="userId">The user identifier.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    public async Task DeleteAsync(string userId, CancellationToken cancellationToken = default)
    {
        await DeleteItemAsync(
            KeyBuilder.UserPk(userId),
            KeyBuilder.UserSk(),
            cancellationToken);
    }

    private static Dictionary<string, AttributeValue> BuildUserItem(User user)
    {
        return AttributeValueSerializer.NewItem(
                KeyBuilder.UserPk(user.UserId),
                KeyBuilder.UserSk())
            .WithGsi1(KeyBuilder.UserGsi1Pk(), KeyBuilder.UserGsi1Sk(user.Email))
            .WithString("userId", user.UserId)
            .WithString("email", user.Email)
            .WithString("name", user.Name)
            .WithString("passwordHash", user.PasswordHash)
            .WithString("role", user.Role)
            .WithNullableString("outletId", user.OutletId)
            .WithBool("isActive", user.IsActive)
            .WithDateTime("createdAt", user.CreatedAt)
            .WithDateTime("updatedAt", user.UpdatedAt)
            .Build();
    }

    private static User MapToUser(Dictionary<string, AttributeValue> item) =>
        new(
            UserId: AttributeValueSerializer.GetRequiredString(item, "userId"),
            Email: AttributeValueSerializer.GetRequiredString(item, "email"),
            Name: AttributeValueSerializer.GetRequiredString(item, "name"),
            PasswordHash: AttributeValueSerializer.GetRequiredString(item, "passwordHash"),
            Role: AttributeValueSerializer.GetRequiredString(item, "role"),
            OutletId: AttributeValueSerializer.GetString(item, "outletId"),
            IsActive: AttributeValueSerializer.GetBool(item, "isActive"),
            CreatedAt: AttributeValueSerializer.GetDateTime(item, "createdAt"),
            UpdatedAt: AttributeValueSerializer.GetDateTime(item, "updatedAt")
        );
}
