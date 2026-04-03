using BlazorClaw.Core.Commands;
using BlazorClaw.Core.Tools;
using BlazorClaw.Core.VFS;
using System.ComponentModel;
using System.ComponentModel.DataAnnotations;
using System.Text.Json;
using System.Text.Json.Serialization;

namespace BlazorClaw.Server.Tools;

public class McpCallParams : BaseToolParams
{
    [Description("MCP Server URI oder Registry-Name:\n1. Registry: mcp://servername (schlägt in mcp.json nach, mit Access Control)\n2. Direkt: http://localhost:3000, ws://example.com, npx://@package")]
    [Required]
    public string ServerUri { get; set; } = string.Empty;

    [Description("JSON-RPC Methode (z.B. memory/search, filesystem/list, vault/get)")]
    [Required]
    public string Method { get; set; } = string.Empty;

    [Description("JSON-RPC Parameter als Dictionary (z.B. {\"query\": \"@SEARCH_QUERY\"} oder {\"path\": \"/home/user/file.txt\"})")]
    public Dictionary<string, object>? Params { get; set; }

    [Description("Bearer Token falls MCP Auth required (optional). Überschreibt Token aus Registry")]
    public string? BearerToken { get; set; }

    [Description("Timeout in Sekunden")]
    [Range(1, 300)]
    public int TimeoutSeconds { get; set; } = 30;

    [Description("SSL-Zertifikat ignorieren (nur für self-signed Certs)")]
    public bool IgnoreSslErrors { get; set; } = false;
}

public class McpCallTool(IHttpClientFactory httpClientFactory, IVfsSystem vfs, ILogger<McpCallTool> logger) : BaseTool<McpCallParams>
{
    public override string Name => "mcp_call";
    public override string Description => """
        Ruft MCP (Model Context Protocol) Server auf.
        
        Standard JSON-RPC 2.0 über HTTP/HTTPS.
        Automatisch: ID-Generierung, Envelope, Response-Parsing.
        
        WICHTIG: Nutze @VAR_NAME für variable Werte + VariableMappings!
        
        Beispiel (Memory Search):
        {
          "serverUrl": "http://blazorclaw-mcp:3000",
          "method": "memory/search",
          "params": {
            "query": "@SEARCH_TERM",
            "limit": 5
          },
          "variableMappings": {
            "SEARCH_TERM": "env:MEMORY_SEARCH"
          },
          "bearerToken": "@MCP_TOKEN",
          "timeoutSeconds": 30
        }
        
        Beispiel (Filesystem List):
        {
          "serverUrl": "https://blazorclaw-mcp.example.com",
          "method": "filesystem/list",
          "params": {
            "path": "/home/user/documents"
          }
        }
        """;

    protected override async Task<string> ExecuteInternalAsync(McpCallParams p, MessageContext context)
    {
        try
        {
            // Resolve variables (same as HttpRequestTool)
            await p.ResolveVarsAsync(context);

            var serverUri = p.ServerUri;
            var method = p.Method;
            var bearerToken = p.BearerToken;

            // Validate Method first
            if (string.IsNullOrWhiteSpace(method))
                return "ERROR: Method ist erforderlich";

            // Handle mcp:// schema (registry lookup with access control)
            if (serverUri.StartsWith("mcp://"))
            {
                var (resolvedUri, resolvedToken) = await ResolveMcpRegistryAsync(serverUri, bearerToken);
                if (resolvedUri.StartsWith("ERROR:"))
                    return resolvedUri;
                serverUri = resolvedUri;
                bearerToken = resolvedToken ?? bearerToken;
            }
            else
            {
                // Check direct URLs against registry (disabled check)
                var accessResult = await CheckDirectUrlAccessAsync(serverUri);
                if (accessResult.StartsWith("ERROR:"))
                    return accessResult;
            }

            // Validate ServerUri
            if (!Uri.TryCreate(serverUri, UriKind.Absolute, out var uri))
                return $"ERROR: Ungültige MCP Server URI: {serverUri}";

            // Build JSON-RPC Request
            var requestId = Guid.NewGuid().ToString("N").Substring(0, 8);
            var jsonRpcRequest = new
            {
                jsonrpc = "2.0",
                method = method,
                @params = p.Params ?? new Dictionary<string, object>(),
                id = requestId
            };

            var requestJson = JsonSerializer.Serialize(jsonRpcRequest);
            logger.LogInformation($"MCP Request to {serverUri}: {requestJson}");

            // Create HTTP Client
            var httpClient = httpClientFactory.CreateClient();
            httpClient.Timeout = TimeSpan.FromSeconds(p.TimeoutSeconds);

            // Handle SSL errors
            if (p.IgnoreSslErrors)
            {
                // Note: For production, use proper certificate validation
                // This is only for self-signed certificates in development
            }

            // Build request
            var request = new HttpRequestMessage(HttpMethod.Post, serverUri)
            {
                Content = new StringContent(requestJson, System.Text.Encoding.UTF8, "application/json")
            };

            // Add auth if provided
            if (!string.IsNullOrWhiteSpace(bearerToken))
            {
                request.Headers.Authorization = new System.Net.Http.Headers.AuthenticationHeaderValue("Bearer", bearerToken);
            }

            // Send request
            var response = await httpClient.SendAsync(request);

            if (!response.IsSuccessStatusCode)
            {
                var errorContent = await response.Content.ReadAsStringAsync();
                return $"ERROR: HTTP {response.StatusCode}\n{errorContent}";
            }

            // Parse response
            var responseContent = await response.Content.ReadAsStringAsync();
            using var jsonDoc = JsonDocument.Parse(responseContent);
            var root = jsonDoc.RootElement;

            // Check for JSON-RPC error
            if (root.TryGetProperty("error", out var errorElement) && errorElement.ValueKind != JsonValueKind.Null)
            {
                var errorCode = errorElement.GetProperty("code").GetInt32();
                var errorMessage = errorElement.GetProperty("message").GetString();
                return $"ERROR: JSON-RPC Error {errorCode}: {errorMessage}";
            }

            // Extract result
            if (root.TryGetProperty("result", out var resultElement))
            {
                var resultJson = resultElement.GetRawText();
                logger.LogInformation($"MCP Result: {resultJson}");
                return resultJson;
            }

            return responseContent;
        }
        catch (HttpRequestException ex)
        {
            logger.LogError($"MCP HTTP Error: {ex.Message}");
            return $"ERROR: HTTP Request failed: {ex.Message}";
        }
        catch (TimeoutException)
        {
            return $"ERROR: MCP Server Timeout ({p.TimeoutSeconds}s)";
        }
        catch (Exception ex)
        {
            logger.LogError($"MCP Call Error: {ex}");
            return $"ERROR: {ex.Message}";
        }
    }

    private async Task<(string uri, string? token)> ResolveMcpRegistryAsync(string mcpUri, string? bearerToken)
    {
        try
        {
            // Parse mcp://servername format
            // Example: mcp://memory
            var serverName = mcpUri.Substring("mcp://".Length).Trim();

            if (string.IsNullOrWhiteSpace(serverName))
                return ("ERROR: mcp:// URI Format: mcp://servername", null);

            // Load registry
            var registry = await LoadRegistryAsync();

            // Find matching server (enabled + in registry)
            var server = registry.Servers.FirstOrDefault(s => s.Name == serverName);
            if (server == null)
                return ($"ERROR: MCP Server '{serverName}' nicht in Registry gefunden. Nutze mcp_set oder mcp_list.", null);

            // Check if enabled
            if (!server.Enabled)
                return ($"ERROR: MCP Server '{serverName}' ist deaktiviert. Nutze mcp_set um zu aktivieren.", null);

            // Use server's bearer token if configured and not overridden
            var resolvedToken = bearerToken;
            if (string.IsNullOrWhiteSpace(bearerToken) && server.AuthType == "bearer" && !string.IsNullOrWhiteSpace(server.TokenName))
                resolvedToken = server.TokenName; // TODO: Resolve from Vault (VariableResolver)

            logger.LogInformation($"Resolved mcp://{serverName} to {server.ServerUri}");
            return (server.ServerUri, resolvedToken);
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Error resolving mcp:// registry");
            return ($"ERROR: mcp:// resolution failed: {ex.Message}", null);
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
            logger.LogWarning(ex, "Could not load MCP registry");
            return new McpRegistry();
        }
    }

    private async Task<string> CheckDirectUrlAccessAsync(string serverUri)
    {
        try
        {
            // Check if this direct URL is registered in the registry
            var registry = await LoadRegistryAsync();
            
            // Match with StartsWith (case-insensitive) to handle query params, fragments, paths
            // This prevents bypassing security by appending ?token=x or #fragment
            var registeredServer = registry.Servers.FirstOrDefault(s => 
                serverUri.StartsWith(s.ServerUri, StringComparison.OrdinalIgnoreCase));

            if (registeredServer == null)
            {
                // URL not in registry → allowed (permissive by default)
                logger.LogInformation($"Direct URL {serverUri} not in registry, allowing access");
                return "OK";
            }

            // URL is in registry
            if (!registeredServer.Enabled)
            {
                // Registered but disabled → blocked
                return $"ERROR: MCP Server '{registeredServer.Name}' ist deaktiviert. Nutze mcp_set um zu aktivieren.";
            }

            // Registered and enabled → allowed
            logger.LogInformation($"Direct URL {serverUri} matches registered server '{registeredServer.Name}' (enabled)");
            return "OK";
        }
        catch (Exception ex)
        {
            logger.LogWarning(ex, "Error checking direct URL access");
            // On error, allow access (fail-open, security via registry)
            return "OK";
        }
    }

    private VfsPath GetRegistryPath()
    {
        return VfsPath.Parse(VfsPath.Parse("/~secure/"), "mcp.json", VfsPathParseMode.File);
    }
}
