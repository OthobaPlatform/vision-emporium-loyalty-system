using Xunit;

namespace VELoyalty.Data.Tests;

public class SanityTests
{
    [Fact]
    public void DataProject_ShouldBeAccessible()
    {
        // Verify the Data project is properly referenced
        var contextType = typeof(DynamoDbContext);
        Assert.NotNull(contextType);
    }
}
