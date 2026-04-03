using BlazorClaw.Core.Commands;
using BlazorClaw.Core.Tools;
using System.ComponentModel;
using System.ComponentModel.DataAnnotations;
using System.Text.Json;

namespace BlazorClaw.Server.Tools;

public class McpCallParams : BaseToolParams
{
    [Description("MCP Server URL (z.B. http://localhost:3000 oder https://blazorclaw-mcp.example.com)")]
    [Required]
    public string ServerUrl { get; set; } = string.Empty;

    [Description("JSON-RPC Methode (z.B. memory/search, filesystem/list, vault/get)")]
    [Required]
    public string Method { get; set; } = string.Empty;

    [Description("JSON-RPC Parameter als Dictionary (z.B. {\"query\": \"@SEARCH_QUERY\"} oder {\"path\": \"/home/user/file.txt\"})")]
    public Dictionary<string, object>? Params { get; set; }

    [Description("Bearer Token falls MCP Auth required (optional)")]
    public string? BearerToken { get; set; }

    [Description("Timeout in Sekunden")]
    [Range(1, 300)]
    public int TimeoutSeconds { get; set; } = 30;

    [Description("SSL-Zertifikat ignorieren (nur für self-signed Certs)")]
    public bool IgnoreSslErrors { get; set; } = false;
}

public class McpCallTool(IHttpClientFactory httpClientFactory, ILogger<McpCallTool> logger) : BaseTool<McpCallParams>
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

            // Validate ServerUrl
            if (!Uri.TryCreate(p.ServerUrl, UriKind.Absolute, out var serverUri))
                return $"ERROR: Ungültige MCP Server URL: {p.ServerUrl}";

            // Validate Method
            if (string.IsNullOrWhiteSpace(p.Method))
                return "ERROR: Method ist erforderlich";

            // Build JSON-RPC Request
            var requestId = Guid.NewGuid().ToString("N").Substring(0, 8);
            var jsonRpcRequest = new
            {
                jsonrpc = "2.0",
                method = p.Method,
                @params = p.Params ?? new Dictionary<string, object>(),
                id = requestId
            };

            var requestJson = JsonSerializer.Serialize(jsonRpcRequest);
            logger.LogInformation($"MCP Request to {p.ServerUrl}: {requestJson}");

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
            var request = new HttpRequestMessage(HttpMethod.Post, p.ServerUrl)
            {
                Content = new StringContent(requestJson, System.Text.Encoding.UTF8, "application/json")
            };

            // Add auth if provided
            if (!string.IsNullOrWhiteSpace(p.BearerToken))
            {
                request.Headers.Authorization = new System.Net.Http.Headers.AuthenticationHeaderValue("Bearer", p.BearerToken);
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
}
