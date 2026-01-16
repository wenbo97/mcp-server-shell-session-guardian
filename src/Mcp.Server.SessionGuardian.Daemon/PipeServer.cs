using Mcp.Server.SessionGuardian.Shared;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using System.Diagnostics;
using System.IO.Pipes;
using System.Text;

namespace Mcp.Server.SessionGuardian.Daemon;

internal class PipeServer : BackgroundService
{
    private static readonly TimeSpan IdleTimeout = TimeSpan.FromMinutes(30);
    private readonly ILogger<PipeServer> logger;
    private readonly IHostApplicationLifetime hostLifetime;
    private readonly SemaphoreSlim serverPipeWriteLock = new(1, 1);

    private StreamWriter? writer;
    private Process? cmdSession;
    private NamedPipeServerStream? pipeServer;
    private Timer? idleTimer;

    public PipeServer(ILogger<PipeServer> logger, IHostApplicationLifetime hostLifetime)
    {
        this.hostLifetime = hostLifetime;
        this.logger = logger;
    }

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        // Ignore for now
        // this.StartIdleTimer();
        this.StartPersistentCmdSession();
        try
        {
            while (!stoppingToken.IsCancellationRequested)
            {
                try
                {
                    await this.StartServerAsync(stoppingToken);
                }
                catch (Exception ex)
                {
                    this.logger.LogError(ex, "[PipeServer] Pipe session error.");
                }
                await Task.Delay(1000, stoppingToken);
            }
        }
        finally
        {
            this.hostLifetime.StopApplication();
        }
    }

    private async Task StartServerAsync(CancellationToken stoppingToken)
    {
        this.logger.LogInformation("[PipeServer] Starting Named Pipe Server...");

        await using var serverStream = new NamedPipeServerStream(
            BuildPipeConstant.BuildPipeName,
            PipeDirection.InOut,
            1,
            PipeTransmissionMode.Byte,
            PipeOptions.Asynchronous);

        this.pipeServer = serverStream;

        this.logger.LogInformation("[PipeServer] Waiting for connection...");
        await serverStream.WaitForConnectionAsync(stoppingToken);
        this.logger.LogInformation("[PipeServer] Client Connected.");

        // Ignore for now
        // this.ResetIdleTimer();

        this.writer = new StreamWriter(serverStream, new UTF8Encoding(false), 1024, leaveOpen: true)
        {
            AutoFlush = true
        };

        await this.WriteToPipeAsync("[PipeServer] Pipe server Ready.", stoppingToken);

        using var reader = new StreamReader(serverStream, Encoding.UTF8, leaveOpen: true);

        while (serverStream.IsConnected && !stoppingToken.IsCancellationRequested)
        {
            string? line = await reader.ReadLineAsync(stoppingToken);
            if (line == null) 
            {
                break;
            };

            if (!string.IsNullOrEmpty(line))
            {
                // reset pipe server host timer.
                // Ignore for now.
                // this.ResetIdleTimer();
                this.logger.LogInformation("[PipeServer] CMD Received: {Cmd}", line);

                if (this.cmdSession is not null && !this.cmdSession.HasExited)
                {
                    await this.cmdSession.StandardInput.WriteLineAsync(line);
                    // ensure flush to standard input stream.
                    await this.cmdSession.StandardInput.FlushAsync();
                }
                else
                {
                    this.logger.LogWarning("[PipeServer] CMD Session is not active. Restarting...");
                    this.StartPersistentCmdSession();
                }
            }
        }
    }

    private void StartPersistentCmdSession()
    {
        if (this.cmdSession is not null && !this.cmdSession.HasExited) return;

        this.logger.LogInformation("[PipeServer] Spawning persistent cmd.exe session...");

        var psi = new ProcessStartInfo
        {
            FileName = "cmd.exe",
            // /Q turns off echo. 
            Arguments = "/Q /K title Pipe_Server_Host",
            UseShellExecute = false, // must set to false if we redirect input, output, error to custom stream.
            RedirectStandardInput = true,
            RedirectStandardOutput = true,
            RedirectStandardError = true,
            CreateNoWindow = false // TODO: we should configure whether a shell window should show or not.
        };

        // CRITICAL FIX: Include PowerShell and Git in the base PATH.
        psi.EnvironmentVariables["PATH"] = @"C:\Windows\system32;C:\Windows;C:\Windows\System32\Wbem;C:\Windows\System32\WindowsPowerShell\v1.0;C:\Program Files\Git\cmd;";

        this.logger.LogDebug("[PipeServer] Initial PATH: {Vars}", psi.EnvironmentVariables["PATH"]);

        this.cmdSession = new Process { StartInfo = psi };
        this.cmdSession.EnableRaisingEvents = true;

        this.cmdSession.Exited += (s, e) =>
        {
            this.logger.LogWarning("[PipeServer] Shell session exited unexpectedly. Restarting...");
            this.StartPersistentCmdSession();
        };

        this.cmdSession.OutputDataReceived += (s, e) => this.ReceiveOutputFromCmdSession("[OUT]", e.Data);
        this.cmdSession.ErrorDataReceived += (s, e) => this.ReceiveOutputFromCmdSession("[ERR]", e.Data);

        this.cmdSession.Start();
        this.cmdSession.BeginOutputReadLine();
        this.cmdSession.BeginErrorReadLine();
    }

    private async void ReceiveOutputFromCmdSession(string type, string? data)
    {
        if (string.IsNullOrEmpty(data))
        {
            return;
        }

        this.logger.LogDebug($"[CMD Session-> Pipe Server]{type}-{data}");

        try
        {
            await this.WriteToPipeAsync($"{type}-{data}", default);
        }
        catch
        {
            // Ignore write errors to broken pipe
        }
    }

    private async Task WriteToPipeAsync(string message, CancellationToken cancel)
    {
        await this.serverPipeWriteLock.WaitAsync(cancel);
        try
        {
            if (this.writer != null && this.writer.BaseStream.CanWrite)
            {
                await this.writer.WriteLineAsync(message);
                await this.writer.FlushAsync(); // Immediate flushing is important.
            }
        }
        catch (Exception ex)
        {
            this.logger.LogWarning("[PipeServer] Write failed: {Ex}", ex.Message);
        }
        finally
        {
            this.serverPipeWriteLock.Release();
        }
    }

    private void ResetIdleTimer() => this.idleTimer?.Change(IdleTimeout, Timeout.InfiniteTimeSpan);
    private void StartIdleTimer() => this.idleTimer = new Timer(_ => this.hostLifetime.StopApplication(), null, IdleTimeout, Timeout.InfiniteTimeSpan);

    public override void Dispose()
    {
        this.cmdSession?.Kill();
        this.cmdSession?.Dispose();
        this.pipeServer?.Dispose();
        this.serverPipeWriteLock.Dispose();
        base.Dispose();
    }
}