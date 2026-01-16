@ECHO OFF

call dotnet restore
call dotnet publish ./src/Mcp.Server.SessionGuardian.Plugin/Mcp.Server.SessionGuardian.Plugin.csproj -c Release -r win-x64 --self-contained -p:PublishSingleFile=true -o ./builds
call dotnet publish ./src/Mcp.Server.SessionGuardian.Host/Mcp.Server.SessionGuardian.Host.csproj -c Release -r win-x64 --self-contained -p:PublishSingleFile=true -o ./builds