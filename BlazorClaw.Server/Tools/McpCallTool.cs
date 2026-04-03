using BlazorClaw.Core.Commands;
using BlazorClaw.Core.Tools;
using BlazorClaw.Core.VFS;
using System.ComponentModel;
using System.ComponentModel.DataAnnotations;
using System.Net.Sockets;
using System.Text.Json;
using System.Text.Json.Serialization;

namespace BlazorClaw.Server.Tools;

public class McpCallParams : BaseToolParams
{
    [Description("MCP Server URI oder Registry-Name:\n1. Registry: mcp://servername (schlägt in mcp.json nach, mit Access Control)\n2. HTTP(S): http://localhost:3000, https://example.com\n3. Unix-Socket: unix:///path/to/socket\n4. Exec (TODO): exec://npx?@package, exec:///bin/binary")]
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

public class McpCallTool(IHttpClientFactory httpClientFactory, IVfsSystem vfs, ILogger<McpCallTool> logger) 
    : BaseTool<McpCallParams>
{
    public override string Name => "mcp_call";
    public override string Description => """
        Ruft MCP (Model Context Protocol) Server auf über verschiedene Transport-Layer.
        
        Supported Transports:
        - http://, https:// - Standard HTTP/HTTPS JSON-RPC
        - unix:// - Unix Domain Sockets (IPC)
        - mcp:// - Registry-basiert (schlägt in mcp.json nach)
        - exec:// - (TODO) Lokale Prozesse via Exec
        
        WICHTIG: Nutze @VAR_NAME für variable Werte + VariableMappings!
        
        Beispiel 1 (HTTP):
        {
          "serverUri": "http://blazorclaw-mcp:3000",
          "method": "memory/search",
          "params": {"query": "@SEARCH_TERM"},
          "variableMappings": {"SEARCH_TERM": "env:MEMORY_SEARCH"}
        }
        
        Beispiel 2 (Registry):
        {
          "serverUri": "mcp://memory",
          "method": "search",
          "params": {"query": "..."}
        }
        
        Beispiel 3 (Unix Socket):
        {
          "serverUri": "unix:///tmp/mcp-server.sock",
          "method": "memory/search",
          "params": {"query": "..."}
        }
        """;

    protected override async Task<string> ExecuteInternalAsync(McpCallParams p, MessageContext context)
    {
        try
        {
            // Resolve variables
            await p.ResolveVarsAsync(context);

            var serverUri = p.ServerUri;
            var method = p.Method;
            var bearerToken = p.BearerToken;

            // Validate Method
            if (string.IsNullOrWhiteSpace(method))
                return "ERROR: Method ist erforderlich";

            // Handle mcp:// schema (registry lookup)
            if (serverUri.StartsWith("mcp://", StringComparison.OrdinalIgnoreCase))
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

            // Build JSON-RPC Request
            var jsonRpcRequest = BuildJsonRpcRequest(method, p.Params);
            logger.LogInformation($"MCP Request to {serverUri}: {jsonRpcRequest}");

            // Dispatch to transport layer based on schema
            string responseJson;
            if (serverUri.StartsWith("http://", StringComparison.OrdinalIgnoreCase) || 
                serverUri.StartsWith("https://", StringComparison.OrdinalIgnoreCase))
            {
                responseJson = await CallViaHttpAsync(serverUri, jsonRpcRequest, bearerToken, p.TimeoutSeconds, p.IgnoreSslErrors);
            }
            else if (serverUri.StartsWith("unix://", StringComparison.OrdinalIgnoreCase))
            {
                responseJson = await CallViaUnixSocketAsync(serverUri, jsonRpcRequest);
            }
            else if (serverUri.StartsWith("exec://", StringComparison.OrdinalIgnoreCase))
            {
                return "ERROR: exec:// transport not yet implemented. Use http:// or unix:// for now.";
            }
            else
            {
                return $"ERROR: Unsupported schema in URI: {serverUri}";
            }

            if (responseJson.StartsWith("ERROR:"))
                return responseJson;

            // Parse result from response
            return ExtractJsonRpcResult(responseJson);
        }
        catch (HttpRequestException ex)
        {
            logger.LogError($"MCP HTTP Error: {ex.Message}");
            return $"ERROR: HTTP Request failed: {ex.Message}";
        }
        catch (TimeoutException)
        {
            return $"ERROR: MCP Server Timeout";
        }
        catch (Exception ex)
        {
            logger.LogError($"MCP Call Error: {ex}");
            return $"ERROR: {ex.Message}";
        }
    }

    // === JSON-RPC Helpers ===

    private string BuildJsonRpcRequest(string method, Dictionary<string, object>? @params)
    {
        var requestId = Guid.NewGuid().ToString("N").Substring(0, 8);
        var jsonRpcRequest = new
        {
            jsonrpc = "2.0",
            method = method,
            @params = @params ?? new Dictionary<string, object>(),
            id = requestId
        };
        return JsonSerializer.Serialize(jsonRpcRequest);
    }

    private string ExtractJsonRpcResult(string responseJson)
    {
        try
        {
            using var jsonDoc = JsonDocument.Parse(responseJson);
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
                return resultElement.GetRawText();
            }

            return responseJson;
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Error parsing JSON-RPC response");
            return $"ERROR: Failed to parse response: {ex.Message}";
        }
    }

    // === Transport Layer: HTTP ===

    private async Task<string> CallViaHttpAsync(string serverUri, string jsonRpcRequest, string? bearerToken, int timeoutSeconds, bool ignoreSslErrors)
    {
        try
        {
            var httpClient = httpClientFactory.CreateClient();
            httpClient.Timeout = TimeSpan.FromSeconds(timeoutSeconds);

            var request = new HttpRequestMessage(HttpMethod.Post, serverUri)
            {
                Content = new StringContent(jsonRpcRequest, System.Text.Encoding.UTF8, "application/json")
            };

            if (!string.IsNullOrWhiteSpace(bearerToken))
            {
                request.Headers.Authorization = new System.Net.Http.Headers.AuthenticationHeaderValue("Bearer", bearerToken);
            }

            var response = await httpClient.SendAsync(request);

            if (!response.IsSuccessStatusCode)
            {
                var errorContent = await response.Content.ReadAsStringAsync();
                return $"ERROR: HTTP {response.StatusCode}\n{errorContent}";
            }

            return await response.Content.ReadAsStringAsync();
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Error calling MCP via HTTP");
            return $"ERROR: HTTP call failed: {ex.Message}";
        }
    }

    // === Transport Layer: Unix Socket ===

    private async Task<string> CallViaUnixSocketAsync(string socketUri, string jsonRpcRequest)
    {
        try
        {
            // Parse unix:///path/to/socket format
            var socketPath = socketUri.Substring("unix://".Length);
            if (!socketPath.StartsWith("/"))
                socketPath = "/" + socketPath;

            logger.LogInformation($"Connecting to Unix socket: {socketPath}");

            var socket = new System.Net.Sockets.Socket(System.Net.Sockets.AddressFamily.Unix, System.Net.Sockets.SocketType.Stream, System.Net.Sockets.ProtocolType.Unspecified);
            var endpoint = new UnixDomainSocketEndPoint(socketPath);
            
            await socket.ConnectAsync(endpoint);

            // Send request
            var requestBytes = System.Text.Encoding.UTF8.GetBytes(jsonRpcRequest + "\n");
            await socket.SendAsync(requestBytes, SocketFlags.None);

            // Receive response
            var buffer = new byte[1024 * 64]; // 64KB buffer
            int bytesRead = await socket.ReceiveAsync(buffer, SocketFlags.None);
            
            if (bytesRead == 0)
                return "ERROR: Unix socket returned no data";

            var responseJson = System.Text.Encoding.UTF8.GetString(buffer, 0, bytesRead);
            return responseJson;
        }
        catch (FileNotFoundException)
        {
            return $"ERROR: Unix socket not found. Check if the MCP server is running.";
        }
        catch (SocketException ex)
        {
            logger.LogError(ex, "Unix socket error");
            return $"ERROR: Unix socket error: {ex.Message}";
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Error calling MCP via Unix socket");
            return $"ERROR: Unix socket call failed: {ex.Message}";
        }
    }

    // === Transport Layer: Exec (TODO) ===
    // TODO: Implement exec:// transport
    // Pattern:
    // 1. Parse exec:///path/to/binary or exec://npx?@package
    // 2. Start process via Process.Start()
    // 3. Write jsonRpcRequest to process.StandardInput
    // 4. Read response from process.StandardOutput
    // 5. Close process gracefully

    // === Registry & Access Control ===

    private async Task<(string uri, string? token)> ResolveMcpRegistryAsync(string mcpUri, string? bearerToken)
    {
        try
        {
            var serverName = mcpUri.Substring("mcp://".Length).Trim();

            if (string.IsNullOrWhiteSpace(serverName))
                return ("ERROR: mcp:// URI Format: mcp://servername", null);

            var registry = await LoadRegistryAsync();
            var server = registry.Servers.FirstOrDefault(s => s.Name == serverName);
            
            if (server == null)
                return ($"ERROR: MCP Server '{serverName}' nicht in Registry gefunden. Nutze mcp_set oder mcp_list.", null);

            if (!server.Enabled)
                return ($"ERROR: MCP Server '{serverName}' ist deaktiviert. Nutze mcp_set um zu aktivieren.", null);

            var resolvedToken = bearerToken;
            if (string.IsNullOrWhiteSpace(bearerToken) && server.AuthType == "bearer" && !string.IsNullOrWhiteSpace(server.TokenName))
                resolvedToken = server.TokenName;

            logger.LogInformation($"Resolved mcp://{serverName} to {server.ServerUri}");
            return (server.ServerUri, resolvedToken);
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Error resolving mcp:// registry");
            return ($"ERROR: mcp:// resolution failed: {ex.Message}", null);
        }
    }

    private async Task<string> CheckDirectUrlAccessAsync(string serverUri)
    {
        try
        {
            var registry = await LoadRegistryAsync();
            
            // Match with StartsWith (case-insensitive) to handle query params, fragments, paths
            var registeredServer = registry.Servers.FirstOrDefault(s => 
                serverUri.StartsWith(s.ServerUri, StringComparison.OrdinalIgnoreCase));

            if (registeredServer == null)
            {
                logger.LogInformation($"Direct URL {serverUri} not in registry, allowing access");
                return "OK";
            }

            if (!registeredServer.Enabled)
            {
                return $"ERROR: MCP Server '{registeredServer.Name}' ist deaktiviert. Nutze mcp_set um zu aktivieren.";
            }

            logger.LogInformation($"Direct URL {serverUri} matches registered server '{registeredServer.Name}' (enabled)");
            return "OK";
        }
        catch (Exception ex)
        {
            logger.LogWarning(ex, "Error checking direct URL access");
            return "OK";
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

// Data classes are in McpShared.cs (shared by McpCallTool, McpListTool, McpSetTool)
