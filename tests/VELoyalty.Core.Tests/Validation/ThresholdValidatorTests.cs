using Xunit;
using VELoyalty.Core.Validation;

namespace VELoyalty.Core.Tests.Validation;

public class ThresholdValidatorTests
{
    [Fact]
    public void Validate_SingleValidThreshold_ReturnsSuccess()
    {
        var thresholds = new List<int> { 3 };

        var result = ThresholdValidator.Validate(thresholds);

        Assert.True(result.IsValid);
    }

    [Fact]
    public void Validate_MultipleValidThresholds_ReturnsSuccess()
    {
        var thresholds = new List<int> { 3, 6, 9 };

        var result = ThresholdValidator.Validate(thresholds);

        Assert.True(result.IsValid);
    }

    [Fact]
    public void Validate_MaxThresholds_10Unique_ReturnsSuccess()
    {
        var thresholds = Enumerable.Range(1, 10).ToList();

        var result = ThresholdValidator.Validate(thresholds);

        Assert.True(result.IsValid);
    }

    [Fact]
    public void Validate_EmptyList_ReturnsFailure()
    {
        var thresholds = new List<int>();

        var result = ThresholdValidator.Validate(thresholds);

        Assert.False(result.IsValid);
        Assert.Contains(result.Errors, e => e.Contains("At least 1"));
    }

    [Fact]
    public void Validate_TooManyThresholds_11_ReturnsFailure()
    {
        var thresholds = Enumerable.Range(1, 11).ToList();

        var result = ThresholdValidator.Validate(thresholds);

        Assert.False(result.IsValid);
        Assert.Contains(result.Errors, e => e.Contains("No more than 10"));
    }

    [Theory]
    [InlineData(0)]
    [InlineData(-1)]
    [InlineData(101)]
    [InlineData(200)]
    public void Validate_ValueOutOfRange_ReturnsFailure(int value)
    {
        var thresholds = new List<int> { value };

        var result = ThresholdValidator.Validate(thresholds);

        Assert.False(result.IsValid);
        Assert.Contains(result.Errors, e => e.Contains("between 1 and 100"));
    }

    [Fact]
    public void Validate_BoundaryValues_1And100_ReturnsSuccess()
    {
        var thresholds = new List<int> { 1, 100 };

        var result = ThresholdValidator.Validate(thresholds);

        Assert.True(result.IsValid);
    }

    [Fact]
    public void Validate_DuplicateValues_ReturnsFailure()
    {
        var thresholds = new List<int> { 3, 6, 3 };

        var result = ThresholdValidator.Validate(thresholds);

        Assert.False(result.IsValid);
        Assert.Contains(result.Errors, e => e.Contains("Duplicate") && e.Contains("3"));
    }

    [Fact]
    public void Validate_MultipleDuplicates_ReportsAll()
    {
        var thresholds = new List<int> { 3, 6, 3, 6 };

        var result = ThresholdValidator.Validate(thresholds);

        Assert.False(result.IsValid);
        Assert.Contains(result.Errors, e => e.Contains("3"));
        Assert.Contains(result.Errors, e => e.Contains("6"));
    }

    [Fact]
    public void Validate_NullList_ReturnsFailure()
    {
        var result = ThresholdValidator.Validate(null!);

        Assert.False(result.IsValid);
    }
}
