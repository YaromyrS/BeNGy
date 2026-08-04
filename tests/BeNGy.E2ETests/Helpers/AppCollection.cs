namespace BeNGy.E2ETests.Helpers;

/// <summary>
/// xUnit collection definition that ensures all E2E test classes
/// share a single AppTestFixture instance and run sequentially.
/// This prevents conflicts from BeNGy's single-instance enforcement.
/// </summary>
[CollectionDefinition("BeNGy App")]
public class AppCollection : ICollectionFixture<AppTestFixture>
{
}
