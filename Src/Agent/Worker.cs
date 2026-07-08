namespace Agent;

/// <summary>
/// Background worker service for the Agent application.
/// </summary>
public class Worker(ILogger<Worker> logger) : BackgroundService
{
    /// <summary>
    /// Executes the background worker logic.
    /// </summary>
    /// <param name="stoppingToken">Cancellation token to stop the worker.</param>
    /// <returns>A task that represents the asynchronous operation.</returns>
    protected override Task ExecuteAsync(CancellationToken stoppingToken)
    {
        // Key-expiry polling is not implemented yet (see Docs/TODO.md); the worker currently
        // only logs startup and performs no periodic work.
        logger.LogInformation("Worker starting at: {time}", DateTimeOffset.Now);
        return Task.CompletedTask;
    }
}
