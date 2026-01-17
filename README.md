### Note
- Still developing on local, will well-format in next commit.

# mcp-server-shell-session-guardian

*An MCP tool that keeps a shell session alive in memory, allowing AI agents to run **locally** commands in a persistent terminal environment.*

## Overview

Standard command execution tools for LLMs typically spawn a new process for every single command. This stateless approach causes issues when:
1.  **Loading Heavy Environments:** You rely on complex batch scripts (e.g., `init-env.cmd`) that take seconds to load.
2.  **Managing State:** You need to maintain `cd` (directory) changes or `set` (environment variable) modifications across multiple conversational turns.
3.  **Dependent Tasks:** You need to run a sequence of commands where subsequent commands depend on the context of the previous ones.

**MCP-Tool-Alive-Shell-Session-Host** solves this by launching a background host process that maintains the shell session via Named Pipes (or similar IPC), allowing the AI to interact with the *same* session repeatedly.

## 🚀 Features

- **Persistent Session:** Maintains a shell process alive; state is preserved between tool calls.
- **High Performance:** Eliminates the overhead of re-initializing the shell environment for every command.
- **MCP Compatible:** Designed to work seamlessly with Claude Desktop, Claude CLI, and other MCP clients.

## 🛠️ Build & Installation

### Prerequisites
- .NET SDK (Compatible with the solution verison)
- Claude CLI (or compatible MCP client)

### Build Steps

1.  **Clone the repository:**
    ```bash
    git clone https://github.com/wenbo97/mcp-server-shell-session-guardian
    cd mcp-server-shell-session-guardian
    ```

2.  **Build the Project:**
    Run the included publish script to generate the binaries.
    ```cmd
    publish.cmd
    ```

3.  **Verify Build Output:**
    Ensure the following executables are generated in the `src\dotnet\builds\` directory:
    - `Mcp.Server.SessionGuardian.Host.exe` (The MCP Interface)
    - `Mcp.Server.SessionGuardian.Plugin.exe` (The Background Host)

## ⚙️ Configuration

To use this tool, you must register it in your MCP configuration file (e.g., `.mcp.json` inside your project or the global config).

1.  Edit your `.mcp.json`.
2.  Set `mcpServers.dotnet_shell.command` to the **absolute path** of the built executable.

**Example `.mcp.json`:**

```json
{
  "mcpServers": {
    "dotnet_shell": {
      "command": "C:\\path\\to\\MCP-Tool-Alive-Shell-Session-Host\\builds\\Automation.Build.PluginV2.exe",
      "args": [],
      "env": {}
    }
  }
}
```

**Note**: Using the absolute path is critical to ensure the MCP client can locate the tool regardless of the current working directory.

## 🖥️ Usage

### 1. Launching with Claude CLI

**Step 1: Administrative Access**
If your workflow involves modifying system paths, writing to protected directories, or running build tools that require elevation, strictly open your terminal as **Administrator**.

**Step 2: Start the CLI**
Launch Claude with the `--plugin-dir` flag pointing to your plugin location:

```cmd
claude --plugin-dir C:\path\to\MCP-Tool-Alive-Shell-Session-Host
```

**Step 3: Verify Connection** Inside the chat, type /mcp to confirm the server is active:

```cmd
plugin:dotnet-plugin:dotnet_shell · √ connected
```

## ⚠️ Troubleshooting

### 1. PATH & Environment Inheritance
The persistent shell **inherits** the environment variables (including `%PATH%`) directly from the terminal window where you launched the `claude` CLI.
* **Symptom:** The tool reports "command not found" for standard tools like `git`, `cmake`, or `msbuild`.
* **Fix:** Verify that these commands are accessible in your **Administrator** terminal *before* you run the `claude` command.
    * *Note:* If you modify your system PATH, you must restart the terminal window and the Claude session for the changes to take effect in the persistent shell.


## Additional

- [Process visualization tool](https://learn.microsoft.com/en-us/sysinternals/downloads/process-explorer)
- [Claude Code Doc](https://code.claude.com/docs/en/sub-agents)
