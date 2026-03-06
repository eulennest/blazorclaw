using BlazorClaw.Core.DTOs;
using BlazorClaw.Core.Security;
using BlazorClaw.Core.Tools;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using System.Text.Json;

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
        if (!request.Messages.Any(m => m.Role == "system"))
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
            request.Tools ??= [];
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
        if (message.ToolCalls != null && message.ToolCalls.Count != 0)
        {
            request.Messages.Add(message); // Assistant Call

            foreach (var call in message.ToolCalls)
            {
                try
                {
                    var tool = _toolRegistry.GetTool(call.Function.Name) ?? throw new ToolNotFoundException(call.Function.Name);
                    var args = tool.BuidlArguments(call.Function.Arguments);

                    _policyProvider.BeforeTool(tool, args, context);
                    var result = await tool.ExecuteAsync(args, context);
                    result = _policyProvider.AfterTool(tool, args, result, context);

                    request.Messages.Add(new ChatMessage
                    {
                        Role = "tool",
                        Content = result,
                        ExtensionData = new Dictionary<string, object> { { "tool_call_id", call.Id } }
                    });
                }
                catch (Exception ex)
                {
                    request.Messages.Add(new ChatMessage
                    {
                        Role = "tool",
                        Content = ToolErrorHandler.ToProblemDetailsJson(ex, call.Function.Name),
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
