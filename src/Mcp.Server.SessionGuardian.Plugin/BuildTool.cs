using Microsoft.Extensions.Logging;
using ModelContextProtocol.Server;
using System.ComponentModel;

namespace Mcp.Server.SessionGuardian.Plugin;

[McpServerToolType]
internal class BuildTool
{
    private readonly ILogger<BuildTool> logger;
    private readonly ServerConnectionManager pipeClient;

    public BuildTool(ILogger<BuildTool> logger, ServerConnectionManager pipeClient)
    {
        this.logger = logger;
        this.pipeClient = pipeClient;
    }

    [McpServerTool(Name = "expensive_init", Title = "Initialize the build environment shell session.")]
    public async Task<string> InitDevEnv(
        [Description("The absolute path to the global NuGet cache directory.")]
        string globalNugetCacheDir,
        [Description("The working directory path.")]
        string? workingDirPath,
        [Description("The absolute path to the build initialization script (e.g., init.cmd).")]
        string? initShellPath)
    {
        // Generate the command chain string instead of a file
        string initCommandTempScript= pipeClient.GenerateInitCommand(globalNugetCacheDir, workingDirPath, initShellPath);

        string initCommand = $"call {initCommandTempScript}";

        this.logger.LogInformation("[Tool] Executing init sequence: {Cmd}", initCommand);

        // Send the single chained command line
        string result = await pipeClient.SendCommandAsync(initCommand, default);

        return result;
    }


    [McpServerTool(Name = "send_command", Title = "Send a command to the persistent shell session.")]
    public async Task<string> SendCommand(
        [Description("The shell command.")] string cmdShellCommand)
    {
        if (string.IsNullOrWhiteSpace(cmdShellCommand))
        {
            throw new InvalidOperationException("Command cannot be empty");
        }

        string finalCommand = $"call {cmdShellCommand}";

        this.logger.LogInformation("[Tool] Sending command: {Cmd}", cmdShellCommand);
        return await pipeClient.SendCommandAsync(finalCommand, default);
    }
}