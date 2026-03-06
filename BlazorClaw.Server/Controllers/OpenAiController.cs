using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Authorization;
using BlazorClaw.Core.DTOs;

namespace BlazorClaw.Server.Controllers;

[Authorize]
[ApiController]
[Route("v1")]
[IgnoreAntiforgeryToken]
public class OpenAiController : ControllerBase
{
    private readonly HttpClient _httpClient;

    public OpenAiController(IHttpClientFactory httpClientFactory)
    {
        _httpClient = httpClientFactory.CreateClient("OpenRouter");
    }

    [HttpPost("chat/completions")]
    public async Task<ActionResult<ChatCompletionResponse>> ChatCompletions([FromBody] ChatCompletionRequest request)
    {
        var response = await _httpClient.PostAsJsonAsync("chat/completions", request);
        var content = await response.Content.ReadFromJsonAsync<ChatCompletionResponse>();

        if (content == null) return BadRequest("Invalid response from upstream");

        return Ok(content);
    }
}
