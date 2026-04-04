using BlazorClaw.Core.VFS;
using Microsoft.CodeAnalysis.Elfie.Diagnostics;
using System.Text.Json;
using System.Text.Json.Serialization;

namespace BlazorClaw.Server.Tools.Mcp;

/// <summary>
/// Shared data classes for MCP (Model Context Protocol) tools.
/// Used by: McpCallTool, McpListTool, McpSetTool
/// </summary>

public class McpServerEntry
{
    [JsonPropertyName("name")]
    public string Name { get; set; } = string.Empty;

    [JsonPropertyName("serverUri")]
    public string ServerUri { get; set; } = string.Empty;

    [JsonPropertyName("authType")]
    public string AuthType { get; set; } = "none";

    [JsonPropertyName("tokenName")]
    public string? TokenName { get; set; }

    [JsonPropertyName("description")]
    public string? Description { get; set; }

    [JsonPropertyName("enabled")]
    public bool Enabled { get; set; } = true;

    [JsonPropertyName("addedAt")]
    public DateTime AddedAt { get; set; } = DateTime.UtcNow;
}

public class McpRegistry
{
    [JsonPropertyName("servers")]
    public List<McpServerEntry> Servers { get; set; } = new();

    public static async Task<McpRegistry> LoadRegistryAsync(IVfsSystem vfs)
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
        catch (Exception)
        {
            return new McpRegistry();
        }
    }

    public static async Task SaveRegistryAsync(McpRegistry registry, IVfsSystem vfs)
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

    }

    private static VfsPath GetRegistryPath()
    {
        return VfsPath.Parse("/~secure/mcp.json");
    }
}
