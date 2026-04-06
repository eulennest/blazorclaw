using BlazorClaw.Core.Commands;
using BlazorClaw.Core.Tools;
using BlazorClaw.Core.VFS;
using System.ComponentModel;
using System.ComponentModel.DataAnnotations;

namespace BlazorClaw.Server.Tools.Mcp;

public class McpSetToolParams : BaseToolParams
{
    [Description("Server-Name (eindeutig, z.B. 'github', 'memory', 'bitcoin')")]
    [Required]
    public string Name { get; set; } = string.Empty;

    [Description("Server URI (z.B. http://localhost:3000, ws://example.com, npx://@modelcontextprotocol/server-github)")]
    [Required]
    public string ServerUri { get; set; } = string.Empty;

    [Description("Auth-Type: 'none', 'bearer', 'basic' (default: 'none')")]
    public string? AuthType { get; set; } = "none";

    [Description("Beschreibung ")]
    [Required]
    public string Description { get; set; } = string.Empty;

    [Description("Aktiviert (default: true)")]
    public bool? Enabled { get; set; } = true;
}

public class McpSetTool(IVfsSystem vfs, ILogger<McpSetTool> logger)
    : BaseTool<McpSetToolParams>
{
    public override string Name => "mcp_set";
    public override string Description => """
        Fügt einen MCP-Server hinzu oder aktualisiert ihn in der Registry.
                
        Parameter:
        - name: Server-Name (eindeutig, case-sensitive)
        - serverUri: Verbindungs-URI
          * http://localhost:3000 (HTTP)
          * ws://example.com (WebSocket)
          * npx://@package/name (Local NPX Package)
          * file:///path/to/binary (Local Binary)
        - authType: Authentifizierung (default: 'none')
          * 'none' - Keine Auth
          * 'bearer' - Bearer Token (benötigt tokenName)
          * 'basic' - Basic Auth (benötigt tokenName)
        - tokenName: Vault-Item-Name für Token (optional, falls bearer/basic)
        - description: Kurzbeschreibung (optional)
        - enabled: Aktiviert (default: true)
        
        Beispiele:
        1. GitHub MCP über HTTP:
           name="github", serverUri="http://localhost:3000", authType="none"
        
        2. Memory MCP mit Bearer Token:
           name="memory", serverUri="http://blazorclaw-mcp:3000", 
           authType="bearer", tokenName="MCP_MEMORY_TOKEN"
        
        3. Lokales NPX Package:
           name="github-local", serverUri="npx://@modelcontextprotocol/server-github"
        """;

    protected override async Task<string> ExecuteInternalAsync(McpSetToolParams p, MessageContext context)
    {
        p.AuthType ??= "none";
        // Validate AuthType
        if (!new[] { "none", "bearer", "basic" }.Contains(p.AuthType.ToLowerInvariant()))
            throw new ArgumentException($"AuthType muss 'none', 'bearer' oder 'basic' sein, nicht '{p.AuthType}'");

        // Load existing registry
        var registry = await McpRegistry.LoadRegistryAsync(vfs);

        // Add or update server
        var existing = registry.Servers.FirstOrDefault(s => s.Name == p.Name);
        if (existing != null)
        {
            // Update
            existing.ServerUri = p.ServerUri;
            existing.AuthType = p.AuthType;
            existing.Description = p.Description;
            existing.Enabled = p.Enabled ?? true;
            logger.LogInformation($"Updated MCP server: {p.Name}");
        }
        else
        {
            // Add new
            registry.Servers.Add(new McpServerEntry
            {
                Name = p.Name,
                ServerUri = p.ServerUri,
                AuthType = p.AuthType,
                Description = p.Description,
                Enabled = p.Enabled ?? true,
                AddedAt = DateTime.UtcNow
            });
            logger.LogInformation($"Added new MCP server: {p.Name}");
        }

        // Save registry
        await McpRegistry.SaveRegistryAsync(registry, vfs);

        return $"✅ MCP Server '{p.Name}' gespeichert.\n" +
               $"URI: {p.ServerUri}\n" +
               $"Auth: {p.AuthType}\n" +
               $"Status: {((p.Enabled ?? true) ? "✅ Enabled" : "❌ Disabled")}";

    }
}
