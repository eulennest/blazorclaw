using BlazorClaw.Core.Commands;
using BlazorClaw.Core.Tools;
using System.ComponentModel;

namespace BlazorClaw.Server.Tools.Mcp;

public class McpListToolParams : BaseToolParams
{
    [Description("Nur aktivierte Server anzeigen (optional, default: true)")]
    public bool OnlyActive { get; set; } = false;
}

public class McpListTool(McpToolRegistry mcpToolRegistry) : BaseTool<McpListToolParams>
{
    public override string Name => "mcp_list";
    public override string Description => """
        Zeigt alle registrierten MCP-Server in der lokalen Registry.
                
        Ausgabe:
        - name: Server-Name (eindeutig)
        - authType: Authentifizierung (none, bearer, basic)
        - description: Beschreibung
        - enabled: Aktiviert/Deaktiviert
        - addedAt: Hinzugefügt am
        
        Parameter:
        - OnlyActive: Nur aktive anzeigen (default: false)
        """;

    protected override async Task<string> ExecuteInternalAsync(McpListToolParams p, MessageContext context)
    {
        if ((mcpToolRegistry.ToolsReg?.Count ?? 0) <= 0)
            return "Keine MCP-Server konfiguriert.";

        var result = new System.Text.StringBuilder();
        result.AppendLine($"=== MCP Server Registry ===\n");

        foreach (var keyval in mcpToolRegistry.ToolsReg!)
        {
            var server = keyval.Value;
            if (p.OnlyActive && !server.Active) continue;

            result.AppendLine($"📌 {server.Entry.Name}");
            result.AppendLine($"   Auth: {server.Entry.AuthType}");
            if (!string.IsNullOrWhiteSpace(server.Entry.Description))
                result.AppendLine($"   Info: {server.Entry.Description}");
            result.AppendLine($"   Status: {(server.Active ? "✅ Enabled" : "❌ Disabled")}");
            result.AppendLine($"   Added: {server.Entry.AddedAt:G}");
            result.AppendLine();
        }

        return result.ToString();
    }
}
