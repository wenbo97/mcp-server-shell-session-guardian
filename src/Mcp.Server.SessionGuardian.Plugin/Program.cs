
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Serilog;
using Serilog.Events;

namespace Mcp.Server.SessionGuardian.Plugin;

/// <summary>
/// MCP tool Server & pipe client.
/// </summary>
public class Program
{
    /// <summary>
    /// Main entry
    /// </summary>
    /// <param name="args"></param>
    /// <returns></returns>
    public static async Task Main(string[] args)
    {
        await StartMcpServer(args);
    }

    /// <summary>
    /// Start mcp server 
    /// </summary>
    /// <param name="args"></param>
    /// <returns></returns>
    private static async Task StartMcpServer(string[] args)
    {
        int pid = Environment.ProcessId;

        Log.Logger = new LoggerConfiguration()
            .MinimumLevel.Debug()
            .WriteTo.File("mcp_server_logs/session_guardian_plugin.log", rollingInterval: RollingInterval.Day)
            .WriteTo.Console(standardErrorFromLevel: LogEventLevel.Verbose)
            .CreateLogger();

        var builder = Host.CreateApplicationBuilder(args);
        builder.Services.AddSerilog();
        builder.Logging.AddConsole(consoleLogOptions =>
        {
            consoleLogOptions.LogToStandardErrorThreshold = LogLevel.Trace;
        });
        builder.Services.AddSingleton<ServerConnectionManager>();
        builder.Services
            .AddMcpServer()
            .WithStdioServerTransport()
            .WithToolsFromAssembly();

        Log.Logger.Information("[PipeClient] Start pipe client mcp tool at PID: {PID}", pid);

        await builder.Build().RunAsync();
    }
}
