namespace BeNGy.Tests;

/// <summary>
/// Basic project health tests - tests that verify the test infrastructure works
/// MAUI-dependent tests should be run separately as integration tests
/// </summary>
public class ProjectHealthTests
{
    [Fact]
    public void TestInfrastructure_Works()
    {
        // This test verifies the test framework is properly configured
        Assert.True(true);
    }

    [Fact]
    public void BasicMath_Works()
    {
        // Simple verification that tests can execute
        var result = 2 + 2;
        Assert.Equal(4, result);
    }
}
