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
    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        logger.LogInformation("Worker starting at: {time}", DateTimeOffset.Now);
        return;
        while (!stoppingToken.IsCancellationRequested)
        {
            if (logger.IsEnabled(LogLevel.Information))
            {
                logger.LogInformation("Worker running at: {time}", DateTimeOffset.Now);
            }

            await Task.Delay(1000, stoppingToken);
        }
    }
}
