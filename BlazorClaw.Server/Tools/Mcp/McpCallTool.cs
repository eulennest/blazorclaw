using BlazorClaw.Core.Commands;
using BlazorClaw.Core.Tools;
using BlazorClaw.Core.Utils;
using BlazorClaw.Core.VFS;
using System.ComponentModel;
using System.ComponentModel.DataAnnotations;
using System.Net.Sockets;
using System.Text.Json;
using System.Text.Json.Serialization;

namespace BlazorClaw.Server.Tools.Mcp;

public class McpCallParams : BaseToolParams
{
    [Description("MCP Server URI oder Registry-Name:\n1. Registry: mcp://servername (empfohlen)\n2. HTTP(S): http://localhost:3000, https://example.com\n3. Unix-Socket: unix:///path/to/socket\n4. Exec: exec://npx?@package, exec:///bin/binary")]
    [Required]
    public string ServerUri { get; set; } = string.Empty;

    [Description("MCP JSON-RPC Methode. Standard-Methoden:\n- 'initialize': MCP-Handshake (Client-Capabilities mitteilen)\n- 'tools/list': Liste alle verfügbaren Tools auf\n- 'tools/call': Rufe ein Tool auf (params enthalten 'name' und 'arguments')\n\nBeispiele:\n- method='initialize' → params={\"protocolVersion\": \"2024-11-05\", \"capabilities\": {...}, \"clientInfo\": {...}}\n- method='tools/list' → params={} (optional)\n- method='tools/call' → params={\"name\": \"web_search\", \"arguments\": {\"query\": \"...\"}}")]
    [Required]
    public string Method { get; set; } = string.Empty;

    [Description("JSON-RPC Parameter als Dictionary (struktur hängt von Methode ab):\n- initialize: {\"protocolVersion\": \"2024-11-05\", \"capabilities\": {...}, \"clientInfo\": {\"name\": \"...\", \"version\": \"...\"}}\n- tools/list: {} (optional, leer)\n- tools/call: {\"name\": \"tool_name\", \"arguments\": {\"field\": \"value\", ...}}\n\nFür variable Werte: nutze @VAR_NAME + VariableMappings")]
    public Dictionary<string, object>? Params { get; set; }

    [Description("Bearer Token für MCP Server-Auth (optional). Wird an Authorization Header angehängt")]
    public string? BearerToken { get; set; }

    [Description("Timeout in Sekunden (default: 30, max: 300)")]
    [Range(1, 300)]
    public int TimeoutSeconds { get; set; } = 30;

    [Description("SSL-Zertifikat-Validierung ignorieren (nur für self-signed Certs, default: false)")]
    public bool? IgnoreSslErrors { get; set; } = false;
}

public class McpCallTool(IHttpClientFactory httpClientFactory, IVfsSystem vfs, ILogger<McpCallTool> logger)
    : BaseTool<McpCallParams>
{
    public override string Name => "mcp_call";
    public override string Description => """
        Ruft einen MCP (Model Context Protocol) Server auf über verschiedene Transport-Layer.
        
        MCP Standard-Workflow:
        1. initialize → Capabilities ermitteln (einmalig)
        2. tools/list → Verfügbare Tools auflisten (optional)
        3. tools/call → Spezifisches Tool aufrufen (wiederholt)
        
        Supported Transports:
        - http://, https:// - Standard HTTP/HTTPS JSON-RPC (remote/lokal)
        - unix:// - Unix Domain Sockets (lokal, schneller)
        - mcp:// - Registry-basiert (aus /~secure/mcp.json)
        - exec:// - Lokale Prozesse via exec (TODO)
        
        WICHTIG: Nutze @VAR_NAME für variable Werte + VariableMappings!
        
        Beispiel 1 - Initialize (Client sendet Capabilities):
        {
          "serverUri": "https://example.com/mcp",
          "method": "initialize",
          "params": {
            "protocolVersion": "2024-11-05",
            "capabilities": {
              "roots": {"listChanged": true},
              "sampling": {}
            },
            "clientInfo": {"name": "BlazorClaw Client", "version": "1.0.0"}
          },
          "bearerToken": "@SESSION_TOKEN",
          "variableMappings": {"SESSION_TOKEN": "vault:MySessionId"}
        }
        
        Beispiel 2 - List Tools (Query alle verfügbaren Tools):
        {
          "serverUri": "mcp://blazorclaw",
          "method": "tools/list"
        }
        
        Beispiel 3 - Call Tool (Spezifisches Tool ausführen):
        {
          "serverUri": "mcp://blazorclaw",
          "method": "tools/call",
          "params": {
            "name": "web_search",
            "arguments": {
              "query": "@SEARCH_QUERY"
            }
          },
          "bearerToken": "@SESSION_TOKEN",
          "variableMappings": {
            "SEARCH_QUERY": "env:SEARCH_INPUT",
            "SESSION_TOKEN": "vault:MySessionId"
          }
        }
        """;

    protected override async Task<string> ExecuteInternalAsync(McpCallParams p, MessageContext context)
    {
        // Resolve variables
        await p.ResolveVarsAsync(context);
        if (!Uri.TryCreate(p.ServerUri, UriKind.Absolute, out var serverUri))
            throw new ArgumentException("Ungültige ServerUri. Stelle sicher, dass es sich um eine absolute URI handelt (z.B. http://, https://, unix://, mcp://).", nameof(p.ServerUri));

        var scheme = serverUri.Scheme.ToLowerInvariant();
        var method = p.Method;
        var bearerToken = p.BearerToken;

        // Validate Method
        if (string.IsNullOrWhiteSpace(method))
            throw new ArgumentException("Method ist erforderlich.", nameof(p.Method));

        var serverInfo = await ResolveMcpRegistryAsync(serverUri);

        if (!(serverInfo?.Enabled ?? false))
            throw new InvalidOperationException($"MCP Server '{serverInfo?.Name ?? serverUri.Host}' ist deaktiviert.");

        if (serverInfo == null && !scheme.StartsWith("http"))
            throw new InvalidOperationException($"MCP Server '{serverUri.Host}' nicht gefunden im Registry. Stelle sicher, dass die URI korrekt ist oder verwende http:// oder unix:// Schema für direkte URLs.");

        if (serverInfo != null)
        {
            if (!Uri.TryCreate(serverInfo.ServerUri, UriKind.Absolute, out serverUri))
                throw new ArgumentException("Ungültige ServerUri. Stelle sicher, dass es sich um eine absolute URI handelt (z.B. http://, https://, unix://, mcp://).", nameof(p.ServerUri));
            scheme = serverUri.Scheme.ToLowerInvariant();
        }

        // Build JSON-RPC Request
        var jsonRpcRequest = BuildJsonRpcRequest(method, p.Params);
        logger.LogInformation("MCP Request to {serverUri}: {jsonRpcRequest}", serverUri, jsonRpcRequest);

        // Dispatch to transport layer based on schema
        string responseJson;
        if ("http".Equals(scheme) || "https".Equals(scheme))
        {
            responseJson = await CallViaHttpAsync(serverUri, jsonRpcRequest, bearerToken, p.TimeoutSeconds, p.IgnoreSslErrors ?? false);
        }
        else if ("unix".Equals(scheme))
        {
            // Parse unix:///path/to/socket format
            var socketPath = string.IsNullOrWhiteSpace(serverUri.Host) ? serverUri.AbsolutePath : $"/{serverUri.Host}{serverUri.AbsolutePath}";
            var socket = new Socket(AddressFamily.Unix, SocketType.Stream, ProtocolType.Unspecified);
            var endpoint = new UnixDomainSocketEndPoint(socketPath);
            responseJson = await CallViaSocketAsync(socket, endpoint, jsonRpcRequest);
        }
        else if ("tcp".Equals(scheme) || "udp".Equals(scheme))
        {
            var socket = new Socket(AddressFamily.InterNetwork, SocketType.Stream, "tcp".Equals(scheme) ? ProtocolType.Tcp : ProtocolType.Udp);
            var endpoint = new System.Net.IPEndPoint(System.Net.IPAddress.Parse(serverUri.Host), serverUri.Port);
            responseJson = await CallViaSocketAsync(socket, endpoint, jsonRpcRequest);
        }
        else if ("exec".Equals(scheme))
        {
            throw new NotImplementedException("exec:// transport is not yet implemented. Use http:// or unix:// for now.");
        }
        else
            throw new NotSupportedException($"Unsupported URI scheme: {scheme}. Supported schemes are http://, https://, unix://, mcp://, exec://");

        // Parse result from response
        return ExtractJsonRpcResult(responseJson);
    }

    // === JSON-RPC Helpers ===

    private string BuildJsonRpcRequest(string method, Dictionary<string, object>? @params)
    {
        var requestId = Guid.NewGuid().ToString("N").Substring(0, 8);
        var jsonRpcRequest = new
        {
            jsonrpc = "2.0",
            method = method,
            @params = @params ?? [],
            id = requestId
        };
        return JsonSerializer.Serialize(jsonRpcRequest);
    }

    private static string ExtractJsonRpcResult(string responseJson)
    {
        using var jsonDoc = JsonDocument.Parse(responseJson);
        var root = jsonDoc.RootElement;

        // Check for JSON-RPC error
        if (root.TryGetProperty("error", out var errorElement) && errorElement.ValueKind != JsonValueKind.Null)
        {
            var errorCode = errorElement.GetProperty("code").GetInt32();
            var errorMessage = errorElement.GetProperty("message").GetString();
            throw new Exception($"JSON-RPC Error {errorCode}: {errorMessage}");
        }

        // Extract result
        if (root.TryGetProperty("result", out var resultElement))
        {
            return resultElement.GetRawText();
        }

        return responseJson;
    }

    // === Transport Layer: HTTP ===

    private async Task<string> CallViaHttpAsync(Uri serverUri, string jsonRpcRequest, string? bearerToken, int timeoutSeconds, bool ignoreSslErrors)
    {
        var httpClient = httpClientFactory.CreateClient(ignoreSslErrors ? "InsecureHttpClient" : "HttpClient");
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

    // === Transport Layer: Unix Socket ===

    private async Task<string> CallViaSocketAsync(Socket socket, System.Net.EndPoint endpoint, string jsonRpcRequest)
    {

        using var sck = socket;
        await sck.ConnectAsync(endpoint);

        // Send request
        var requestBytes = System.Text.Encoding.UTF8.GetBytes(jsonRpcRequest + "\n");
        await sck.SendAsync(requestBytes, SocketFlags.None);

        // Receive response
        var buffer = new byte[1024 * 64]; // 64KB buffer
        int bytesRead = await sck.ReceiveAsync(buffer, SocketFlags.None);

        if (bytesRead == 0)
            throw new Exception("Unix socket returned no data");

        var responseJson = System.Text.Encoding.UTF8.GetString(buffer, 0, bytesRead);
        return responseJson;
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

    private async Task<McpServerEntry?> ResolveMcpRegistryAsync(Uri mcpUri)
    {
        var registry = await LoadRegistryAsync();

        if ("mcp".Equals(mcpUri.Scheme, StringComparison.InvariantCultureIgnoreCase))
        {
            return registry.Servers.FirstOrDefault(s => s.Name.Equals(mcpUri.Host, StringComparison.InvariantCultureIgnoreCase));
        }

        var server = mcpUri.ToString();
        return registry.Servers.FirstOrDefault(s => s.ServerUri.StartsWith(server, StringComparison.InvariantCultureIgnoreCase));
    }

    private async Task<McpRegistry> LoadRegistryAsync()
    {
        try
        {
            var filePath = VfsPath.Parse("/~secure/mcp.json");
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
}

// Data classes are in McpShared.cs (shared by McpCallTool, McpListTool, McpSetTool)
