using Mcp.Server.SessionGuardian.Shared;
using Microsoft.Extensions.Logging;
using System.Diagnostics;
using System.IO.Pipes;
using System.Text;

namespace Mcp.Server.SessionGuardian.Plugin;

internal class ServerConnectionManager : IDisposable
{
    private NamedPipeClientStream? clientStream;
    private readonly ILogger<ServerConnectionManager> logger;
    private readonly string PipeName = BuildPipeConstant.BuildPipeName;
    private readonly string ServerExecutableName = BuildPipeConstant.BuildServerAssemblyName;
    private readonly int ConnectionTimeout = BuildPipeConstant.ConnectionTimeout;
    private readonly SemaphoreSlim slim = new(1, 1);
    private StreamReader? reader;
    private StreamWriter? writer;

    public ServerConnectionManager(ILogger<ServerConnectionManager> logger)
    {
        this.logger = logger;
    }

    public async Task<string> SendCommandAsync(string command, CancellationToken cancel = default)
    {
        await this.slim.WaitAsync(cancel);

        string commandId = Guid.NewGuid().ToString("n");
        StringBuilder responseLineBuilder = new();
        try
        {
            await this.EnsurePipeConnection(cancel);
            if (this.writer is null || this.reader is null)
            {
                throw new InvalidOperationException("[PipeClient] Pipe connection is not established.");
            }

            string wrappedCommand = $"ECHO __START_COMMAND__ & {command} & ECHO {BuildPipeConstant.CommandEndMark}COMMAND:ID_{commandId}__ERRORCODE:%ERRORLEVEL%__";

            this.logger.LogInformation("[PipeClient] Sending command chain: [{Command}]", wrappedCommand);

            await this.writer.WriteLineAsync(wrappedCommand.AsMemory(), cancel);
            await this.writer.FlushAsync(cancel);

            string? line;
            using var timeoutCts = new CancellationTokenSource(TimeSpan.FromMinutes(5));
            using var linkedCts = CancellationTokenSource.CreateLinkedTokenSource(cancel, timeoutCts.Token);

            while ((line = await this.reader.ReadLineAsync(linkedCts.Token)) != null)
            {
                if (string.IsNullOrEmpty(line)) continue;

                this.logger.LogInformation("[PipeServer] -> [PipeClient] Message: [{Msg}]", line);

                if (this.CheckCommandHasFinished(line, commandId, responseLineBuilder))
                {
                    this.logger.LogInformation("[PipeClient] Command finished.");
                    break;
                }
                responseLineBuilder.AppendLine(line);
            }
        }
        catch (Exception ex)
        {
            this.logger.LogWarning(ex, "[PipeClient] Error communicating with server.");
            this.Dispose();
        }
        finally
        {
            this.slim.Release();
        }

        return responseLineBuilder.ToString();
    }

    private bool CheckCommandHasFinished(string line, string commandId, StringBuilder responseLineBuilder)
    {
        if (line.Trim().Contains(commandId))
        {
            if (line.TrimStart().StartsWith($"[OUT]-{BuildPipeConstant.CommandEndMark}", StringComparison.OrdinalIgnoreCase) ||
                line.TrimStart().StartsWith($"[ERR]-{BuildPipeConstant.CommandEndMark}", StringComparison.OrdinalIgnoreCase))
            {
                responseLineBuilder.AppendLine(line);
                return true;
            }
        }
        return false;
    }

    /// <summary>
    /// Generates the init command string.
    /// </summary>
    public string GenerateInitCommand(string globalNugetCacheDir, string? workingDir, string? buildInitShellPath)
    {
        var wrapperCommandBuilder = new StringBuilder();

        // FIX 1: Add trailing semicolon to prevent path concatenation issues (Git\cmdC:\...)
        string sysPath = @"C:\Windows\system32;C:\Windows;C:\Windows\System32\Wbem;C:\Windows\System32\WindowsPowerShell\v1.0;C:\Program Files\Git\cmd;";

        string nugetCache = string.IsNullOrWhiteSpace(globalNugetCacheDir) ? @"C:\CxCache" : globalNugetCacheDir;
        wrapperCommandBuilder.AppendLine($"set \"PATH={sysPath}\"");

        // Set Working Directory
        if (!string.IsNullOrWhiteSpace(workingDir))
        {
            wrapperCommandBuilder.AppendLine($"cd /d \"{workingDir}\" ");
        }

        // Call Init Script
        if (!string.IsNullOrWhiteSpace(buildInitShellPath))
        {
            wrapperCommandBuilder.AppendLine($"echo [Client] Calling Init Script: {buildInitShellPath} ");

            // use '&' to execute the following commands regardless of whether the init dev env shell command succeeds or fails.
            wrapperCommandBuilder.AppendLine($"call \"{buildInitShellPath}\" ");
        }

        wrapperCommandBuilder.AppendLine($"echo [Client] Check PATH after loading {buildInitShellPath}.");

        wrapperCommandBuilder.AppendLine("set path");
        wrapperCommandBuilder.AppendLine("echo [Client] Override Path Check:");
        wrapperCommandBuilder.AppendLine("set path");

        string initCommandContent = wrapperCommandBuilder.ToString();

        string tempDir = AppContext.BaseDirectory;

        string guid = Guid.NewGuid().ToString("n");

        string tempCmdFileName = $"mcp_init_env_{guid}.cmd";

        string initCommandTempFile = Path.Combine(tempDir, tempCmdFileName);

        File.WriteAllTextAsync(initCommandTempFile, initCommandContent);

        return initCommandTempFile;
    }

    private void StartPipeServer()
    {
        string currentWorkDir = AppContext.BaseDirectory;
        string pipeServerExcutablePath = Path.Combine(currentWorkDir, this.ServerExecutableName);

        if (!File.Exists(pipeServerExcutablePath))
        {
            // Fallback logic omitted for brevity
            throw new FileNotFoundException("Pipe server executable not found", pipeServerExcutablePath);
        }

        ProcessStartInfo startInfo = new ProcessStartInfo()
        {
            FileName = pipeServerExcutablePath,
            UseShellExecute = true,
            CreateNoWindow = false
        };
        Process.Start(startInfo);
    }

    private async Task EnsurePipeConnection(CancellationToken cancel)
    {
        if (this.clientStream is not null && this.clientStream.IsConnected)
        {
            this.logger.LogInformation("[PipeClient] Pipe client has connected, nothing to do.");
            return;
        }

        if (this.clientStream is not null && !this.clientStream.IsConnected)
        {
            this.logger.LogInformation("[PipeClient] Pipe client is active but pipe client stream is not connected. Cleanup pipe client and reconnect to pipe server..");
            this.Dispose();
        }

        this.clientStream = new NamedPipeClientStream(".", this.PipeName, PipeDirection.InOut, PipeOptions.Asynchronous);
        try
        {
            await this.clientStream.ConnectAsync(TimeSpan.FromSeconds(this.ConnectionTimeout), cancel);
        }
        catch (TimeoutException)
        {
            this.logger.LogWarning("[PipeClient] Pipe server seems like not active yet, try start pipe server and connect to it...");
            this.StartPipeServer();
            await Task.Delay((int)TimeSpan.FromSeconds(this.ConnectionTimeout).TotalMilliseconds, cancel);
            await this.clientStream.ConnectAsync(TimeSpan.FromSeconds(this.ConnectionTimeout), cancel);
        }

        this.reader = new StreamReader(this.clientStream);
        this.writer = new StreamWriter(this.clientStream) { AutoFlush = true };
        await this.reader.ReadLineAsync(cancel);
    }

    public void Dispose()
    {
        this.writer?.Dispose();
        this.reader?.Dispose();
        this.clientStream?.Dispose();
    }
}