using BlazorClaw.Core.DTOs;
using BlazorClaw.Core.Sessions;
using BlazorClaw.Core.Providers;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using System.Security.Claims;

namespace BlazorClaw.Server.Controllers;

[Authorize]
[ApiController]
[Route("v1")]
[IgnoreAntiforgeryToken]
public class OpenAiController(ISessionManager sessionManager) : ControllerBase
{
    [HttpPost("chat/completions")]
    public async Task<IActionResult> ChatCompletions([FromBody] ChatCompletionRequest request)
    {
        var userIdString = User.FindFirst(ClaimTypes.NameIdentifier)?.Value;
        if (string.IsNullOrEmpty(userIdString)) return Unauthorized();

        var userId = Guid.Parse(userIdString);

        // 1. Session via SessionManager laden/erstellen
        var sessionState = await sessionManager.GetOrCreateSessionAsync(userId, request.Model);

        // 2. Nur die letzte Nachricht verarbeiten
        var lastUserMessage = request.Messages.LastOrDefault(m => m.Role == "user");
        if (lastUserMessage != null)
        {
            await sessionManager.AppendMessageAsync(userId, lastUserMessage);
        }

        // 3. LLM Dispatcher ausführen
        var responses = new List<ChatMessage>();
        await foreach (var response in sessionManager.DispatchToLLMAsync(sessionState))
        {
            responses.Add(response);
        }

        // 4. Rückgabe der Assistant-Antworten
        return Ok(new ChatCompletionResponse 
        { 
            Choices = responses.Select(r => new ChatChoice { Message = r }).ToList() 
        });
    }
}
