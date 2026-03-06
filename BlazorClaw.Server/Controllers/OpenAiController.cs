using Microsoft.AspNetCore.Mvc;
using System.Net.Http.Headers;
using Microsoft.AspNetCore.Authorization;

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
    public async Task<IActionResult> ChatCompletions([FromBody] object request)
    {
        var response = await _httpClient.PostAsJsonAsync("chat/completions", request);
        var content = await response.Content.ReadAsStringAsync();

        return Content(content, "application/json");
    }
}
