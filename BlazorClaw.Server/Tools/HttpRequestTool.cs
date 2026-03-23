using BlazorClaw.Core.Commands;
using BlazorClaw.Core.Services;
using BlazorClaw.Core.Tools;
using System.ComponentModel;
using System.ComponentModel.DataAnnotations;
using System.Text.Json;

namespace BlazorClaw.Server.Tools;

public class HttpRequestParams : BaseToolParams
{
    [Description("HTTP Method (GET, POST, PUT, DELETE, PATCH)")]
    [Required]
    public string Method { get; set; } = "GET";

    [Description("URL (z.B. https://example.com/api/endpoint). Use @VAR_NAME for variable substitution")]
    [Required]
    public string Url { get; set; } = string.Empty;

    [Description("Request Body (JSON oder Text, optional). Use @VAR_NAME for variable substitution. Beispiel: {\"entity_id\": \"@ENTITY_ID\"}")]
    public string? Body { get; set; }

    [Description("Bearer Token für Authorization Header (optional). IMMER @VAR_NAME mit VariableMappings nutzen! Beispiel: @HA_TOKEN + VariableMappings {\"HA_TOKEN\": \"vault:Home_Assistant_Token\"} - wird automatisch aus Vault geholt.")]
    public string? BearerToken { get; set; }

    [Description("Custom Headers als JSON Object. Use @VAR_NAME for variable substitution. Beispiel: {\"X-Custom-Header\": \"@CUSTOM_VALUE\"}")]
    public string? Headers { get; set; }

    [Description("SSL-Zertifikat ignorieren (nur für self-signed Certs, z.B. qdha.duckdns.org)")]
    public bool IgnoreSslErrors { get; set; } = false;

    [Description("Timeout in Sekunden")]
    [Range(1, 300)]
    public int TimeoutSeconds { get; set; } = 30;
}

public class HttpRequestTool : BaseTool<HttpRequestParams>
{
    private readonly IHttpClientFactory _httpClientFactory;
    private readonly ILogger<HttpRequestTool> _logger;

    public override string Name => "http_request";
    public override string Description => """
        Führt HTTP Requests durch (GET, POST, PUT, DELETE, PATCH).
        Unterstützt Bearer Token, Custom Headers, JSON Body und SSL-Verifikation.
        
        WICHTIG: Nutze @VAR_NAME für Tokens/Secrets + VariableMappings!
        Tokens gehören NIEMALS im Klartext in die Parameter!
        
        Vault-Items werden automatisch via vault_get geholt (vault:ItemName).
        
        Beispiel (Home Assistant Light ausschalten mit Token aus Vault):
        {
          "method": "POST",
          "url": "https://qdha.duckdns.org:8123/api/services/light/turn_off",
          "bearerToken": "@HA_TOKEN",
          "body": "{"entity_id": "@ENTITY_ID"}",
          "variableMappings": {
            "HA_TOKEN": "vault:Home_Assistant_Token",
            "ENTITY_ID": "env:LIGHT_ENTITY_ID"
          },
          "ignoreSslErrors": true
        }
        """;


    public HttpRequestTool(IHttpClientFactory httpClientFactory, ILogger<HttpRequestTool> logger)
    {
        _httpClientFactory = httpClientFactory;
        _logger = logger;
    }

    protected override async Task<string> ExecuteInternalAsync(HttpRequestParams p, MessageContext context)
    {
        try
        {
            // Validate URL
            if (!Uri.TryCreate(p.Url, UriKind.Absolute, out var uri))
                return $"ERROR: Ungültige URL: {p.Url}";

            // Validate HTTP Method
            var method = p.Method.ToUpperInvariant();
            if (!new[] { "GET", "POST", "PUT", "DELETE", "PATCH", "HEAD", "OPTIONS" }.Contains(method))
                return $"ERROR: Ungültige HTTP Method: {p.Method}";

            // Create HTTP client
            var clientName = p.IgnoreSslErrors ? "InsecureHttpClient" : "HttpClient";
            var client = _httpClientFactory.CreateClient(clientName);
            client.Timeout = TimeSpan.FromSeconds(p.TimeoutSeconds);

            // Create request
            var request = new HttpRequestMessage(new HttpMethod(method), uri);

            // Add Bearer Token
            if (!string.IsNullOrWhiteSpace(p.BearerToken))
            {
                request.Headers.Authorization = new System.Net.Http.Headers.AuthenticationHeaderValue("Bearer", p.BearerToken);
            }

            // Add custom headers
            if (!string.IsNullOrWhiteSpace(p.Headers))
            {
                try
                {
                    var headers = JsonSerializer.Deserialize<Dictionary<string, string>>(p.Headers);
                    if (headers != null)
                    {
                        foreach (var (key, value) in headers)
                        {
                            request.Headers.Add(key, value);
                        }
                    }
                }
                catch (Exception ex)
                {
                    _logger.LogWarning(ex, "Failed to parse custom headers");
                    return $"ERROR: Ungültige Headers JSON: {ex.Message}";
                }
            }

            // Add body
            if (!string.IsNullOrWhiteSpace(p.Body))
            {
                request.Content = new StringContent(p.Body, System.Text.Encoding.UTF8, "application/json");
            }

            _logger.LogInformation("HTTP Request: {Method} {Url}", method, p.Url);

            // Send request
            var response = await client.SendAsync(request);

            // Read response body
            var responseBody = await response.Content.ReadAsStringAsync();

            // Format response
            var result = $"Status: {(int)response.StatusCode} {response.StatusCode}\n\n";

            // Try to format JSON if content is JSON
            if (response.Content.Headers.ContentType?.MediaType?.Contains("application/json") ?? false)
            {
                try
                {
                    var jsonDoc = JsonSerializer.Deserialize<JsonElement>(responseBody);
                    result += JsonSerializer.Serialize(jsonDoc, new JsonSerializerOptions { WriteIndented = true });
                }
                catch
                {
                    result += responseBody;
                }
            }
            else
            {
                result += responseBody;
            }

            _logger.LogInformation("HTTP Response: {Status}", response.StatusCode);

            return result;
        }
        catch (HttpRequestException ex)
        {
            _logger.LogError(ex, "HTTP Request failed");
            return $"ERROR: HTTP Request fehlgeschlagen: {ex.Message}";
        }
        catch (TaskCanceledException ex)
        {
            _logger.LogError(ex, "HTTP Request timeout");
            return $"ERROR: HTTP Request Timeout nach {p.TimeoutSeconds}s";
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Unexpected error in HTTP request");
            return $"ERROR: {ex.Message}";
        }
    }
}
