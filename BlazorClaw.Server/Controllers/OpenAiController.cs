using BlazorClaw.Core.Commands;
using BlazorClaw.Core.DTOs;
using BlazorClaw.Core.Sessions;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using System.CommandLine;
using System.Security.Claims;

namespace BlazorClaw.Server.Controllers;

[Authorize]
[ApiController]
[Route("v1")]
[IgnoreAntiforgeryToken]
public class OpenAiController(ISessionManager sessionManager, IServiceProvider serviceProvider, ILogger<OpenAiController> logger) : ControllerBase
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
            var lastUserMessage = request.Messages.LastOrDefault(m => m.Role == "user")
                ?? throw new ArgumentException("Keine Benutzernachricht in der Anfrage gefunden");

            var text = Convert.ToString(lastUserMessage.Content)?.Trim();

            if (text?.StartsWith('/') ?? false)
            {
                try
                {
                    var cmdContext = new CommandContext
                    {
                        UserId = userIdString,
                        ChannelProvider = "WebChat",
                        ChannelId = userIdString,
                        Session = sessionState.Session,
                        Provider = serviceProvider
                    };
                    var cmds = serviceProvider.GetRequiredService<ICommandProvider>();
                    RootCommand rootCommand = new("Commands for WebChat");
                    foreach (var cmd in cmds.GetCommands())
                    {
                        var command = cmd.GetCommand();
                        rootCommand.Add(command);
                    }
                    var ret = await sessionManager.DispatchCommandAsync(text, cmdContext, rootCommand, cmds);
                    if (ret != null)
                    {
                        var textRes = Convert.ToString(ret);
                        if (!string.IsNullOrWhiteSpace(textRes))
                        {
                            resp.Choices ??= [];
                            resp.Choices.Add(new ChatChoice { Message = new() { Content = textRes } });
                        }
                    }
                }
                catch (Exception ex)
                {
                    logger.LogError(ex, "Error processing command '{Command}' for WebChat - User ID {userIdString}: {Message}", text, userIdString, ex.Message);
                    var textRes = $"Error processing command: {ex.Message}";
                    resp.Choices ??= [];
                    resp.Choices.Add(new ChatChoice { Message = new() { Content = textRes } });
                }
            }
            else
            {
                await sessionManager.AppendMessageAsync(userId, lastUserMessage);

                // 3. LLM Dispatcher ausführen
                await foreach (var item in sessionManager.DispatchToLLMAsync(sessionState))
                {
                    resp.Choices ??= [];
                    resp.Choices.Add(new ChatChoice { Message = item });
                }
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
