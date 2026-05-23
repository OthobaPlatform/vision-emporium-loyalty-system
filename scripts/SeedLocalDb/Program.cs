using Amazon.DynamoDBv2;
using Amazon.DynamoDBv2.Model;

var config = new AmazonDynamoDBConfig { ServiceURL = "http://localhost:8000" };
var client = new AmazonDynamoDBClient("fakeKey", "fakeSecret", config);

var passwordHash = BCrypt.Net.BCrypt.HashPassword("Admin123!", 12);
Console.WriteLine($"Generated hash: {passwordHash}");

// Seed admin user
await client.PutItemAsync(new PutItemRequest
{
    TableName = "VELoyalty",
    Item = new Dictionary<string, AttributeValue>
    {
        ["PK"] = new() { S = "USER#admin-001" },
        ["SK"] = new() { S = "META" },
        ["GSI1PK"] = new() { S = "GSI1_USER" },
        ["GSI1SK"] = new() { S = "USER#admin@veloyalty.com" },
        ["userId"] = new() { S = "admin-001" },
        ["email"] = new() { S = "admin@veloyalty.com" },
        ["name"] = new() { S = "System Admin" },
        ["passwordHash"] = new() { S = passwordHash },
        ["role"] = new() { S = "Admin" },
        ["isActive"] = new() { BOOL = true },
        ["createdAt"] = new() { S = "2024-01-01T00:00:00Z" },
        ["updatedAt"] = new() { S = "2024-01-01T00:00:00Z" }
    }
});
Console.WriteLine("Admin user seeded: admin@veloyalty.com / Admin123!");

// Seed outlet
await client.PutItemAsync(new PutItemRequest
{
    TableName = "VELoyalty",
    Item = new Dictionary<string, AttributeValue>
    {
        ["PK"] = new() { S = "OUTLET#OTL-001" },
        ["SK"] = new() { S = "META" },
        ["GSI1PK"] = new() { S = "GSI1_OUTLET" },
        ["GSI1SK"] = new() { S = "OUTLET#OTL-001" },
        ["outletId"] = new() { S = "OTL-001" },
        ["name"] = new() { S = "Vision Emporium - Gulshan" },
        ["address"] = new() { S = "Gulshan-2, Dhaka" },
        ["phoneNumber"] = new() { S = "+8801711000001" },
        ["assignedManagerId"] = new() { S = "admin-001" },
        ["isActive"] = new() { BOOL = true }
    }
});
Console.WriteLine("Outlet seeded: Vision Emporium - Gulshan");

// Seed active cycle
await client.PutItemAsync(new PutItemRequest
{
    TableName = "VELoyalty",
    Item = new Dictionary<string, AttributeValue>
    {
        ["PK"] = new() { S = "CONFIG" },
        ["SK"] = new() { S = "CYCLE#2025-2026" },
        ["CycleId"] = new() { S = "2025-2026" },
        ["StartDate"] = new() { S = "2025-06-01" },
        ["EndDate"] = new() { S = "2026-05-31" },
        ["IsActive"] = new() { BOOL = true }
    }
});
Console.WriteLine("Loyalty cycle seeded: 2025-2026");

// Seed general config
await client.PutItemAsync(new PutItemRequest
{
    TableName = "VELoyalty",
    Item = new Dictionary<string, AttributeValue>
    {
        ["PK"] = new() { S = "CONFIG" },
        ["SK"] = new() { S = "SETTINGS#GENERAL" },
        ["SyncIntervalMinutes"] = new() { N = "60" },
        ["CodeExpiryDays"] = new() { N = "30" },
        ["MinPurchaseAmount"] = new() { N = "100" }
    }
});
Console.WriteLine("General config seeded");

Console.WriteLine("\nLocal database initialized successfully!");
