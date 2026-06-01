using System.Globalization;
using Amazon.DynamoDBv2.Model;

namespace VELoyalty.Data;

/// <summary>
/// Helpers to convert between .NET types and DynamoDB AttributeValue dictionaries.
/// </summary>
public static class AttributeValueSerializer
{
    // ─── To AttributeValue ──────────────────────────────────────────────────────

    /// <summary>Creates a string AttributeValue.</summary>
    public static AttributeValue ToS(string value) => new() { S = value };

    /// <summary>Creates a numeric AttributeValue from an integer.</summary>
    public static AttributeValue ToN(int value) => new() { N = value.ToString(CultureInfo.InvariantCulture) };

    /// <summary>Creates a numeric AttributeValue from a long.</summary>
    public static AttributeValue ToN(long value) => new() { N = value.ToString(CultureInfo.InvariantCulture) };

    /// <summary>Creates a numeric AttributeValue from a decimal.</summary>
    public static AttributeValue ToN(decimal value) => new() { N = value.ToString("F2", CultureInfo.InvariantCulture) };

    /// <summary>Creates a boolean AttributeValue.</summary>
    public static AttributeValue ToBool(bool value) => new() { BOOL = value };

    /// <summary>Creates a string AttributeValue from a DateTime (ISO 8601 UTC).</summary>
    public static AttributeValue ToDateTime(DateTime value) =>
        new() { S = value.ToUniversalTime().ToString("o", CultureInfo.InvariantCulture) };

    /// <summary>Creates a string AttributeValue from a DateOnly (yyyy-MM-dd).</summary>
    public static AttributeValue ToDate(DateOnly value) =>
        new() { S = value.ToString("yyyy-MM-dd", CultureInfo.InvariantCulture) };

    /// <summary>Creates a list AttributeValue from a list of strings.</summary>
    public static AttributeValue ToStringList(List<string> values) =>
        new() { L = values.Select(v => ToS(v)).ToList() };

    /// <summary>Creates a map AttributeValue from a dictionary of string key-value pairs.</summary>
    public static AttributeValue ToMap(Dictionary<string, string> values) =>
        new() { M = values.ToDictionary(kv => kv.Key, kv => ToS(kv.Value)) };

    /// <summary>Creates a NULL AttributeValue.</summary>
    public static AttributeValue ToNull() => new() { NULL = true };

    /// <summary>
    /// Creates a string or NULL AttributeValue depending on whether the value is null.
    /// </summary>
    public static AttributeValue ToNullableS(string? value) =>
        value is null ? ToNull() : ToS(value);

    // ─── From AttributeValue ────────────────────────────────────────────────────

    /// <summary>Extracts a string from an AttributeValue, or returns null if not present.</summary>
    public static string? GetString(Dictionary<string, AttributeValue> item, string key) =>
        item.TryGetValue(key, out var av) && av.S is not null ? av.S : null;

    /// <summary>Extracts a required string from an AttributeValue.</summary>
    public static string GetRequiredString(Dictionary<string, AttributeValue> item, string key) =>
        GetString(item, key) ?? throw new InvalidOperationException($"Missing required attribute: {key}");

    /// <summary>Extracts an integer from a numeric AttributeValue.</summary>
    public static int GetInt(Dictionary<string, AttributeValue> item, string key) =>
        item.TryGetValue(key, out var av) && av.N is not null
            ? int.Parse(av.N, CultureInfo.InvariantCulture)
            : 0;

    /// <summary>Extracts a decimal from a numeric AttributeValue.</summary>
    public static decimal GetDecimal(Dictionary<string, AttributeValue> item, string key) =>
        item.TryGetValue(key, out var av) && av.N is not null
            ? decimal.Parse(av.N, CultureInfo.InvariantCulture)
            : 0m;

    /// <summary>Extracts a boolean from an AttributeValue.</summary>
    public static bool GetBool(Dictionary<string, AttributeValue> item, string key) =>
        item.TryGetValue(key, out var av) && av.IsBOOLSet && av.BOOL;

    /// <summary>Extracts a DateTime from a string AttributeValue (ISO 8601).</summary>
    public static DateTime GetDateTime(Dictionary<string, AttributeValue> item, string key) =>
        item.TryGetValue(key, out var av) && av.S is not null
            ? DateTime.Parse(av.S, CultureInfo.InvariantCulture, DateTimeStyles.RoundtripKind)
            : DateTime.MinValue;

    /// <summary>Extracts a nullable DateTime from a string AttributeValue (ISO 8601).</summary>
    public static DateTime? GetNullableDateTime(Dictionary<string, AttributeValue> item, string key) =>
        item.TryGetValue(key, out var av) && av.S is not null
            ? DateTime.Parse(av.S, CultureInfo.InvariantCulture, DateTimeStyles.RoundtripKind)
            : null;

    /// <summary>Extracts a DateOnly from a string AttributeValue (yyyy-MM-dd).</summary>
    public static DateOnly GetDateOnly(Dictionary<string, AttributeValue> item, string key) =>
        item.TryGetValue(key, out var av) && av.S is not null
            ? DateOnly.Parse(av.S, CultureInfo.InvariantCulture)
            : DateOnly.MinValue;

    /// <summary>Extracts a list of strings from a list AttributeValue.</summary>
    public static List<string> GetStringList(Dictionary<string, AttributeValue> item, string key) =>
        item.TryGetValue(key, out var av) && av.L is not null
            ? av.L.Where(v => v.S is not null).Select(v => v.S).ToList()
            : [];

    /// <summary>Extracts a dictionary of string key-value pairs from a map AttributeValue.</summary>
    public static Dictionary<string, string> GetStringMap(Dictionary<string, AttributeValue> item, string key) =>
        item.TryGetValue(key, out var av) && av.M is not null
            ? av.M.Where(kv => kv.Value.S is not null).ToDictionary(kv => kv.Key, kv => kv.Value.S)
            : new Dictionary<string, string>();

    // ─── Item Builder ───────────────────────────────────────────────────────────

    /// <summary>
    /// Starts building a DynamoDB item with PK and SK.
    /// </summary>
    public static ItemBuilder NewItem(string pk, string sk) => new(pk, sk);
}

/// <summary>
/// Fluent builder for constructing DynamoDB item attribute dictionaries.
/// </summary>
public sealed class ItemBuilder
{
    private readonly Dictionary<string, AttributeValue> _item;

    public ItemBuilder(string pk, string sk)
    {
        _item = new Dictionary<string, AttributeValue>
        {
            [DynamoDbContext.PkAttribute] = AttributeValueSerializer.ToS(pk),
            [DynamoDbContext.SkAttribute] = AttributeValueSerializer.ToS(sk)
        };
    }

    /// <summary>Adds a string attribute.</summary>
    public ItemBuilder WithString(string key, string value)
    {
        _item[key] = AttributeValueSerializer.ToS(value);
        return this;
    }

    /// <summary>Adds a nullable string attribute (stores NULL if value is null).</summary>
    public ItemBuilder WithNullableString(string key, string? value)
    {
        _item[key] = AttributeValueSerializer.ToNullableS(value);
        return this;
    }

    /// <summary>Adds an integer attribute.</summary>
    public ItemBuilder WithInt(string key, int value)
    {
        _item[key] = AttributeValueSerializer.ToN(value);
        return this;
    }

    /// <summary>Adds a decimal attribute.</summary>
    public ItemBuilder WithDecimal(string key, decimal value)
    {
        _item[key] = AttributeValueSerializer.ToN(value);
        return this;
    }

    /// <summary>Adds a boolean attribute.</summary>
    public ItemBuilder WithBool(string key, bool value)
    {
        _item[key] = AttributeValueSerializer.ToBool(value);
        return this;
    }

    /// <summary>Adds a DateTime attribute (stored as ISO 8601 UTC string).</summary>
    public ItemBuilder WithDateTime(string key, DateTime value)
    {
        _item[key] = AttributeValueSerializer.ToDateTime(value);
        return this;
    }

    /// <summary>Adds a DateOnly attribute (stored as yyyy-MM-dd string).</summary>
    public ItemBuilder WithDate(string key, DateOnly value)
    {
        _item[key] = AttributeValueSerializer.ToDate(value);
        return this;
    }

    /// <summary>Adds a string list attribute. Skips if the list is empty (DynamoDB Local rejects empty L).</summary>
    public ItemBuilder WithStringList(string key, List<string> values)
    {
        if (values.Count > 0)
        {
            _item[key] = AttributeValueSerializer.ToStringList(values);
        }
        return this;
    }

    /// <summary>Adds a string map attribute.</summary>
    public ItemBuilder WithStringMap(string key, Dictionary<string, string> values)
    {
        _item[key] = AttributeValueSerializer.ToMap(values);
        return this;
    }

    /// <summary>Adds a GSI1 key pair.</summary>
    public ItemBuilder WithGsi1(string gsi1Pk, string gsi1Sk)
    {
        _item[DynamoDbContext.Gsi1Pk] = AttributeValueSerializer.ToS(gsi1Pk);
        _item[DynamoDbContext.Gsi1Sk] = AttributeValueSerializer.ToS(gsi1Sk);
        return this;
    }

    /// <summary>Adds a GSI2 key pair.</summary>
    public ItemBuilder WithGsi2(string gsi2Pk, string gsi2Sk)
    {
        _item[DynamoDbContext.Gsi2Pk] = AttributeValueSerializer.ToS(gsi2Pk);
        _item[DynamoDbContext.Gsi2Sk] = AttributeValueSerializer.ToS(gsi2Sk);
        return this;
    }

    /// <summary>Adds a TTL attribute (Unix epoch seconds).</summary>
    public ItemBuilder WithTtl(string key, DateTime expiresAt)
    {
        var epoch = new DateTimeOffset(expiresAt.ToUniversalTime()).ToUnixTimeSeconds();
        _item[key] = AttributeValueSerializer.ToN(epoch);
        return this;
    }

    /// <summary>Builds the final attribute dictionary.</summary>
    public Dictionary<string, AttributeValue> Build() => _item;
}
