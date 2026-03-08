using BlazorClaw.Core.DTOs;
using BlazorClaw.Core.Security;
using BlazorClaw.Core.Tools;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace BlazorClaw.Server.Controllers;

[Authorize]
[ApiController]
[Route("v1")]
[IgnoreAntiforgeryToken]
public class OpenAiController(IHttpClientFactory httpClientFactory, IToolRegistry toolRegistry, IConfiguration configuration, IToolPolicyProvider policyProvider) : ControllerBase
{
    private readonly HttpClient _httpClient = httpClientFactory.CreateClient("OpenRouter");

    [HttpPost("chat/completions")]
    public async Task<IActionResult> ChatCompletions([FromBody] ChatCompletionRequest request)
    {
        // 1. System Prompt nur beim ersten Aufruf
        var systemPrompt = configuration["Llm:SystemPrompt"] ?? "Du bist ein hilfreicher KI-Assistent.";
        if (!request.Messages.Any(m => m.Role == "system"))
        {
            request.Messages.Insert(0, new ChatMessage { Role = "system", Content = systemPrompt });
        }

        // Context für Security/Policies
        var context = new ToolContext
        {
            SessionId = Guid.NewGuid(), // TODO: Aus Request/Header
            ServiceProvider = HttpContext.RequestServices,
            UserId = User.Identity?.Name ?? "anonymous",
            HttpContext = HttpContext
        };

        // 2. Tools filtern und hinzufügen
        var tools = policyProvider.FilterTools(toolRegistry.GetAllTools(), context);
        if (tools.Any())
        {
            request.Tools ??= [];
            foreach (var tool in tools)
            {
                request.Tools.Add(
                    new()
                    {
                        Function = new() { Name = tool.Name, Description = tool.Description, Parameters = tool.GetSchema() }
                    });
            }
        }


        // 3. OpenAI Request
        var response = await _httpClient.PostAsJsonAsync("chat/completions", request);
        var content = await response.Content.ReadFromJsonAsync<ChatCompletionResponse>();
        if (content == null) return BadRequest("Invalid response");

        if (content.Choices != null)
        {
            var mustLoop = false;
            // 4. Tool Handling Loop
            foreach (var choice in content.Choices)
            {
                var message = choice.Message;
                if (message.ToolCalls != null && message.ToolCalls.Count != 0)
                {
                    mustLoop = true;
                    request.Messages.Add(message); // Assistant Call

                    foreach (var call in message.ToolCalls)
                    {
                        try
                        {
                            var tool = toolRegistry.GetTool(call.Function.Name) ?? throw new ToolNotFoundException(call.Function.Name);
                            var args = tool.BuidlArguments(call.Function.Arguments);

                            policyProvider.BeforeTool(tool, args, context);
                            var result = await tool.ExecuteAsync(args, context);
                            result = policyProvider.AfterTool(tool, args, result, context);

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
                }
            }
            // Rekursion nach der gesamten Tool-Loop
            if (mustLoop) return await ChatCompletions(request);
        }
        return Ok(content);
    }
}
