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
    [Description("MCP Server URI - 2 Optionen:\n1. Direkt: http://localhost:3000, ws://example.com, npx://@package\n2. Aus Registry: mcp://servername (nutzt mcp_list zum Nachschlag)")]
    [Required]
    public string ServerUri { get; set; } = string.Empty;

    [Description("JSON-RPC Methode (z.B. memory/search, filesystem/list, vault/get)\nBei mcp:// Schema wird Method automatisch aus URI extrahiert")]
    public string? Method { get; set; }

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

            // Handle mcp:// schema (registry lookup)
            if (serverUri.StartsWith("mcp://"))
            {
                var (resolvedUri, resolvedMethod, resolvedToken) = await ResolveMcpSchemaAsync(serverUri, method, bearerToken);
                if (resolvedUri.StartsWith("ERROR:"))
                    return resolvedUri;
                serverUri = resolvedUri;
                method = resolvedMethod ?? method;
                bearerToken = resolvedToken ?? bearerToken;
            }

            // Validate ServerUri
            if (!Uri.TryCreate(serverUri, UriKind.Absolute, out var uri))
                return $"ERROR: Ungültige MCP Server URI: {serverUri}";

            // Validate Method
            if (string.IsNullOrWhiteSpace(method))
                return "ERROR: Method ist erforderlich";

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

    private async Task<(string uri, string? method, string? token)> ResolveMcpSchemaAsync(string mcpUri, string? method, string? bearerToken)
    {
        try
        {
            // Parse mcp://servername/method format
            // Examples: mcp://memory/search, mcp://github/pr/create
            var uriPart = mcpUri.Substring("mcp://".Length);
            var parts = uriPart.Split('/', StringSplitOptions.RemoveEmptyEntries);

            if (parts.Length < 1)
                return ("ERROR: mcp:// URI format: mcp://servername/method", null, null);

            var serverName = parts[0];
            var methodPath = string.Join("/", parts.Skip(1));

            // Load registry
            var registry = await LoadRegistryAsync();

            // Find matching server
            var server = registry.Servers.FirstOrDefault(s => s.Name == serverName && s.Enabled);
            if (server == null)
                return ($"ERROR: MCP Server '{serverName}' nicht in Registry gefunden oder deaktiviert. Nutze mcp_list zum Anzeigen.", null, null);

            // Set method from URI path if not provided
            var resolvedMethod = method;
            if (string.IsNullOrWhiteSpace(method) && !string.IsNullOrWhiteSpace(methodPath))
                resolvedMethod = methodPath;

            // Use server's bearer token if configured and not overridden
            var resolvedToken = bearerToken;
            if (string.IsNullOrWhiteSpace(bearerToken) && server.AuthType == "bearer" && !string.IsNullOrWhiteSpace(server.TokenName))
                resolvedToken = server.TokenName; // TODO: Resolve from Vault

            logger.LogInformation($"Resolved mcp://{serverName} to {server.ServerUri}");
            return (server.ServerUri, resolvedMethod, resolvedToken);
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Error resolving mcp:// schema");
            return ($"ERROR: mcp:// resolution failed: {ex.Message}", null, null);
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

    private VfsPath GetRegistryPath()
    {
        return VfsPath.Parse(VfsPath.Parse("/~secure/"), "mcp.json", VfsPathParseMode.File);
    }
}
