using Xunit;
using VELoyalty.Core.Validation;

namespace VELoyalty.Core.Tests.Validation;

public class TransactionValidatorTests
{
    [Fact]
    public void Validate_AllFieldsPresent_ValidAmount_ValidDate_ReturnsSuccess()
    {
        var transaction = new RawTransaction("CUST001", "+8801712345678", "OUT001", "2024-06-15", "1500.50");

        var result = TransactionValidator.Validate(transaction);

        Assert.True(result.IsValid);
        Assert.Empty(result.Errors);
    }

    [Theory]
    [InlineData(null, "+8801712345678", "OUT001", "2024-06-15", "100")]
    [InlineData("", "+8801712345678", "OUT001", "2024-06-15", "100")]
    [InlineData("  ", "+8801712345678", "OUT001", "2024-06-15", "100")]
    public void Validate_MissingCustomerId_ReturnsFailure(string? customerId, string phone, string outlet, string date, string amount)
    {
        var transaction = new RawTransaction(customerId, phone, outlet, date, amount);

        var result = TransactionValidator.Validate(transaction);

        Assert.False(result.IsValid);
        Assert.Contains(result.Errors, e => e.Contains("Customer identifier"));
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("  ")]
    public void Validate_MissingPhone_ReturnsFailure(string? phone)
    {
        var transaction = new RawTransaction("CUST001", phone, "OUT001", "2024-06-15", "100");

        var result = TransactionValidator.Validate(transaction);

        Assert.False(result.IsValid);
        Assert.Contains(result.Errors, e => e.Contains("phone number"));
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("  ")]
    public void Validate_MissingOutletId_ReturnsFailure(string? outletId)
    {
        var transaction = new RawTransaction("CUST001", "+8801712345678", outletId, "2024-06-15", "100");

        var result = TransactionValidator.Validate(transaction);

        Assert.False(result.IsValid);
        Assert.Contains(result.Errors, e => e.Contains("Outlet identifier"));
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("  ")]
    public void Validate_MissingPurchaseDate_ReturnsFailure(string? date)
    {
        var transaction = new RawTransaction("CUST001", "+8801712345678", "OUT001", date, "100");

        var result = TransactionValidator.Validate(transaction);

        Assert.False(result.IsValid);
        Assert.Contains(result.Errors, e => e.Contains("Purchase date"));
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("  ")]
    public void Validate_MissingPurchaseAmount_ReturnsFailure(string? amount)
    {
        var transaction = new RawTransaction("CUST001", "+8801712345678", "OUT001", "2024-06-15", amount);

        var result = TransactionValidator.Validate(transaction);

        Assert.False(result.IsValid);
        Assert.Contains(result.Errors, e => e.Contains("Purchase amount"));
    }

    [Fact]
    public void Validate_AllFieldsMissing_ReturnsMultipleErrors()
    {
        var transaction = new RawTransaction(null, null, null, null, null);

        var result = TransactionValidator.Validate(transaction);

        Assert.False(result.IsValid);
        Assert.Equal(5, result.Errors.Count);
    }

    [Theory]
    [InlineData("abc")]
    [InlineData("not-a-number")]
    [InlineData("12.34.56")]
    public void Validate_NonNumericAmount_ReturnsFailure(string amount)
    {
        var transaction = new RawTransaction("CUST001", "+8801712345678", "OUT001", "2024-06-15", amount);

        var result = TransactionValidator.Validate(transaction);

        Assert.False(result.IsValid);
        Assert.Contains(result.Errors, e => e.Contains("valid numeric"));
    }

    [Theory]
    [InlineData("0")]
    [InlineData("0.00")]
    [InlineData("0.001")]
    [InlineData("-1")]
    public void Validate_AmountBelowMinimum_ReturnsFailure(string amount)
    {
        var transaction = new RawTransaction("CUST001", "+8801712345678", "OUT001", "2024-06-15", amount);

        var result = TransactionValidator.Validate(transaction);

        Assert.False(result.IsValid);
        Assert.Contains(result.Errors, e => e.Contains("at least"));
    }

    [Theory]
    [InlineData("1000000000")]
    [InlineData("999999999.999")]
    public void Validate_AmountAboveMaximum_ReturnsFailure(string amount)
    {
        var transaction = new RawTransaction("CUST001", "+8801712345678", "OUT001", "2024-06-15", amount);

        var result = TransactionValidator.Validate(transaction);

        Assert.False(result.IsValid);
        Assert.Contains(result.Errors, e => e.Contains("not exceed"));
    }

    [Theory]
    [InlineData("0.01")]
    [InlineData("1")]
    [InlineData("999999999.99")]
    [InlineData("500000")]
    public void Validate_AmountWithinRange_ReturnsSuccess(string amount)
    {
        var transaction = new RawTransaction("CUST001", "+8801712345678", "OUT001", "2024-06-15", amount);

        var result = TransactionValidator.Validate(transaction);

        Assert.True(result.IsValid);
    }

    [Theory]
    [InlineData("not-a-date")]
    [InlineData("32-13-2024")]
    [InlineData("abcdef")]
    public void Validate_UnparseableDate_ReturnsFailure(string date)
    {
        var transaction = new RawTransaction("CUST001", "+8801712345678", "OUT001", date, "100");

        var result = TransactionValidator.Validate(transaction);

        Assert.False(result.IsValid);
        Assert.Contains(result.Errors, e => e.Contains("valid date"));
    }

    [Theory]
    [InlineData("2024-06-15")]
    [InlineData("2024-01-01")]
    [InlineData("2023-12-31T10:30:00")]
    public void Validate_ParseableDate_ReturnsSuccess(string date)
    {
        var transaction = new RawTransaction("CUST001", "+8801712345678", "OUT001", date, "100");

        var result = TransactionValidator.Validate(transaction);

        Assert.True(result.IsValid);
    }
}
