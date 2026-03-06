using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Authorization;
using BlazorClaw.Core.DTOs;
using BlazorClaw.Core.Tools;

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

    public OpenAiController(IHttpClientFactory httpClientFactory, IToolRegistry toolRegistry, IConfiguration configuration)
    {
        _httpClient = httpClientFactory.CreateClient("OpenRouter");
        _toolRegistry = toolRegistry;
        _configuration = configuration;
    }

    [HttpPost("chat/completions")]
    public async Task<ActionResult<ChatCompletionResponse>> ChatCompletions([FromBody] ChatCompletionRequest request)
    {
        // 1. System Prompt hinzufügen
        var systemPrompt = _configuration["Llm:SystemPrompt"] ?? "Du bist ein hilfreicher KI-Assistent.";
        if (request.Messages.FirstOrDefault(m => m.Role == "system") == null)
        {
            request.Messages.Insert(0, new ChatMessage { Role = "system", Content = systemPrompt });
        }

        // 2. Tools hinzufügen
        var tools = _toolRegistry.GetAllTools();
        if (tools.Any())
        {
            request.Tools ??= new List<ToolDefinition>();
            foreach (var tool in tools)
            {
                request.Tools.Add(new ToolDefinition
                {
                    Function = tool.GetSchema()
                });
            }
        }

        var response = await _httpClient.PostAsJsonAsync("chat/completions", request);
        var content = await response.Content.ReadFromJsonAsync<ChatCompletionResponse>();

        if (content == null) return BadRequest("Invalid response from upstream");

        return Ok(content);
    }
}
