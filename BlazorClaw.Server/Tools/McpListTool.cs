using BlazorClaw.Core.Commands;
using BlazorClaw.Core.Tools;
using BlazorClaw.Core.Utils;
using BlazorClaw.Core.VFS;
using System.ComponentModel;
using System.Text.Json;
using System.Text.Json.Serialization;

namespace BlazorClaw.Server.Tools;

public class McpListToolParams : BaseToolParams
{
    [Description("Nur aktivierte Server anzeigen (optional, default: true)")]
    public bool OnlyEnabled { get; set; } = true;
}

public class McpListTool(IVfsSystem vfs, MessageContextAccessor mca, ILogger<McpListTool> logger) 
    : BaseTool<McpListToolParams>
{
    public override string Name => "mcp_list";
    public override string Description => """
        Zeigt alle registrierten MCP-Server in der lokalen Registry.
        
        Gespeichert in: /~secure/mcp.json
        
        Ausgabe:
        - name: Server-Name (eindeutig)
        - serverUri: Verbindungs-URI (http://, ws://, npx://, etc.)
        - authType: Authentifizierung (none, bearer, basic)
        - tokenName: Token-Name aus Vault (falls bearer)
        - description: Beschreibung
        - enabled: Aktiviert/Deaktiviert
        - addedAt: Hinzugefügt am
        
        Parameter:
        - onlyEnabled: Nur aktivierte anzeigen (default: true)
        """;

    protected override async Task<string> ExecuteInternalAsync(McpListToolParams p, MessageContext context)
    {
        try
        {
            var registry = await LoadRegistryAsync();

            var servers = registry.Servers
                .Where(s => !p.OnlyEnabled || s.Enabled)
                .OrderBy(s => s.Name)
                .ToList();

            if (!servers.Any())
                return "Keine MCP-Server konfiguriert.";

            var result = new System.Text.StringBuilder();
            result.AppendLine($"=== MCP Server Registry ({servers.Count}) ===\n");

            foreach (var server in servers)
            {
                result.AppendLine($"📌 {server.Name}");
                result.AppendLine($"   URI: {server.ServerUri}");
                result.AppendLine($"   Auth: {server.AuthType}");
                if (!string.IsNullOrWhiteSpace(server.TokenName))
                    result.AppendLine($"   Token: {server.TokenName}");
                if (!string.IsNullOrWhiteSpace(server.Description))
                    result.AppendLine($"   Info: {server.Description}");
                result.AppendLine($"   Status: {(server.Enabled ? "✅ Enabled" : "❌ Disabled")}");
                result.AppendLine($"   Added: {server.AddedAt:G}");
                result.AppendLine();
            }

            return result.ToString();
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Error listing MCP servers");
            return $"ERROR: {ex.Message}";
        }
    }

    private async Task<McpRegistry> LoadRegistryAsync()
    {
        try
        {
            var filePath = GetRegistryPath();
            if (!await vfs.ExistsAsync(filePath))
                return new McpRegistry();

            using var stream = await vfs.OpenFileAsync(filePath, FileMode.Open, FileAccess.Read);
            return await JsonSerializer.DeserializeAsync<McpRegistry>(stream) 
                ?? new McpRegistry();
        }
        catch (Exception ex)
        {
            logger.LogWarning(ex, "Could not load MCP registry, returning empty");
            return new McpRegistry();
        }
    }

    private VfsPath GetRegistryPath()
    {
        return VfsPath.Parse(VfsPath.Parse("/~secure/"), "mcp.json", VfsPathParseMode.File);
    }
}
