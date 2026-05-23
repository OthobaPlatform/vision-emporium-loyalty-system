using Amazon.DynamoDBv2;

namespace VELoyalty.Data;

/// <summary>
/// Holds the DynamoDB client, table name, and GSI name constants for the VELoyalty single-table design.
/// </summary>
public sealed class DynamoDbContext
{
    /// <summary>
    /// DynamoDB table name.
    /// </summary>
    public const string TableName = "VELoyalty";

    /// <summary>
    /// GSI1: Phone lookups, outlet queries.
    /// </summary>
    public const string Gsi1IndexName = "GSI1";

    /// <summary>
    /// GSI1 partition key attribute name.
    /// </summary>
    public const string Gsi1Pk = "GSI1PK";

    /// <summary>
    /// GSI1 sort key attribute name.
    /// </summary>
    public const string Gsi1Sk = "GSI1SK";

    /// <summary>
    /// GSI2: Code lookups, job status queries.
    /// </summary>
    public const string Gsi2IndexName = "GSI2";

    /// <summary>
    /// GSI2 partition key attribute name.
    /// </summary>
    public const string Gsi2Pk = "GSI2PK";

    /// <summary>
    /// GSI2 sort key attribute name.
    /// </summary>
    public const string Gsi2Sk = "GSI2SK";

    /// <summary>
    /// Primary partition key attribute name.
    /// </summary>
    public const string PkAttribute = "PK";

    /// <summary>
    /// Primary sort key attribute name.
    /// </summary>
    public const string SkAttribute = "SK";

    /// <summary>
    /// The DynamoDB client instance.
    /// </summary>
    public IAmazonDynamoDB Client { get; }

    /// <summary>
    /// The resolved table name (allows override for testing).
    /// </summary>
    public string Table { get; }

    /// <summary>
    /// Creates a new DynamoDbContext with the specified client and optional table name override.
    /// </summary>
    /// <param name="client">The IAmazonDynamoDB client.</param>
    /// <param name="tableName">Optional table name override (defaults to "VELoyalty").</param>
    public DynamoDbContext(IAmazonDynamoDB client, string? tableName = null)
    {
        Client = client ?? throw new ArgumentNullException(nameof(client));
        Table = tableName ?? TableName;
    }
}
