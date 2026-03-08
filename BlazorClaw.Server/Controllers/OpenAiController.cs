using BlazorClaw.Core.DTOs;
using BlazorClaw.Core.Sessions;
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
    public async Task<ChatCompletionResponse> ChatCompletions([FromBody] ChatCompletionRequest request)
    {
        var resp = new ChatCompletionResponse();
        try
        {
            var userIdString = User.FindFirst(ClaimTypes.NameIdentifier)?.Value;
            if (string.IsNullOrEmpty(userIdString)) throw new UnauthorizedAccessException("User not logged in");

            var userId = Guid.Parse(userIdString);

            // 1. Session via SessionManager laden/erstellen
            var sessionState = await sessionManager.GetOrCreateSessionAsync(userId, request.Model);
            if (!string.IsNullOrWhiteSpace(request.Model))
            {
                sessionState.Session.CurrentModel = request.Model;
            }
            // 2. Nur die letzte Nachricht verarbeiten
            var lastUserMessage = request.Messages.LastOrDefault(m => m.Role == "user");
            if (lastUserMessage != null)
            {
                await sessionManager.AppendMessageAsync(userId, lastUserMessage);
            }

            // 3. LLM Dispatcher ausführen

            await foreach (var item in sessionManager.DispatchToLLMAsync(sessionState))
            {
                resp.Choices ??= [];
                resp.Choices.Add(new ChatChoice { Message = item });
            }
        }
        catch (Exception ex)
        {
            resp.Error = new ApiError { Message = $"Fehler: {ex.Message}" };
        }
        // 4. Rückgabe der Assistant-Antworten
        return resp;
    }
}
