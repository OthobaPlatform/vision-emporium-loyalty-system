using Xunit;

namespace VELoyalty.Api.Tests;

public class SanityTests
{
    [Fact]
    public void ApiProject_ShouldBeAccessible()
    {
        // Verify the Core project is properly referenced from API tests
        var markerType = typeof(VELoyalty.Core.CoreAssemblyMarker);
        Assert.NotNull(markerType);
    }
}
