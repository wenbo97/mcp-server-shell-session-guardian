namespace Mcp.Server.SessionGuardian.Shared;

public static class BuildPipeConstant
{
    /// <summary>
    /// Pipe name
    /// </summary>
    public static readonly string BuildPipeName = "BuildPipe";

    /// <summary>
    /// Server executable name.
    /// </summary>
    public static readonly string BuildServerAssemblyName = "Mcp.Server.SessionGuardian.Host.exe";

    /// <summary>
    /// Timeout by second
    /// </summary>
    public static int ConnectionTimeout = 5;

    /// <summary>
    /// Command end mark.
    /// </summary>
    public static readonly string CommandEndMark = "__END_COMMAND__";
}
