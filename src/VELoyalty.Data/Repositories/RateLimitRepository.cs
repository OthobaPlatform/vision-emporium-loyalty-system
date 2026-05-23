using Amazon.DynamoDBv2;
using Amazon.DynamoDBv2.Model;
using VELoyalty.Core;

namespace VELoyalty.Data.Repositories;

/// <summary>
/// Repository for tracking failed redemption attempts and enforcing rate limiting.
/// Rate limit records auto-expire via DynamoDB TTL after 45 minutes.
/// 
/// Rate limiting rules:
/// - Track failed attempts per verification code within a 15-minute window
/// - Block further attempts for 30 minutes after 5 failures in a window
/// </summary>
public class RateLimitRepository : DynamoDbRepository
{
    /// <summary>
    /// TTL duration for rate limit records (45 minutes).
    /// Records auto-expire after this period to keep the table clean.
    /// </summary>
    private static readonly TimeSpan TtlDuration = TimeSpan.FromMinutes(45);

    /// <summary>
    /// DynamoDB TTL attribute name.
    /// </summary>
    private const string TtlAttribute = "TTL";

    public RateLimitRepository(DynamoDbContext context) : base(context)
    {
    }

    /// <summary>
    /// Increments the failed attempt count for a verification code in the current rate limit window.
    /// Creates the record if it doesn't exist, or increments the existing counter.
    /// </summary>
    /// <param name="code">The verification code being rate-limited.</param>
    /// <param name="utcNow">The current UTC time.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>The updated attempt count after incrementing.</returns>
    public async Task<int> IncrementAttemptsAsync(
        string code,
        DateTime utcNow,
        CancellationToken cancellationToken = default)
    {
        var windowStart = GetWindowStart(utcNow);
        var pk = KeyBuilder.RateLimitPk(code);
        var sk = KeyBuilder.RateLimitSk(windowStart);
        var ttlEpoch = new DateTimeOffset(utcNow.Add(TtlDuration)).ToUnixTimeSeconds();

        // Use UpdateItem with ADD to atomically increment the counter
        var request = new UpdateItemRequest
        {
            TableName = Context.Table,
            Key = new Dictionary<string, AttributeValue>
            {
                [DynamoDbContext.PkAttribute] = AttributeValueSerializer.ToS(pk),
                [DynamoDbContext.SkAttribute] = AttributeValueSerializer.ToS(sk)
            },
            UpdateExpression = "ADD #attempts :inc SET #ttl = :ttl",
            ExpressionAttributeNames = new Dictionary<string, string>
            {
                ["#attempts"] = "Attempts",
                ["#ttl"] = TtlAttribute
            },
            ExpressionAttributeValues = new Dictionary<string, AttributeValue>
            {
                [":inc"] = AttributeValueSerializer.ToN(1),
                [":ttl"] = AttributeValueSerializer.ToN(ttlEpoch)
            },
            ReturnValues = ReturnValue.ALL_NEW
        };

        var response = await Context.Client.UpdateItemAsync(request, cancellationToken);
        return AttributeValueSerializer.GetInt(response.Attributes, "Attempts");
    }

    /// <summary>
    /// Gets the current number of failed attempts for a verification code in the active window.
    /// </summary>
    /// <param name="code">The verification code.</param>
    /// <param name="utcNow">The current UTC time.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>The number of failed attempts in the current window.</returns>
    public async Task<int> GetAttemptsAsync(
        string code,
        DateTime utcNow,
        CancellationToken cancellationToken = default)
    {
        var windowStart = GetWindowStart(utcNow);
        var pk = KeyBuilder.RateLimitPk(code);
        var sk = KeyBuilder.RateLimitSk(windowStart);

        var item = await GetItemAsync(pk, sk, cancellationToken: cancellationToken);
        if (item is null)
            return 0;

        return AttributeValueSerializer.GetInt(item, "Attempts");
    }

    /// <summary>
    /// Checks whether a verification code is currently blocked due to rate limiting.
    /// A code is blocked if it has a BlockedUntil timestamp that is in the future.
    /// </summary>
    /// <param name="code">The verification code.</param>
    /// <param name="utcNow">The current UTC time.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>True if the code is currently blocked; false otherwise.</returns>
    public async Task<bool> IsBlockedAsync(
        string code,
        DateTime utcNow,
        CancellationToken cancellationToken = default)
    {
        var blockedUntil = await GetBlockedUntilAsync(code, utcNow, cancellationToken);
        return blockedUntil.HasValue && blockedUntil.Value > utcNow;
    }

    /// <summary>
    /// Gets the BlockedUntil timestamp for a verification code, if any.
    /// </summary>
    /// <param name="code">The verification code.</param>
    /// <param name="utcNow">The current UTC time.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>The BlockedUntil DateTime if set; null otherwise.</returns>
    public async Task<DateTime?> GetBlockedUntilAsync(
        string code,
        DateTime utcNow,
        CancellationToken cancellationToken = default)
    {
        var windowStart = GetWindowStart(utcNow);
        var pk = KeyBuilder.RateLimitPk(code);
        var sk = KeyBuilder.RateLimitSk(windowStart);

        var item = await GetItemAsync(pk, sk, cancellationToken: cancellationToken);
        if (item is null)
            return null;

        return AttributeValueSerializer.GetNullableDateTime(item, "BlockedUntil");
    }

    /// <summary>
    /// Sets the block status for a verification code, blocking further attempts
    /// for the configured block duration (30 minutes).
    /// </summary>
    /// <param name="code">The verification code to block.</param>
    /// <param name="utcNow">The current UTC time.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    public async Task SetBlockedAsync(
        string code,
        DateTime utcNow,
        CancellationToken cancellationToken = default)
    {
        var windowStart = GetWindowStart(utcNow);
        var pk = KeyBuilder.RateLimitPk(code);
        var sk = KeyBuilder.RateLimitSk(windowStart);
        var blockedUntil = utcNow.AddMinutes(Constants.RateLimitBlockMinutes);
        var ttlEpoch = new DateTimeOffset(blockedUntil.Add(TtlDuration)).ToUnixTimeSeconds();

        await UpdateItemAsync(
            pk, sk,
            updateExpression: "SET #blockedUntil = :blockedUntil, #ttl = :ttl",
            expressionAttributeValues: new Dictionary<string, AttributeValue>
            {
                [":blockedUntil"] = AttributeValueSerializer.ToDateTime(blockedUntil),
                [":ttl"] = AttributeValueSerializer.ToN(ttlEpoch)
            },
            expressionAttributeNames: new Dictionary<string, string>
            {
                ["#blockedUntil"] = "BlockedUntil",
                ["#ttl"] = TtlAttribute
            },
            cancellationToken: cancellationToken);
    }

    /// <summary>
    /// Records a failed attempt and applies rate limiting if the threshold is exceeded.
    /// This is a convenience method that combines IncrementAttempts and SetBlocked.
    /// </summary>
    /// <param name="code">The verification code.</param>
    /// <param name="utcNow">The current UTC time.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>
    /// A tuple containing:
    /// - Attempts: the updated attempt count
    /// - IsBlocked: whether the code is now blocked
    /// - BlockedUntil: the block expiry time if blocked, null otherwise
    /// </returns>
    public async Task<(int Attempts, bool IsBlocked, DateTime? BlockedUntil)> RecordFailedAttemptAsync(
        string code,
        DateTime utcNow,
        CancellationToken cancellationToken = default)
    {
        var attempts = await IncrementAttemptsAsync(code, utcNow, cancellationToken);

        if (attempts >= Constants.MaxRedemptionAttempts)
        {
            await SetBlockedAsync(code, utcNow, cancellationToken);
            var blockedUntil = utcNow.AddMinutes(Constants.RateLimitBlockMinutes);
            return (attempts, true, blockedUntil);
        }

        return (attempts, false, null);
    }

    /// <summary>
    /// Calculates the start of the rate limit window for a given time.
    /// Windows are aligned to 15-minute boundaries (e.g., 10:00, 10:15, 10:30, 10:45).
    /// </summary>
    /// <param name="utcNow">The current UTC time.</param>
    /// <returns>The start of the current rate limit window.</returns>
    private static DateTime GetWindowStart(DateTime utcNow)
    {
        var windowMinutes = Constants.RateLimitWindowMinutes;
        var totalMinutes = (int)utcNow.TimeOfDay.TotalMinutes;
        var windowStartMinutes = (totalMinutes / windowMinutes) * windowMinutes;

        return utcNow.Date.AddMinutes(windowStartMinutes);
    }
}
