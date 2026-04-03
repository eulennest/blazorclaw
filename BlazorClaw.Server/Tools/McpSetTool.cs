using BlazorClaw.Core.Commands;
using BlazorClaw.Core.Tools;
using BlazorClaw.Core.VFS;
using System.ComponentModel;
using System.ComponentModel.DataAnnotations;
using System.Text.Json;
using System.Text.Json.Serialization;

namespace BlazorClaw.Server.Tools;

public class McpSetToolParams : BaseToolParams
{
    [Description("Server-Name (eindeutig, z.B. 'github', 'memory', 'bitcoin')")]
    [Required]
    public string Name { get; set; } = string.Empty;

    [Description("Server URI (z.B. http://localhost:3000, ws://example.com, npx://@modelcontextprotocol/server-github)")]
    [Required]
    public string ServerUri { get; set; } = string.Empty;

    [Description("Auth-Type: 'none', 'bearer', 'basic' (default: 'none')")]
    public string AuthType { get; set; } = "none";

    [Description("Token-Name aus Vault falls bearer auth (z.B. 'GITHUB_MCP_TOKEN')")]
    public string? TokenName { get; set; }

    [Description("Beschreibung (optional)")]
    public string? Description { get; set; }

    [Description("Aktiviert (default: true)")]
    public bool Enabled { get; set; } = true;
}

public class McpSetTool(IVfsSystem vfs, MessageContextAccessor mca, ILogger<McpSetTool> logger)
    : BaseTool<McpSetToolParams>
{
    public override string Name => "mcp_set";
    public override string Description => """
        Fügt einen MCP-Server hinzu oder aktualisiert ihn in der Registry.
        
        Gespeichert in: /~secure/mcp.json
        
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
        try
        {
            // Validate AuthType
            if (!new[] { "none", "bearer", "basic" }.Contains(p.AuthType.ToLowerInvariant()))
                return $"ERROR: AuthType muss 'none', 'bearer' oder 'basic' sein, nicht '{p.AuthType}'";

            // If bearer/basic, require tokenName
            if ((p.AuthType == "bearer" || p.AuthType == "basic") && string.IsNullOrWhiteSpace(p.TokenName))
                return $"ERROR: tokenName ist erforderlich für authType '{p.AuthType}'";

            // Load existing registry
            var registry = await LoadRegistryAsync();

            // Add or update server
            var existing = registry.Servers.FirstOrDefault(s => s.Name == p.Name);
            if (existing != null)
            {
                // Update
                existing.ServerUri = p.ServerUri;
                existing.AuthType = p.AuthType;
                existing.TokenName = p.TokenName;
                existing.Description = p.Description;
                existing.Enabled = p.Enabled;
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
                    TokenName = p.TokenName,
                    Description = p.Description,
                    Enabled = p.Enabled,
                    AddedAt = DateTime.UtcNow
                });
                logger.LogInformation($"Added new MCP server: {p.Name}");
            }

            // Save registry
            await SaveRegistryAsync(registry);

            return $"✅ MCP Server '{p.Name}' gespeichert.\n" +
                   $"URI: {p.ServerUri}\n" +
                   $"Auth: {p.AuthType}\n" +
                   $"Status: {(p.Enabled ? "✅ Enabled" : "❌ Disabled")}";
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Error setting MCP server");
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
            logger.LogWarning(ex, "Could not load MCP registry, starting fresh");
            return new McpRegistry();
        }
    }

    private async Task SaveRegistryAsync(McpRegistry registry)
    {
        var filePath = GetRegistryPath();

        // Ensure directory exists
        var parentPath = filePath.ParentPath;
        if (!await vfs.ExistsAsync(parentPath))
            await vfs.CreateDirectoryAsync(parentPath);

        // Write JSON
        using var stream = await vfs.OpenFileAsync(filePath, FileMode.Create, FileAccess.Write);
        var options = new JsonSerializerOptions { WriteIndented = true };
        await JsonSerializer.SerializeAsync(stream, registry, options);
        
        logger.LogInformation($"MCP registry saved ({registry.Servers.Count} servers)");
    }

    private VfsPath GetRegistryPath()
    {
        return VfsPath.Parse(VfsPath.Parse("/~secure/"), "mcp.json", VfsPathParseMode.File);
    }
}
