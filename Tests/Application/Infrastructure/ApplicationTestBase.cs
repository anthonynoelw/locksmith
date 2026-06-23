namespace Application.Infrastructure;

/// <summary>
/// Base class for application-level tests. Participates in the "Application" collection
/// so the shared <see cref="ApplicationFixture"/> is injected by xUnit.
/// </summary>
[Collection("Application")]
public abstract class ApplicationTestBase
{
    /// <summary>
    /// Initializes a new instance of the <see cref="ApplicationTestBase"/> class.
    /// </summary>
    /// <param name="fixture">The application fixture.</param>
    protected ApplicationTestBase(ApplicationFixture fixture)
    {
        Client = fixture.Client;
        Services = fixture.Services;
    }

    /// <summary>Gets the HTTP client targeting the in-process application.</summary>
    protected HttpClient Client { get; }

    /// <summary>Gets the root service provider of the running application.</summary>
    protected IServiceProvider Services { get; }
}
