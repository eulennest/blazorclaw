using BlazorClaw.Core.Commands;
using BlazorClaw.Core.Sessions;
using BlazorClaw.Core.Tools;
using Microsoft.AspNetCore.Mvc;
using System.Text.Json;

namespace BlazorClaw.Server.Controllers;

/// <summary>
/// MCP (Model Context Protocol) Server endpoint.
/// Exposes BlazorClaw tools as JSON-RPC 2.0 methods.
/// Auth: Bearer token with session_id
/// </summary>
[ApiController]
[Route("mcp")]
[IgnoreAntiforgeryToken]
public class McpController(ISessionManager sessionManager, ILogger<McpController> logger)
    : ControllerBase
{
    private const string MCP_VERSION = "2024-11-05";

    private object Initialize(MessageContext context)
    {
        return new
        {
            protocolVersion = MCP_VERSION,
            capabilities = new
            {
                tools = new { },
                resources = new { },
                logging = new { }
            },
            serverInfo = new
            {
                name = "BlazorClaw MCP Server",
                version = "1.0.0"
            },
        };
    }

    private object Tools_List(MessageContext context)
    {
        var toolRegistry = context.Provider.GetRequiredService<IToolRegistry>();
        var tools = toolRegistry.GetAllTools().ToList();

        var toolSchemas = tools.Select(t => new
        {
            name = t.Name,
            description = t.Description,
            inputSchema = GetInputSchema(t)
        }).ToList();

        return new
        {
            tools = toolSchemas
        };
    }

    /// <summary>
    /// Call a tool via MCP JSON-RPC.
    /// 
    /// Request: POST /mcp/call
    /// Header: Authorization: Bearer <session_id>
    /// Body: {
    ///   "jsonrpc": "2.0",
    ///   "method": "web_search",
    ///   "params": {"query": "example"},
    ///   "id": "123"
    /// }
    /// 
    /// Response: {
    ///   "jsonrpc": "2.0",
    ///   "result": "...",
    ///   "id": "123"
    /// }
    /// </summary>
    private static readonly Random random = new Random();
    [HttpGet()]
    public async Task Get()
    {
        Response.ContentType = "text/event-stream";
        await Response.Body.FlushAsync();

        while (!HttpContext.RequestAborted.IsCancellationRequested)
        {
        }
    }

    [HttpPost()]
    public async Task<IActionResult> Call([FromBody] JsonElement request)
    {
        try
        {
            var session = await ValidateBearerTokenAsync();
            if (session == null)
                return Unauthorized(JsonRpcError("Invalid or missing Bearer token", -32600));

            var context = new MessageContext
            {
                Session = session.Session,
                Provider = session.Services,
                UserId = session.Session.UserId
            };

            // Parse JSON-RPC request
            if (!request.TryGetProperty("jsonrpc", out var jsonrpcVer) || jsonrpcVer.GetString() != "2.0")
                return BadRequest(JsonRpcError("Invalid JSON-RPC version", -32700));

            if (!request.TryGetProperty("method", out var methodElement))
                return BadRequest(JsonRpcError("Missing method", -32602));

            var method = methodElement.GetString();
            if (string.IsNullOrWhiteSpace(method))
                return BadRequest(JsonRpcError("Method must not be empty", -32602));

            var requestId = "1";
            if (request.TryGetProperty("id", out var idElement))
            {
                requestId = idElement.ValueKind == System.Text.Json.JsonValueKind.String
                    ? idElement.GetString() ?? "1"
                    : idElement.ValueKind == System.Text.Json.JsonValueKind.Number
                        ? idElement.GetInt32().ToString()
                        : "1";
            }

            request.TryGetProperty("params", out var paramsElement);

            object? result = null;
            logger.LogInformation("MCP call started: {SessionId}:{Method}", session, method);

            if ("initialize".Equals(method))
            {
                result = Initialize(context);
            }
            else if ("notifications/initialized".Equals(method))
            {
                result = null;
            }
            else if ("tools/list".Equals(method))
            {
                result = Tools_List(context);
            }
            else if ("tools/call".Equals(method))
            {
                var toolRegistry = context.Provider.GetRequiredService<IToolRegistry>();
                // Get tool

                if (!paramsElement.TryGetProperty("name", out var toolElement))
                    return BadRequest(JsonRpcError("Missing 'name' parameter", -32602, requestId));
                var toolName = toolElement.GetString();
                if (string.IsNullOrWhiteSpace(toolName))
                    return BadRequest(JsonRpcError("'name' parameter must not be empty", -32602, requestId));

                var tool = toolRegistry.GetTool(method);
                if (tool == null)
                    return BadRequest(JsonRpcError($"Tool '{method}' not found", -32601, requestId));

                if (!paramsElement.TryGetProperty("arguments", out var argsElement))
                    return BadRequest(JsonRpcError("Missing 'arguments' parameter", -32602, requestId));

                // Convert params to JSON string for tool deserialization
                var paramsJson = argsElement.ValueKind != JsonValueKind.Undefined
                    ? paramsElement.GetRawText()
                    : "{}";

                // Deserialize tool parameters using tool's BuildArguments
                var toolArgs = tool.BuildArguments(paramsJson);

                // Execute tool
                result = new
                {
                    content = new object[] {
                        new
                        {
                            type = "text",
                            text = await tool.ExecuteAsync(toolArgs, context)
                        }
                    }
                };
            }
            else
            {
                return BadRequest(JsonRpcError($"Method '{method}' not found", -32601, requestId));
            }

            var response = new
            {
                jsonrpc = "2.0",
                result = result,
                id = requestId
            };

            logger.LogInformation("MCP call succeeded: {SessionId}:{Method}", session, method);
            return Ok(response);

        }
        catch (ArgumentException ex)
        {
            logger.LogWarning(ex, "MCP call argument error");
            return BadRequest(JsonRpcError(ex.Message, -32602));
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "MCP call failed");
            return BadRequest(JsonRpcError(ex.Message, -32603));
        }
    }

    // === Helpers ===

    /// <summary>
    /// Extract and validate Bearer token from Authorization header.
    /// Returns the token value (session_id) or null if invalid.
    /// </summary>
    private async Task<ChatSessionState?> ValidateBearerTokenAsync()
    {
        var authHeader = Request.Headers.Authorization.FirstOrDefault();
        if (string.IsNullOrWhiteSpace(authHeader) || !authHeader.StartsWith("Bearer ", StringComparison.CurrentCultureIgnoreCase))
            return null;
        var token = authHeader[7..].Trim();
        if (Guid.TryParse(token, out var sessId))
            return await sessionManager.GetSessionAsync(sessId);
        return null;
    }

    /// <summary>
    /// Build JSON-RPC error response.
    /// </summary>
    private object JsonRpcError(string message, int code, string? id = null)
    {
        return new
        {
            jsonrpc = "2.0",
            error = new
            {
                code = code,
                message = message
            },
            id = id
        };
    }

    /// <summary>
    /// Extract input schema from tool (for MCP tooling).
    /// Returns simplified JSON schema based on tool's BaseToolParams.
    /// </summary>
    private object GetInputSchema(ITool tool)
    {
        // TODO: Use SchemaGenerator to build full JSON schema
        // For now, return simple schema
        return new
        {
            type = "object",
            properties = new { },
            required = new string[] { }
        };
    }

    /// <summary>
    /// Convert JsonElement to Dictionary<string, object> for tool parameters.
    /// </summary>
    private Dictionary<string, object> JsonElementToDictionary(JsonElement element)
    {
        var dict = new Dictionary<string, object>();

        if (element.ValueKind == JsonValueKind.Object)
        {
            foreach (var property in element.EnumerateObject())
            {
                dict[property.Name] = JsonElementToObject(property.Value);
            }
        }

        return dict;
    }

    /// <summary>
    /// Recursively convert JsonElement to C# object.
    /// </summary>
    private object JsonElementToObject(JsonElement element)
    {
        return element.ValueKind switch
        {
            JsonValueKind.String => element.GetString() ?? "",
            JsonValueKind.Number => element.TryGetInt32(out int intVal)
                ? intVal
                : element.GetDouble(),
            JsonValueKind.True => true,
            JsonValueKind.False => false,
            JsonValueKind.Null => null!,
            JsonValueKind.Array => element.EnumerateArray()
                .Select(JsonElementToObject)
                .ToList(),
            JsonValueKind.Object => JsonElementToDictionary(element),
            _ => element.GetRawText()
        };
    }
}
