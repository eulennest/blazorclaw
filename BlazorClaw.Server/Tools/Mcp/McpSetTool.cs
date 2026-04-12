using BlazorClaw.Core.Commands;
using BlazorClaw.Core.Tools;
using System.ComponentModel;
using System.ComponentModel.DataAnnotations;

namespace BlazorClaw.Server.Tools.Mcp;

public class McpSetToolParams : BaseToolParams
{
    [Description("Server-Name (eindeutig, z.B. 'github', 'memory', 'bitcoin')")]
    [Required]
    public string Name { get; set; } = string.Empty;

    [Description("Aktivieren / Deaktivieren (default: true)")]
    public bool Active { get; set; } = true;
}

public class McpSetTool(McpToolRegistry mcpToolRegistry, ILogger<McpSetTool> logger)
    : BaseTool<McpSetToolParams>
{
    public override string Name => "mcp_set";
    public override string Description => "Aktiviert und Deaktivert Tools der MCP-Server. Tool-Liste wird live aktualisiert. mcp-{Name}-*";

    protected override async Task<string> ExecuteInternalAsync(McpSetToolParams p, MessageContext context)
    {
        var reg = (mcpToolRegistry.ToolsReg?.FirstOrDefault(o => o.Key.Equals(p.Name, StringComparison.InvariantCultureIgnoreCase))) ?? throw new KeyNotFoundException($"Mcp-Server '{p.Name}' nicht gefunden.");
        reg.Value.Active = p.Active;
        await mcpToolRegistry.ReloadAsync();
        return $"MCP Server '{p.Name}' wurde {(p.Active ? "aktiviert" : "deaktiviert")}.";
    }
}
