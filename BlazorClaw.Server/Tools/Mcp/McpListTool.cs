using BlazorClaw.Core.Commands;
using BlazorClaw.Core.Tools;
using BlazorClaw.Core.VFS;
using System.ComponentModel;

namespace BlazorClaw.Server.Tools.Mcp;

public class McpListToolParams : BaseToolParams
{
    [Description("Nur aktivierte Server anzeigen (optional, default: true)")]
    public bool OnlyEnabled { get; set; } = true;
}

public class McpListTool(IVfsSystem vfs, ILogger<McpListTool> logger)
    : BaseTool<McpListToolParams>
{
    public override string Name => "mcp_list";
    public override string Description => """
        Zeigt alle registrierten MCP-Server in der lokalen Registry.
                
        Ausgabe:
        - name: Server-Name (eindeutig)
        - serverUri: Verbindungs-URI (http://, ws://, npx://, etc.)
        - authType: Authentifizierung (none, bearer, basic)
        - description: Beschreibung
        - enabled: Aktiviert/Deaktiviert
        - addedAt: Hinzugefügt am
        
        Parameter:
        - onlyEnabled: Nur aktivierte anzeigen (default: true)
        """;

    protected override async Task<string> ExecuteInternalAsync(McpListToolParams p, MessageContext context)
    {
        var registry = await McpRegistry.LoadRegistryAsync(vfs);

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
            if (!string.IsNullOrWhiteSpace(server.Description))
                result.AppendLine($"   Info: {server.Description}");
            result.AppendLine($"   Status: {(server.Enabled ? "✅ Enabled" : "❌ Disabled")}");
            result.AppendLine($"   Added: {server.AddedAt:G}");
            result.AppendLine();
        }

        return result.ToString();

    }
}
