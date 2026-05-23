using Amazon.DynamoDBv2;
using Amazon.DynamoDBv2.Model;

namespace VELoyalty.Data;

/// <summary>
/// Base repository providing generic DynamoDB operations with exponential backoff retry handling.
/// </summary>
public abstract class DynamoDbRepository
{
    private const int MaxRetries = 3;
    private static readonly TimeSpan BaseDelay = TimeSpan.FromMilliseconds(100);

    protected DynamoDbContext Context { get; }

    protected DynamoDbRepository(DynamoDbContext context)
    {
        Context = context ?? throw new ArgumentNullException(nameof(context));
    }

    /// <summary>
    /// Puts a single item into the table with retry handling.
    /// </summary>
    /// <param name="item">The item attribute dictionary to write.</param>
    /// <param name="conditionExpression">Optional condition expression for conditional writes.</param>
    /// <param name="expressionAttributeNames">Optional expression attribute name mappings.</param>
    /// <param name="expressionAttributeValues">Optional expression attribute value mappings.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    protected async Task PutItemAsync(
        Dictionary<string, AttributeValue> item,
        string? conditionExpression = null,
        Dictionary<string, string>? expressionAttributeNames = null,
        Dictionary<string, AttributeValue>? expressionAttributeValues = null,
        CancellationToken cancellationToken = default)
    {
        var request = new PutItemRequest
        {
            TableName = Context.Table,
            Item = item
        };

        if (conditionExpression is not null)
            request.ConditionExpression = conditionExpression;

        if (expressionAttributeNames is not null)
            request.ExpressionAttributeNames = expressionAttributeNames;

        if (expressionAttributeValues is not null)
            request.ExpressionAttributeValues = expressionAttributeValues;

        await ExecuteWithRetryAsync(
            () => Context.Client.PutItemAsync(request, cancellationToken),
            cancellationToken);
    }

    /// <summary>
    /// Gets a single item by primary key with retry handling.
    /// </summary>
    /// <param name="pk">Partition key value.</param>
    /// <param name="sk">Sort key value.</param>
    /// <param name="consistentRead">Whether to use strongly consistent reads.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>The item attributes, or null if not found.</returns>
    protected async Task<Dictionary<string, AttributeValue>?> GetItemAsync(
        string pk,
        string sk,
        bool consistentRead = false,
        CancellationToken cancellationToken = default)
    {
        var request = new GetItemRequest
        {
            TableName = Context.Table,
            Key = new Dictionary<string, AttributeValue>
            {
                [DynamoDbContext.PkAttribute] = AttributeValueSerializer.ToS(pk),
                [DynamoDbContext.SkAttribute] = AttributeValueSerializer.ToS(sk)
            },
            ConsistentRead = consistentRead
        };

        var response = await ExecuteWithRetryAsync(
            () => Context.Client.GetItemAsync(request, cancellationToken),
            cancellationToken);

        return response.Item is { Count: > 0 } ? response.Item : null;
    }

    /// <summary>
    /// Queries items by partition key and optional sort key condition with retry handling.
    /// </summary>
    /// <param name="keyConditionExpression">Key condition expression (e.g., "PK = :pk AND begins_with(SK, :prefix)").</param>
    /// <param name="expressionAttributeValues">Expression attribute values for the condition.</param>
    /// <param name="expressionAttributeNames">Optional expression attribute name mappings.</param>
    /// <param name="indexName">Optional GSI name to query against.</param>
    /// <param name="filterExpression">Optional filter expression applied after query.</param>
    /// <param name="scanIndexForward">Sort order (true = ascending, false = descending).</param>
    /// <param name="limit">Maximum number of items to return (0 = no limit).</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>List of matching items.</returns>
    protected async Task<List<Dictionary<string, AttributeValue>>> QueryAsync(
        string keyConditionExpression,
        Dictionary<string, AttributeValue> expressionAttributeValues,
        Dictionary<string, string>? expressionAttributeNames = null,
        string? indexName = null,
        string? filterExpression = null,
        bool scanIndexForward = true,
        int limit = 0,
        CancellationToken cancellationToken = default)
    {
        var results = new List<Dictionary<string, AttributeValue>>();
        Dictionary<string, AttributeValue>? lastEvaluatedKey = null;

        do
        {
            var request = new QueryRequest
            {
                TableName = Context.Table,
                KeyConditionExpression = keyConditionExpression,
                ExpressionAttributeValues = expressionAttributeValues,
                ScanIndexForward = scanIndexForward
            };

            if (expressionAttributeNames is not null)
                request.ExpressionAttributeNames = expressionAttributeNames;

            if (indexName is not null)
                request.IndexName = indexName;

            if (filterExpression is not null)
                request.FilterExpression = filterExpression;

            if (limit > 0)
                request.Limit = limit - results.Count;

            if (lastEvaluatedKey is not null)
                request.ExclusiveStartKey = lastEvaluatedKey;

            var response = await ExecuteWithRetryAsync(
                () => Context.Client.QueryAsync(request, cancellationToken),
                cancellationToken);

            results.AddRange(response.Items);
            lastEvaluatedKey = response.LastEvaluatedKey is { Count: > 0 }
                ? response.LastEvaluatedKey
                : null;

        } while (lastEvaluatedKey is not null && (limit == 0 || results.Count < limit));

        return limit > 0 && results.Count > limit
            ? results.Take(limit).ToList()
            : results;
    }

    /// <summary>
    /// Writes items in batches of up to 25 with retry handling for unprocessed items.
    /// </summary>
    /// <param name="items">The items to write.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    protected async Task BatchWriteAsync(
        List<Dictionary<string, AttributeValue>> items,
        CancellationToken cancellationToken = default)
    {
        const int batchSize = 25;

        for (int i = 0; i < items.Count; i += batchSize)
        {
            var batch = items.Skip(i).Take(batchSize).ToList();
            var writeRequests = batch.Select(item => new WriteRequest
            {
                PutRequest = new PutRequest { Item = item }
            }).ToList();

            var request = new BatchWriteItemRequest
            {
                RequestItems = new Dictionary<string, List<WriteRequest>>
                {
                    [Context.Table] = writeRequests
                }
            };

            await ExecuteBatchWithRetryAsync(request, cancellationToken);
        }
    }

    /// <summary>
    /// Deletes a single item by primary key with retry handling.
    /// </summary>
    /// <param name="pk">Partition key value.</param>
    /// <param name="sk">Sort key value.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    protected async Task DeleteItemAsync(
        string pk,
        string sk,
        CancellationToken cancellationToken = default)
    {
        var request = new DeleteItemRequest
        {
            TableName = Context.Table,
            Key = new Dictionary<string, AttributeValue>
            {
                [DynamoDbContext.PkAttribute] = AttributeValueSerializer.ToS(pk),
                [DynamoDbContext.SkAttribute] = AttributeValueSerializer.ToS(sk)
            }
        };

        await ExecuteWithRetryAsync(
            () => Context.Client.DeleteItemAsync(request, cancellationToken),
            cancellationToken);
    }

    /// <summary>
    /// Updates an item with an update expression and retry handling.
    /// </summary>
    /// <param name="pk">Partition key value.</param>
    /// <param name="sk">Sort key value.</param>
    /// <param name="updateExpression">The update expression.</param>
    /// <param name="expressionAttributeValues">Expression attribute values.</param>
    /// <param name="expressionAttributeNames">Optional expression attribute name mappings.</param>
    /// <param name="conditionExpression">Optional condition expression.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    protected async Task UpdateItemAsync(
        string pk,
        string sk,
        string updateExpression,
        Dictionary<string, AttributeValue> expressionAttributeValues,
        Dictionary<string, string>? expressionAttributeNames = null,
        string? conditionExpression = null,
        CancellationToken cancellationToken = default)
    {
        var request = new UpdateItemRequest
        {
            TableName = Context.Table,
            Key = new Dictionary<string, AttributeValue>
            {
                [DynamoDbContext.PkAttribute] = AttributeValueSerializer.ToS(pk),
                [DynamoDbContext.SkAttribute] = AttributeValueSerializer.ToS(sk)
            },
            UpdateExpression = updateExpression,
            ExpressionAttributeValues = expressionAttributeValues
        };

        if (expressionAttributeNames is not null)
            request.ExpressionAttributeNames = expressionAttributeNames;

        if (conditionExpression is not null)
            request.ConditionExpression = conditionExpression;

        await ExecuteWithRetryAsync(
            () => Context.Client.UpdateItemAsync(request, cancellationToken),
            cancellationToken);
    }

    // ─── Retry Logic ────────────────────────────────────────────────────────────

    /// <summary>
    /// Executes a DynamoDB operation with exponential backoff retry on throttling/transient errors.
    /// </summary>
    private static async Task<TResponse> ExecuteWithRetryAsync<TResponse>(
        Func<Task<TResponse>> operation,
        CancellationToken cancellationToken)
    {
        int attempt = 0;

        while (true)
        {
            try
            {
                return await operation();
            }
            catch (ProvisionedThroughputExceededException) when (attempt < MaxRetries)
            {
                await DelayWithBackoff(attempt, cancellationToken);
                attempt++;
            }
            catch (InternalServerErrorException) when (attempt < MaxRetries)
            {
                await DelayWithBackoff(attempt, cancellationToken);
                attempt++;
            }
            catch (Amazon.Runtime.AmazonServiceException ex)
                when (attempt < MaxRetries && IsTransientError(ex))
            {
                await DelayWithBackoff(attempt, cancellationToken);
                attempt++;
            }
        }
    }

    /// <summary>
    /// Executes a BatchWriteItem with retry for unprocessed items using exponential backoff.
    /// </summary>
    private async Task ExecuteBatchWithRetryAsync(
        BatchWriteItemRequest request,
        CancellationToken cancellationToken)
    {
        int attempt = 0;

        while (true)
        {
            BatchWriteItemResponse response;

            try
            {
                response = await Context.Client.BatchWriteItemAsync(request, cancellationToken);
            }
            catch (ProvisionedThroughputExceededException) when (attempt < MaxRetries)
            {
                await DelayWithBackoff(attempt, cancellationToken);
                attempt++;
                continue;
            }
            catch (InternalServerErrorException) when (attempt < MaxRetries)
            {
                await DelayWithBackoff(attempt, cancellationToken);
                attempt++;
                continue;
            }
            catch (Amazon.Runtime.AmazonServiceException ex)
                when (attempt < MaxRetries && IsTransientError(ex))
            {
                await DelayWithBackoff(attempt, cancellationToken);
                attempt++;
                continue;
            }

            // Handle unprocessed items
            if (response.UnprocessedItems is { Count: > 0 })
            {
                if (attempt >= MaxRetries)
                    throw new InvalidOperationException(
                        $"BatchWriteItem still has {response.UnprocessedItems[Context.Table].Count} unprocessed items after {MaxRetries} retries.");

                await DelayWithBackoff(attempt, cancellationToken);
                attempt++;
                request.RequestItems = response.UnprocessedItems;
            }
            else
            {
                break;
            }
        }
    }

    private static async Task DelayWithBackoff(int attempt, CancellationToken cancellationToken)
    {
        var delay = BaseDelay * Math.Pow(2, attempt);
        await Task.Delay(delay, cancellationToken);
    }

    private static bool IsTransientError(Amazon.Runtime.AmazonServiceException ex) =>
        ex.StatusCode == System.Net.HttpStatusCode.ServiceUnavailable ||
        ex.StatusCode == System.Net.HttpStatusCode.TooManyRequests ||
        ex.StatusCode == System.Net.HttpStatusCode.InternalServerError;
}
