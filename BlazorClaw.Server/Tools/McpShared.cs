using System.Text.Json.Serialization;

namespace BlazorClaw.Server.Tools;

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
}
