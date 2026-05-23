using Xunit;
using VELoyalty.Core.Validation;

namespace VELoyalty.Core.Tests.Validation;

public class ExcelSchemaValidatorTests
{
    [Fact]
    public void ValidateColumns_AllRequiredPresent_ReturnsSuccess()
    {
        var headers = new List<string>
        {
            "Customer Identifier",
            "Customer Name",
            "Customer Phone Number",
            "Outlet Identifier",
            "Purchase Date",
            "Purchase Amount",
            "Product Category"
        };

        var result = ExcelSchemaValidator.ValidateColumns(headers);

        Assert.True(result.IsValid);
    }

    [Fact]
    public void ValidateColumns_CaseInsensitive_ReturnsSuccess()
    {
        var headers = new List<string>
        {
            "CUSTOMER IDENTIFIER",
            "customer name",
            "Customer Phone Number",
            "OUTLET IDENTIFIER",
            "purchase date",
            "Purchase Amount",
            "PRODUCT CATEGORY"
        };

        var result = ExcelSchemaValidator.ValidateColumns(headers);

        Assert.True(result.IsValid);
    }

    [Fact]
    public void ValidateColumns_MissingColumn_ReturnsFailure()
    {
        var headers = new List<string>
        {
            "Customer Identifier",
            "Customer Name",
            "Customer Phone Number",
            "Outlet Identifier",
            "Purchase Date",
            "Purchase Amount"
            // Missing "Product Category"
        };

        var result = ExcelSchemaValidator.ValidateColumns(headers);

        Assert.False(result.IsValid);
        Assert.Contains(result.Errors, e => e.Contains("product category"));
    }

    [Fact]
    public void ValidateColumns_MultipleMissing_ReportsAll()
    {
        var headers = new List<string>
        {
            "Customer Identifier",
            "Customer Name"
        };

        var result = ExcelSchemaValidator.ValidateColumns(headers);

        Assert.False(result.IsValid);
        Assert.True(result.Errors.Count >= 5); // At least 5 missing columns
    }

    [Fact]
    public void ValidateColumns_EmptyHeaders_ReturnsFailure()
    {
        var result = ExcelSchemaValidator.ValidateColumns(new List<string>());

        Assert.False(result.IsValid);
        Assert.Contains(result.Errors, e => e.Contains("No columns"));
    }

    [Fact]
    public void ValidateColumns_NullHeaders_ReturnsFailure()
    {
        var result = ExcelSchemaValidator.ValidateColumns(null!);

        Assert.False(result.IsValid);
    }

    [Fact]
    public void ValidateRow_AllValidData_ReturnsSuccess()
    {
        var row = new Dictionary<string, string?>
        {
            ["customer identifier"] = "CUST001",
            ["customer name"] = "John Doe",
            ["customer phone number"] = "+8801712345678",
            ["outlet identifier"] = "OUT001",
            ["purchase date"] = "2024-06-15",
            ["purchase amount"] = "1500.50",
            ["product category"] = "Electronics"
        };

        var result = ExcelSchemaValidator.ValidateRow(row, 1);

        Assert.True(result.IsValid);
    }

    [Fact]
    public void ValidateRow_EmptyCustomerId_ReturnsFailure()
    {
        var row = CreateValidRow();
        row["customer identifier"] = "";

        var result = ExcelSchemaValidator.ValidateRow(row, 2);

        Assert.False(result.IsValid);
        Assert.Contains(result.Errors, e => e.Contains("Row 2") && e.Contains("Customer identifier"));
    }

    [Fact]
    public void ValidateRow_CustomerNameTooLong_ReturnsFailure()
    {
        var row = CreateValidRow();
        row["customer name"] = new string('A', 201);

        var result = ExcelSchemaValidator.ValidateRow(row, 3);

        Assert.False(result.IsValid);
        Assert.Contains(result.Errors, e => e.Contains("200 characters"));
    }

    [Fact]
    public void ValidateRow_InvalidPhoneNumber_ReturnsFailure()
    {
        var row = CreateValidRow();
        row["customer phone number"] = "invalid-phone";

        var result = ExcelSchemaValidator.ValidateRow(row, 4);

        Assert.False(result.IsValid);
        Assert.Contains(result.Errors, e => e.Contains("phone number") && e.Contains("invalid"));
    }

    [Fact]
    public void ValidateRow_InvalidDate_ReturnsFailure()
    {
        var row = CreateValidRow();
        row["purchase date"] = "not-a-date";

        var result = ExcelSchemaValidator.ValidateRow(row, 5);

        Assert.False(result.IsValid);
        Assert.Contains(result.Errors, e => e.Contains("valid date"));
    }

    [Fact]
    public void ValidateRow_NonNumericAmount_ReturnsFailure()
    {
        var row = CreateValidRow();
        row["purchase amount"] = "abc";

        var result = ExcelSchemaValidator.ValidateRow(row, 6);

        Assert.False(result.IsValid);
        Assert.Contains(result.Errors, e => e.Contains("valid numeric"));
    }

    [Fact]
    public void ValidateRow_AmountBelowMinimum_ReturnsFailure()
    {
        var row = CreateValidRow();
        row["purchase amount"] = "0.00";

        var result = ExcelSchemaValidator.ValidateRow(row, 7);

        Assert.False(result.IsValid);
        Assert.Contains(result.Errors, e => e.Contains("at least"));
    }

    [Fact]
    public void ValidateRow_AmountAboveMaximum_ReturnsFailure()
    {
        var row = CreateValidRow();
        row["purchase amount"] = "1000000000";

        var result = ExcelSchemaValidator.ValidateRow(row, 8);

        Assert.False(result.IsValid);
        Assert.Contains(result.Errors, e => e.Contains("not exceed"));
    }

    [Fact]
    public void ValidateRow_EmptyProductCategory_ReturnsFailure()
    {
        var row = CreateValidRow();
        row["product category"] = "";

        var result = ExcelSchemaValidator.ValidateRow(row, 9);

        Assert.False(result.IsValid);
        Assert.Contains(result.Errors, e => e.Contains("Product category"));
    }

    [Fact]
    public void ValidateRow_MultipleErrors_ReportsAll()
    {
        var row = new Dictionary<string, string?>
        {
            ["customer identifier"] = "",
            ["customer name"] = "",
            ["customer phone number"] = "",
            ["outlet identifier"] = "",
            ["purchase date"] = "",
            ["purchase amount"] = "",
            ["product category"] = ""
        };

        var result = ExcelSchemaValidator.ValidateRow(row, 10);

        Assert.False(result.IsValid);
        Assert.True(result.Errors.Count >= 7); // All fields should have errors
    }

    private static Dictionary<string, string?> CreateValidRow()
    {
        return new Dictionary<string, string?>
        {
            ["customer identifier"] = "CUST001",
            ["customer name"] = "John Doe",
            ["customer phone number"] = "+8801712345678",
            ["outlet identifier"] = "OUT001",
            ["purchase date"] = "2024-06-15",
            ["purchase amount"] = "1500.50",
            ["product category"] = "Electronics"
        };
    }
}
