using BlazorClaw.Core.VFS;
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

    [JsonPropertyName("description")]
    public string? Description { get; set; }

    [JsonPropertyName("access")]
    public AccessState Access { get; set; } = AccessState.Enabled;

    [JsonPropertyName("addedAt")]
    public DateTime AddedAt { get; set; } = DateTime.UtcNow;
}

public class McpRegistry
{
    [JsonPropertyName("servers")]
    public List<McpServerEntry> Servers { get; set; } = [];

    public static Task<McpRegistry> LoadRegistryAsync(IVfsSystem vfs, VfsPath path) => LoadRegistryAsync(new VfsEntity(vfs, path));
    public static async Task<McpRegistry> LoadRegistryAsync(VfsEntity vfs)
    {
        try
        {
            var filePath = vfs.Path;
            if (!await vfs.VFS.ExistsAsync(filePath))
                return new McpRegistry();

            using var stream = await vfs.VFS.OpenFileAsync(filePath, FileMode.Open, FileAccess.Read);
            return await JsonSerializer.DeserializeAsync<McpRegistry>(stream)
                ?? new McpRegistry();
        }
        catch (Exception)
        {
            return new McpRegistry();
        }
    }

    public static Task SaveRegistryAsync(McpRegistry registry, IVfsSystem vfs, VfsPath path) => SaveRegistryAsync(registry, new VfsEntity(vfs, path));
    public static async Task SaveRegistryAsync(McpRegistry registry, VfsEntity vfs)
    {
        var filePath = vfs.Path;

        // Ensure directory exists
        var parentPath = filePath.ParentPath;
        if (!await vfs.VFS.ExistsAsync(parentPath))
            await vfs.VFS.CreateDirectoryAsync(parentPath);

        // Write JSON
        using var stream = await vfs.VFS.OpenFileAsync(filePath, FileMode.Create, FileAccess.Write);
        var options = new JsonSerializerOptions { WriteIndented = true };
        await JsonSerializer.SerializeAsync(stream, registry, options);

    }
}


public enum AccessState
{
    Disabled = 0,
    Enabled = 1,
    Autostart = 2
}