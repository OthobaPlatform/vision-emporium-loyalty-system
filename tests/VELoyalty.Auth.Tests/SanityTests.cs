using Xunit;

namespace VELoyalty.Auth.Tests;

public class SanityTests
{
    [Fact]
    public void AuthProject_ShouldBeAccessible()
    {
        // Verify the Auth project is properly referenced
        var markerType = typeof(JwtTokenService);
        Assert.NotNull(markerType);
    }
}
