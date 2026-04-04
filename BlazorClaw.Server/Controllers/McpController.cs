using BlazorClaw.Core.Commands;
using BlazorClaw.Core.DTOs;
using BlazorClaw.Core.Models;
using BlazorClaw.Core.Sessions;
using BlazorClaw.Core.Tools;
using BlazorClaw.Core.Utils;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using System.ComponentModel;
using System.Security.Claims;
using System.Text.Json;
using System.Text.Json.Serialization;

namespace BlazorClaw.Server.Controllers;

/// <summary>
/// MCP (Model Context Protocol) Server endpoint.
/// Exposes BlazorClaw tools as JSON-RPC 2.0 methods.
/// Auth: Bearer token with session_id
/// </summary>
[Authorize]
[ApiController]
[Route("mcp")]
[IgnoreAntiforgeryToken]
public class McpController(IToolRegistry toolRegistry, IMessageDispatcher messageDispatcher, ILogger<McpController> logger)
    : ControllerBase
{
    private const string MCP_VERSION = "2024-11-05";

    /// <summary>
    /// Initialize MCP connection and list available tools.
    /// 
    /// Request: POST /mcp/initialize
    /// Header: Authorization: Bearer <session_id>
    /// 
    /// Response:
    /// {
    ///   "jsonrpc": "2.0",
    ///   "result": {
    ///     "protocolVersion": "2024-11-05",
    ///     "capabilities": {...},
    ///     "serverInfo": {...},
    ///     "tools": [
    ///       { "name": "web_search", "description": "...", "inputSchema": {...} }
    ///     ]
    ///   },
    ///   "id": "init"
    /// }
    /// </summary>
    [HttpPost("initialize")]
    public IActionResult Initialize()
    {
        try
        {
            var sessionId = ValidateBearerToken();
            if (sessionId == null)
                return Unauthorized(JsonRpcError("Invalid or missing Bearer token", -32600));

            var tools = toolRegistry.GetAllTools().ToList();

            var toolSchemas = tools.Select(t => new
            {
                name = t.Name,
                description = t.Description,
                inputSchema = GetInputSchema(t)
            }).ToList();

            var response = new
            {
                jsonrpc = "2.0",
                result = new
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
                    tools = toolSchemas
                },
                id = "init"
            };

            logger.LogInformation("MCP initialize: {SessionId}, tools: {ToolCount}", sessionId, tools.Count);
            return Ok(response);
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "MCP initialize failed");
            return BadRequest(JsonRpcError(ex.Message, -32603));
        }
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
    [HttpPost("call")]
    public async Task<IActionResult> Call([FromBody] JsonElement request)
    {
        try
        {
            var sessionId = ValidateBearerToken();
            if (sessionId == null)
                return Unauthorized(JsonRpcError("Invalid or missing Bearer token", -32600));

            // Parse JSON-RPC request
            if (!request.TryGetProperty("jsonrpc", out var jsonrpcVer) || jsonrpcVer.GetString() != "2.0")
                return BadRequest(JsonRpcError("Invalid JSON-RPC version", -32700));

            if (!request.TryGetProperty("method", out var methodElement))
                return BadRequest(JsonRpcError("Missing method", -32602));

            var method = methodElement.GetString();
            if (string.IsNullOrWhiteSpace(method))
                return BadRequest(JsonRpcError("Method must not be empty", -32602));

            var requestId = request.TryGetProperty("id", out var idElement)
                ? idElement.GetString() ?? idElement.GetInt32().ToString()
                : "1";

            request.TryGetProperty("params", out var paramsElement);

            // Get tool
            var tool = toolRegistry.GetTool(method);
            if (tool == null)
                return BadRequest(JsonRpcError($"Tool '{method}' not found", -32601, requestId));

            // Create message context (MCP session)
            var mcpBot = new McpSessionBot(sessionId);
            messageDispatcher.Register(mcpBot);

            try
            {
                // Convert params to JSON string for tool deserialization
                var paramsJson = paramsElement.ValueKind != JsonValueKind.Undefined
                    ? paramsElement.GetRawText()
                    : "{}";

                // Build MessageContext
                var fakeChatSession = new ChatSession
                {
                    Id = Guid.NewGuid(),
                    Title = $"MCP:{method}",
                    UserId = Guid.Empty,
                    CreatedAt = DateTime.UtcNow
                };

                var context = new MessageContext
                {
                    Session = new ChatSessionState { Session = fakeChatSession },
                    User = null,
                    Message = method
                };

                // Deserialize tool parameters using tool's BuildArguments
                var toolArgs = tool.BuildArguments(paramsJson);

                // Execute tool
                var result = await tool.ExecuteAsync(toolArgs, context);

                var response = new
                {
                    jsonrpc = "2.0",
                    result = result,
                    id = requestId
                };

                logger.LogInformation("MCP call succeeded: {SessionId}:{Method}", sessionId, method);
                return Ok(response);
            }
            finally
            {
                messageDispatcher.Unregister(mcpBot);
            }
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
    private string? ValidateBearerToken()
    {
        var authHeader = Request.Headers.Authorization.FirstOrDefault();
        if (string.IsNullOrWhiteSpace(authHeader) || !authHeader.StartsWith("Bearer "))
            return null;

        var token = authHeader.Substring("Bearer ".Length).Trim();
        return string.IsNullOrWhiteSpace(token) ? null : token;
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

/// <summary>
/// Session representation for MCP calls (implements IChannelSession).
/// Messages are not sent anywhere (MCP is bidirectional HTTP request/response).
/// </summary>
public class McpSessionBot(string sessionId) : AbstractChannelBot("MCP"), IChannelSession
{
    public string SessionId { get; set; } = Guid.NewGuid();
    public string ChannelId => "mcp";
    public string SenderId => sessionId;
    Guid IChannelSession.SessionId { get; set; } = Guid.NewGuid();

    public override Task SendChannelAsync(IChannelSession channelId, ChatMessage message, CancellationToken cancellationToken = default)
    {
        // MCP doesn't use channel messaging; responses go via HTTP response
        return Task.CompletedTask;
    }

    public override Task SendUserAsync(IChannelSession channelId, ChatMessage message, CancellationToken cancellationToken = default)
    {
        // MCP doesn't use user messaging; responses go via HTTP response
        return Task.CompletedTask;
    }
}
