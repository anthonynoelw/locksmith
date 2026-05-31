namespace Application.Infrastructure;

/// <summary>
/// Registers the "Application" xUnit collection and binds it to the shared
/// <see cref="ApplicationFixture"/> so the fixture is created once and reused
/// across all test classes in the collection.
/// </summary>
[CollectionDefinition("Application")]
public sealed class ApplicationCollection : ICollectionFixture<ApplicationFixture>
{
}
