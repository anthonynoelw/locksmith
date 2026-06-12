namespace Agent;

using Agent.Extensions;
using Infrastructure.Extensions;
using Serilog;

/// <summary>
/// Entry point for the Agent application.
/// </summary>
public static class Program
{
    /// <summary>
    /// Main entry point for the Agent application.
    /// </summary>
    /// <param name="args">Command line arguments.</param>
    public static void Main(string[] args)
    {
        // Captures fatal errors during host construction before appsettings.json is loaded.
        Log.Logger = new LoggerConfiguration()
            .MinimumLevel.Debug()
            .WriteTo.Console()
            .CreateBootstrapLogger();

        try
        {
            HostApplicationBuilder builder = Host.CreateApplicationBuilder(args);

            builder.AddSerilogLogging();
            builder.AddAgentServices();
            builder.AddInfrastructure();

            IHost host = builder.Build();
            host.Run();
        }
        catch (Exception ex)
        {
            Log.Fatal(ex, "Agent terminated unexpectedly");
            throw;
        }
        finally
        {
            Log.CloseAndFlush();
        }
    }
}
