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
    private readonly IToolSecurityInjector _securityInjector;

    public OpenAiController(IHttpClientFactory httpClientFactory, IToolRegistry toolRegistry, IConfiguration configuration, IToolSecurityInjector securityInjector)
    {
        _httpClient = httpClientFactory.CreateClient("OpenRouter");
        _toolRegistry = toolRegistry;
        _configuration = configuration;
        _securityInjector = securityInjector;
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

        var tools = _toolRegistry.GetAllTools();
        if (tools.Any())
        {
            request.Tools ??= new List<ToolDefinition>();
            foreach (var tool in tools)
            {
                request.Tools.Add(new ToolDefinition { Function = tool.GetSchema() });
            }
        }

        // 2. OpenAI Request
        var response = await _httpClient.PostAsJsonAsync("chat/completions", request);
        var content = await response.Content.ReadFromJsonAsync<ChatCompletionResponse>();
        if (content == null || content.Choices.Count == 0) return BadRequest("Invalid response");

        // 3. Tool Handling Loop
        var message = content.Choices[0].Message;
        if (message.ToolCalls != null && message.ToolCalls.Any())
        {
            foreach (var call in message.ToolCalls)
            {
                var tool = _toolRegistry.GetTool(call.Function.Name);
                if (tool != null)
                {
                    // Erstelle context (SessionID etc aus Request/Auth)
                    var context = new ToolContext { 
                        SessionId = Guid.NewGuid(), // TODO: Aus Request/Header
                        ServiceProvider = HttpContext.RequestServices,
                        UserId = User.Identity?.Name ?? "anonymous"
                    };

                    // SECURITY: Before Tool Hook
                    _securityInjector.BeforeTool(tool, call.Function.Arguments, context);

                    var result = await tool.ExecuteAsync(call.Function.Arguments, context);

                    // SECURITY: After Tool Hook
                    _securityInjector.AfterTool(tool, call.Function.Arguments, result, context);
                    
                    // Füge Tool-Result zu History hinzu (vereinfacht)
                    request.Messages.Add(message); // Assistant Call
                    request.Messages.Add(new ChatMessage { 
                        Role = "tool", 
                        Content = result,
                        ExtensionData = new Dictionary<string, object> { { "tool_call_id", call.Id } }
                    });

                    // Rufe LLM erneut auf, damit es Tool-Result interpretiert
                    return await ChatCompletions(request);
                }
            }
        }

        return Ok(content);
    }
}
