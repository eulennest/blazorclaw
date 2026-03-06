using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Authorization;
using BlazorClaw.Core.DTOs;
using BlazorClaw.Core.Tools;
using BlazorClaw.Core.Security;

namespace BlazorClaw.Server.Controllers;

[Authorize]
[ApiController]
[Route("v1")]
[IgnoreAntiforgeryToken]
public class OpenAiController : ControllerBase
{
    private readonly HttpClient _httpClient;
    private readonly IToolRegistry _toolRegistry;
    private readonly IConfiguration _configuration;
    private readonly IToolPolicyProvider _policyProvider;

    public OpenAiController(IHttpClientFactory httpClientFactory, IToolRegistry toolRegistry, IConfiguration configuration, IToolPolicyProvider policyProvider)
    {
        _httpClient = httpClientFactory.CreateClient("OpenRouter");
        _toolRegistry = toolRegistry;
        _configuration = configuration;
        _policyProvider = policyProvider;
    }

    [HttpPost("chat/completions")]
    public async Task<ActionResult<ChatCompletionResponse>> ChatCompletions([FromBody] ChatCompletionRequest request)
    {
        // 1. System Prompt nur beim ersten Aufruf
        var systemPrompt = _configuration["Llm:SystemPrompt"] ?? "Du bist ein hilfreicher KI-Assistent.";
        if (request.Messages.Count(m => m.Role == "system") == 0)
        {
            request.Messages.Insert(0, new ChatMessage { Role = "system", Content = systemPrompt });
        }

        // Context für Security/Policies
        var context = new ToolContext { 
            SessionId = Guid.NewGuid(), // TODO: Aus Request/Header
            ServiceProvider = HttpContext.RequestServices,
            UserId = User.Identity?.Name ?? "anonymous",
            HttpContext = HttpContext
        };

        // 2. Tools filtern und hinzufügen
        var tools = _policyProvider.FilterTools(_toolRegistry.GetAllTools(), context);
        if (tools.Any())
        {
            request.Tools ??= new List<ToolDefinition>();
            foreach (var tool in tools)
            {
                request.Tools.Add(new ToolDefinition { Function = tool.GetSchema() });
            }
        }

        // 3. OpenAI Request
        var response = await _httpClient.PostAsJsonAsync("chat/completions", request);
        var content = await response.Content.ReadFromJsonAsync<ChatCompletionResponse>();
        if (content == null || content.Choices.Count == 0) return BadRequest("Invalid response");

        // 4. Tool Handling Loop
        var message = content.Choices[0].Message;
        if (message.ToolCalls != null && message.ToolCalls.Any())
        {
            request.Messages.Add(message); // Assistant Call

            foreach (var call in message.ToolCalls)
            {
                try
                {
                    var tool = _toolRegistry.GetTool(call.Function.Name);
                    if (tool == null) throw new ToolNotFoundException(call.Function.Name);

                    _policyProvider.BeforeTool(tool, call.Function.Arguments, context);
                    var result = await tool.ExecuteAsync(call.Function.Arguments, context);
                    result = _policyProvider.AfterTool(tool, call.Function.Arguments, result, context);

                    request.Messages.Add(new ChatMessage
                    {
                        Role = "tool",
                        Content = result,
                        ExtensionData = new Dictionary<string, object> { { "tool_call_id", call.Id } }
                    });
                }
                catch (Exception ex)
                {
                    int status = ex is ToolNotFoundException ? 404 : 500;
                    request.Messages.Add(new ChatMessage
                    {
                        Role = "tool",
                        Content = ToolErrorHandler.ToProblemDetailsJson(ex, call.Function.Name, status),
                        ExtensionData = new Dictionary<string, object> { { "tool_call_id", call.Id } }
                    });
                }
            }

            // Rekursion nach der gesamten Tool-Loop
            return await ChatCompletions(request);
        }

        return Ok(content);
    }
}
