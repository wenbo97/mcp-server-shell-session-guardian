namespace Mcp.Server.SessionGuardian.Shared;

public class CommandExecResponse
{
    public string CommandExecutionLog { get; set; } = string.Empty;

    public int ExitCode { get; set; }
}
