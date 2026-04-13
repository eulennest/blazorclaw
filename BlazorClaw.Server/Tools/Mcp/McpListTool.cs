using BlazorClaw.Core.Commands;
using BlazorClaw.Core.Tools;
using System.ComponentModel;

namespace BlazorClaw.Server.Tools.Mcp;


public class McpListTool(McpToolRegistry mcpToolRegistry) : BaseTool<EmptyParams>
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

    protected override async Task<string> ExecuteInternalAsync(EmptyParams p, MessageContext context)
    {
        if ((mcpToolRegistry.ToolsReg?.Count ?? 0) <= 0)
            return "Keine MCP-Server konfiguriert.";

        var result = new System.Text.StringBuilder();
        result.AppendLine($"=== MCP Server Registry ===\n");

        foreach (var keyval in mcpToolRegistry.ToolsReg!)
        {
            var server = keyval.Value;

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
