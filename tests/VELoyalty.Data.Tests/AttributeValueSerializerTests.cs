using Amazon.DynamoDBv2.Model;
using Xunit;

namespace VELoyalty.Data.Tests;

public class AttributeValueSerializerTests
{
    // ─── ToS ────────────────────────────────────────────────────────────────────

    [Fact]
    public void ToS_ReturnsStringAttributeValue()
    {
        var result = AttributeValueSerializer.ToS("hello");
        Assert.Equal("hello", result.S);
    }

    // ─── ToN (int) ──────────────────────────────────────────────────────────────

    [Fact]
    public void ToN_Int_ReturnsNumericAttributeValue()
    {
        var result = AttributeValueSerializer.ToN(42);
        Assert.Equal("42", result.N);
    }

    // ─── ToN (decimal) ──────────────────────────────────────────────────────────

    [Fact]
    public void ToN_Decimal_ReturnsTwoDecimalPlaces()
    {
        var result = AttributeValueSerializer.ToN(1500.5m);
        Assert.Equal("1500.50", result.N);
    }

    // ─── ToBool ─────────────────────────────────────────────────────────────────

    [Theory]
    [InlineData(true)]
    [InlineData(false)]
    public void ToBool_ReturnsBoolAttributeValue(bool value)
    {
        var result = AttributeValueSerializer.ToBool(value);
        Assert.Equal(value, result.BOOL);
    }

    // ─── ToDateTime ─────────────────────────────────────────────────────────────

    [Fact]
    public void ToDateTime_ReturnsIso8601UtcString()
    {
        var dt = new DateTime(2024, 6, 15, 10, 30, 0, DateTimeKind.Utc);
        var result = AttributeValueSerializer.ToDateTime(dt);
        Assert.Contains("2024-06-15", result.S);
        Assert.Contains("10:30:00", result.S);
    }

    // ─── ToDate ─────────────────────────────────────────────────────────────────

    [Fact]
    public void ToDate_ReturnsYyyyMmDdString()
    {
        var date = new DateOnly(2024, 6, 15);
        var result = AttributeValueSerializer.ToDate(date);
        Assert.Equal("2024-06-15", result.S);
    }

    // ─── ToStringList ───────────────────────────────────────────────────────────

    [Fact]
    public void ToStringList_ReturnsListAttributeValue()
    {
        var list = new List<string> { "Electronics", "Accessories" };
        var result = AttributeValueSerializer.ToStringList(list);
        Assert.Equal(2, result.L.Count);
        Assert.Equal("Electronics", result.L[0].S);
        Assert.Equal("Accessories", result.L[1].S);
    }

    // ─── ToNullableS ────────────────────────────────────────────────────────────

    [Fact]
    public void ToNullableS_WithValue_ReturnsStringAttribute()
    {
        var result = AttributeValueSerializer.ToNullableS("value");
        Assert.Equal("value", result.S);
    }

    [Fact]
    public void ToNullableS_WithNull_ReturnsNullAttribute()
    {
        var result = AttributeValueSerializer.ToNullableS(null);
        Assert.True(result.NULL);
    }

    // ─── GetString ──────────────────────────────────────────────────────────────

    [Fact]
    public void GetString_WhenPresent_ReturnsValue()
    {
        var item = new Dictionary<string, AttributeValue>
        {
            ["name"] = new() { S = "John" }
        };
        Assert.Equal("John", AttributeValueSerializer.GetString(item, "name"));
    }

    [Fact]
    public void GetString_WhenMissing_ReturnsNull()
    {
        var item = new Dictionary<string, AttributeValue>();
        Assert.Null(AttributeValueSerializer.GetString(item, "name"));
    }

    // ─── GetRequiredString ──────────────────────────────────────────────────────

    [Fact]
    public void GetRequiredString_WhenMissing_ThrowsInvalidOperationException()
    {
        var item = new Dictionary<string, AttributeValue>();
        Assert.Throws<InvalidOperationException>(() =>
            AttributeValueSerializer.GetRequiredString(item, "name"));
    }

    // ─── GetInt ─────────────────────────────────────────────────────────────────

    [Fact]
    public void GetInt_WhenPresent_ReturnsValue()
    {
        var item = new Dictionary<string, AttributeValue>
        {
            ["count"] = new() { N = "5" }
        };
        Assert.Equal(5, AttributeValueSerializer.GetInt(item, "count"));
    }

    [Fact]
    public void GetInt_WhenMissing_ReturnsZero()
    {
        var item = new Dictionary<string, AttributeValue>();
        Assert.Equal(0, AttributeValueSerializer.GetInt(item, "count"));
    }

    // ─── GetDecimal ─────────────────────────────────────────────────────────────

    [Fact]
    public void GetDecimal_WhenPresent_ReturnsValue()
    {
        var item = new Dictionary<string, AttributeValue>
        {
            ["amount"] = new() { N = "1500.50" }
        };
        Assert.Equal(1500.50m, AttributeValueSerializer.GetDecimal(item, "amount"));
    }

    // ─── GetBool ────────────────────────────────────────────────────────────────

    [Fact]
    public void GetBool_WhenTrue_ReturnsTrue()
    {
        var item = new Dictionary<string, AttributeValue>
        {
            ["active"] = new() { BOOL = true }
        };
        Assert.True(AttributeValueSerializer.GetBool(item, "active"));
    }

    [Fact]
    public void GetBool_WhenMissing_ReturnsFalse()
    {
        var item = new Dictionary<string, AttributeValue>();
        Assert.False(AttributeValueSerializer.GetBool(item, "active"));
    }

    // ─── GetDateOnly ────────────────────────────────────────────────────────────

    [Fact]
    public void GetDateOnly_WhenPresent_ReturnsDate()
    {
        var item = new Dictionary<string, AttributeValue>
        {
            ["date"] = new() { S = "2024-06-15" }
        };
        Assert.Equal(new DateOnly(2024, 6, 15), AttributeValueSerializer.GetDateOnly(item, "date"));
    }

    // ─── GetStringList ──────────────────────────────────────────────────────────

    [Fact]
    public void GetStringList_WhenPresent_ReturnsList()
    {
        var item = new Dictionary<string, AttributeValue>
        {
            ["categories"] = new()
            {
                L = new List<AttributeValue>
                {
                    new() { S = "Electronics" },
                    new() { S = "Accessories" }
                }
            }
        };
        var result = AttributeValueSerializer.GetStringList(item, "categories");
        Assert.Equal(2, result.Count);
        Assert.Contains("Electronics", result);
        Assert.Contains("Accessories", result);
    }

    [Fact]
    public void GetStringList_WhenMissing_ReturnsEmptyList()
    {
        var item = new Dictionary<string, AttributeValue>();
        var result = AttributeValueSerializer.GetStringList(item, "categories");
        Assert.Empty(result);
    }

    // ─── GetStringMap ───────────────────────────────────────────────────────────

    [Fact]
    public void GetStringMap_WhenPresent_ReturnsDictionary()
    {
        var item = new Dictionary<string, AttributeValue>
        {
            ["details"] = new()
            {
                M = new Dictionary<string, AttributeValue>
                {
                    ["key1"] = new() { S = "value1" },
                    ["key2"] = new() { S = "value2" }
                }
            }
        };
        var result = AttributeValueSerializer.GetStringMap(item, "details");
        Assert.Equal(2, result.Count);
        Assert.Equal("value1", result["key1"]);
        Assert.Equal("value2", result["key2"]);
    }

    // ─── ItemBuilder ────────────────────────────────────────────────────────────

    [Fact]
    public void ItemBuilder_BuildsItemWithPkAndSk()
    {
        var item = AttributeValueSerializer.NewItem("CUST#C001", "PROFILE").Build();

        Assert.Equal("CUST#C001", item["PK"].S);
        Assert.Equal("PROFILE", item["SK"].S);
    }

    [Fact]
    public void ItemBuilder_WithGsi1_AddsGsiKeys()
    {
        var item = AttributeValueSerializer.NewItem("CUST#C001", "PROFILE")
            .WithGsi1("PHONE#+8801712345678", "CUST#C001")
            .Build();

        Assert.Equal("PHONE#+8801712345678", item["GSI1PK"].S);
        Assert.Equal("CUST#C001", item["GSI1SK"].S);
    }

    [Fact]
    public void ItemBuilder_WithGsi2_AddsGsiKeys()
    {
        var item = AttributeValueSerializer.NewItem("CUST#C001", "ELIG#2024#3")
            .WithGsi2("CODE#123456", "ELIG#C001")
            .Build();

        Assert.Equal("CODE#123456", item["GSI2PK"].S);
        Assert.Equal("ELIG#C001", item["GSI2SK"].S);
    }

    [Fact]
    public void ItemBuilder_WithAllTypes_BuildsCompleteItem()
    {
        var item = AttributeValueSerializer.NewItem("CUST#C001", "PROFILE")
            .WithString("name", "John Doe")
            .WithInt("purchases", 5)
            .WithDecimal("amount", 1500.50m)
            .WithBool("active", true)
            .WithDate("startDate", new DateOnly(2024, 6, 1))
            .WithDateTime("createdAt", new DateTime(2024, 6, 15, 10, 0, 0, DateTimeKind.Utc))
            .WithStringList("categories", new List<string> { "Electronics" })
            .WithNullableString("outletId", null)
            .Build();

        Assert.Equal("John Doe", item["name"].S);
        Assert.Equal("5", item["purchases"].N);
        Assert.Equal("1500.50", item["amount"].N);
        Assert.True(item["active"].BOOL);
        Assert.Equal("2024-06-01", item["startDate"].S);
        Assert.Contains("2024-06-15", item["createdAt"].S);
        Assert.Single(item["categories"].L);
        Assert.True(item["outletId"].NULL);
    }

    [Fact]
    public void ItemBuilder_WithTtl_StoresUnixEpochSeconds()
    {
        var expiresAt = new DateTime(2024, 7, 15, 10, 0, 0, DateTimeKind.Utc);
        var item = AttributeValueSerializer.NewItem("RATELIMIT#123456", "WINDOW#2024-06-15T10:00:00Z")
            .WithTtl("ttl", expiresAt)
            .Build();

        var expectedEpoch = new DateTimeOffset(expiresAt).ToUnixTimeSeconds();
        Assert.Equal(expectedEpoch.ToString(), item["ttl"].N);
    }
}
