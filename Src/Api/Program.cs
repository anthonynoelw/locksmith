namespace Api;

using Api.Extensions;
using Infrastructure.Extensions;

using Serilog;

/// <summary>
/// Entry point for the API application.
/// </summary>
public partial class Program
{
    /// <summary>
    /// Main entry point for the application.
    /// </summary>
    /// <param name="args">Command line arguments.</param>
    private static void Main(string[] args)
    {
        // Captures fatal errors during host construction before appsettings.json is loaded.
        Log.Logger = new LoggerConfiguration()
            .MinimumLevel.Debug()
            .WriteTo.Console()
            .CreateBootstrapLogger();

        try
        {
            WebApplicationBuilder builder = WebApplication.CreateBuilder(args);

            builder.AddSerilogLogging();
            builder.AddInfrastructure();
            builder.AddApiServices();

            WebApplication app = builder.Build();

            app.UseApiPipeline();

            app.Run();
        }
        catch (Exception ex)
        {
            Log.Fatal(ex, "Application terminated unexpectedly");
            throw;
        }
        finally
        {
            Log.CloseAndFlush();
        }
    }
}
