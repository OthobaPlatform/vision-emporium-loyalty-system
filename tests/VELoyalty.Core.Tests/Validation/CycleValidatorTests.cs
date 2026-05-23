using Xunit;
using VELoyalty.Core.Validation;

namespace VELoyalty.Core.Tests.Validation;

public class CycleValidatorTests
{
    [Fact]
    public void Validate_ValidCycle_30Days_ReturnsSuccess()
    {
        var start = new DateOnly(2024, 6, 1);
        var end = new DateOnly(2024, 7, 1); // 30 days

        var result = CycleValidator.Validate(start, end);

        Assert.True(result.IsValid);
    }

    [Fact]
    public void Validate_ValidCycle_730Days_ReturnsSuccess()
    {
        var start = new DateOnly(2024, 1, 1);
        var end = new DateOnly(2025, 12, 31); // 730 days

        var result = CycleValidator.Validate(start, end);

        Assert.True(result.IsValid);
    }

    [Fact]
    public void Validate_ValidCycle_DefaultPeriod_ReturnsSuccess()
    {
        var start = new DateOnly(2024, 6, 1);
        var end = new DateOnly(2025, 5, 31); // ~365 days

        var result = CycleValidator.Validate(start, end);

        Assert.True(result.IsValid);
    }

    [Fact]
    public void Validate_EndDateBeforeStartDate_ReturnsFailure()
    {
        var start = new DateOnly(2024, 6, 1);
        var end = new DateOnly(2024, 5, 1);

        var result = CycleValidator.Validate(start, end);

        Assert.False(result.IsValid);
        Assert.Contains(result.Errors, e => e.Contains("after the start date"));
    }

    [Fact]
    public void Validate_EndDateEqualsStartDate_ReturnsFailure()
    {
        var start = new DateOnly(2024, 6, 1);
        var end = new DateOnly(2024, 6, 1);

        var result = CycleValidator.Validate(start, end);

        Assert.False(result.IsValid);
        Assert.Contains(result.Errors, e => e.Contains("after the start date"));
    }

    [Fact]
    public void Validate_DurationTooShort_29Days_ReturnsFailure()
    {
        var start = new DateOnly(2024, 6, 1);
        var end = new DateOnly(2024, 6, 30); // 29 days

        var result = CycleValidator.Validate(start, end);

        Assert.False(result.IsValid);
        Assert.Contains(result.Errors, e => e.Contains("at least 30 days"));
    }

    [Fact]
    public void Validate_DurationTooLong_731Days_ReturnsFailure()
    {
        var start = new DateOnly(2024, 1, 1);
        var end = new DateOnly(2026, 1, 1); // 731 days

        var result = CycleValidator.Validate(start, end);

        Assert.False(result.IsValid);
        Assert.Contains(result.Errors, e => e.Contains("not exceed 730 days"));
    }
}
