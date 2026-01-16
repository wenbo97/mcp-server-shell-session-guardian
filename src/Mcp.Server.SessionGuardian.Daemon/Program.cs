using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Serilog;
using Serilog.Events;

namespace Mcp.Server.SessionGuardian.Daemon;

public class Program
{
    public static async Task Main(string[] args)
    {
        int pid = Environment.ProcessId;

        Log.Logger = new LoggerConfiguration().
            MinimumLevel.Debug()
            .WriteTo.File("mcp_server_logs/session_guardian_host.log", rollingInterval: RollingInterval.Day)
            .WriteTo.Console(standardErrorFromLevel: LogEventLevel.Verbose)
            .CreateLogger();

        var builder = Host.CreateApplicationBuilder(args);
        builder.Services.AddSerilog();
        builder.Services.AddHostedService<PipeServer>();
        builder.Logging.AddConsole(consoleLogOptions =>
        {
            consoleLogOptions.LogToStandardErrorThreshold = LogLevel.Trace;
        });

        Log.Logger.Information("[PipeServer] Start pipe server at PID: {PID}", pid);

        await builder.Build().RunAsync();
    }
}
