using BlazorClaw.Core.Commands;
using BlazorClaw.Core.Sessions;
using BlazorClaw.Core.Tools;
using ModelContextProtocol.Client;
using System.ComponentModel;
using System.ComponentModel.DataAnnotations;

namespace BlazorClaw.Server.Tools.Mcp;

public class McpToggleToolParams : BaseToolParams
{
    [Description("Server-Name (eindeutig, z.B. 'github', 'bitcoin', für Liste nutze mcp_list)")]
    [Required]
    public string Name { get; set; } = string.Empty;

    [Description("Aktivieren / Deaktivieren (default: true)")]
    public bool Active { get; set; } = true;

    [Description("Setzt den Authorization Header, Bearer Token, Basic auth (optional)")]
    public string? Auth { get; set; }
}

public class McpToggleTool(McpToolRegistry mcpToolRegistry)
    : BaseTool<McpToggleToolParams>
{
    public override string Name => "mcp_toggle";
    public override string Description => "Aktiviert und Deaktivert Tools der MCP-Server. Tool-Liste wird live aktualisiert. mcp_{Name}_*";

    protected override async Task<string> ExecuteInternalAsync(McpToggleToolParams p, MessageContext context)
    {
        await p.ResolveVarsAsync(context);

        var reg = mcpToolRegistry.ToolsReg?.FirstOrDefault(o => o.Key.Equals(p.Name, StringComparison.InvariantCultureIgnoreCase));
        if (reg?.Value == null) throw new KeyNotFoundException($"Mcp-Server '{p.Name}' nicht gefunden.");
        var regi = reg.Value.Value;
        regi.Active = p.Active;
        if (p.Active && !string.IsNullOrWhiteSpace(p.Auth))
        {
            var transport = new HttpClientTransport(new()
            {
                Endpoint = new Uri(regi.Entry.ServerUri),
                AdditionalHeaders = new Dictionary<string, string>()
                {
                    ["Authorization"] = p.Auth
                }
            });
            if (regi.McpClient != null) await regi.McpClient.DisposeAsync();
            regi.McpClient = await McpClient.CreateAsync(transport);
            regi.ClientTools = await regi.McpClient.ListToolsAsync();
        }

        var sess = context.Provider.GetRequiredService<SessionStateAccessor>().SessionState;
        if (sess != null) sess.Tools = null;
        return $"MCP Server '{regi.Entry.Name}' wurde {(p.Active ? "aktiviert" : "deaktiviert")}.\nWeitere Tools (mcp_{regi.Entry.Name}_*) sind nun verfügbar.";
    }
}
