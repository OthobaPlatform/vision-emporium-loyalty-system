using Xunit;
using VELoyalty.Core.Validation;

namespace VELoyalty.Core.Tests.Validation;

public class PhoneNumberValidatorTests
{
    [Theory]
    [InlineData("+8801712345678")]
    [InlineData("+14155552671")]
    [InlineData("+442071234567")]
    public void Validate_ValidE164Number_ReturnsSuccess(string phone)
    {
        var result = PhoneNumberValidator.Validate(phone);

        Assert.True(result.IsValid);
        Assert.Empty(result.Errors);
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("  ")]
    public void Validate_NullOrEmpty_ReturnsFailure(string? phone)
    {
        var result = PhoneNumberValidator.Validate(phone);

        Assert.False(result.IsValid);
        Assert.Contains(result.Errors, e => e.Contains("required"));
    }

    [Theory]
    [InlineData("abc")]
    [InlineData("+0123456789")]
    [InlineData("++8801712345678")]
    [InlineData("+")]
    [InlineData("+1")]
    public void Validate_InvalidFormat_ReturnsFailure(string phone)
    {
        var result = PhoneNumberValidator.Validate(phone);

        Assert.False(result.IsValid);
        Assert.Contains(result.Errors, e => e.Contains("E.164"));
    }

    [Fact]
    public void Normalize_LocalNumberWithLeadingZero_PrependsCountryCode()
    {
        var normalized = PhoneNumberValidator.Normalize("01712345678");

        Assert.Equal("+8801712345678", normalized);
    }

    [Fact]
    public void Normalize_LocalNumberWithoutLeadingZero_PrependsCountryCode()
    {
        var normalized = PhoneNumberValidator.Normalize("1712345678");

        Assert.Equal("+8801712345678", normalized);
    }

    [Fact]
    public void Normalize_NumberWithCountryCode_ReturnsAsIs()
    {
        var normalized = PhoneNumberValidator.Normalize("+8801712345678");

        Assert.Equal("+8801712345678", normalized);
    }

    [Fact]
    public void Normalize_NumberWith00Prefix_ReplacesWithPlus()
    {
        var normalized = PhoneNumberValidator.Normalize("008801712345678");

        Assert.Equal("+8801712345678", normalized);
    }

    [Fact]
    public void ValidateAndNormalize_ValidLocalNumber_ReturnsNormalized()
    {
        var (result, normalized) = PhoneNumberValidator.ValidateAndNormalize("01712345678");

        Assert.True(result.IsValid);
        Assert.Equal("+8801712345678", normalized);
    }

    [Fact]
    public void ValidateAndNormalize_InvalidNumber_ReturnsNull()
    {
        var (result, normalized) = PhoneNumberValidator.ValidateAndNormalize("abc");

        Assert.False(result.IsValid);
        Assert.Null(normalized);
    }

    [Fact]
    public void ValidateAndNormalize_NullInput_ReturnsFailure()
    {
        var (result, normalized) = PhoneNumberValidator.ValidateAndNormalize(null);

        Assert.False(result.IsValid);
        Assert.Null(normalized);
    }
}
