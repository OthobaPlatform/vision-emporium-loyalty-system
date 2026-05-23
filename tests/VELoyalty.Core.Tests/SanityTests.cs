using Xunit;

namespace VELoyalty.Core.Tests;

public class SanityTests
{
    [Fact]
    public void CoreProject_ShouldBeAccessible()
    {
        // Verify the Core project is properly referenced
        var markerType = typeof(CoreAssemblyMarker);
        Assert.NotNull(markerType);
    }
}
