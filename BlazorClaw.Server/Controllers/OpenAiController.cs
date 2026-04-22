using BlazorClaw.Core.DTOs;
using BlazorClaw.Core.Sessions;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.AI;
using System.Security.Claims;

namespace BlazorClaw.Server.Controllers;

[Authorize]
[ApiController]
[Route("v1")]
[IgnoreAntiforgeryToken]
public class OpenAiController(IMessageDispatcher md, ILogger<OpenAiController> logger) : ControllerBase
{
    [HttpPost("chat/completions")]
    public async Task<ChatCompletionResponse> ChatCompletions([FromBody] ChatCompletionRequest request)
    {
        var resp = new ChatCompletionResponse();

        var userIdString = User.FindFirst(ClaimTypes.NameIdentifier)?.Value;
        if (string.IsNullOrEmpty(userIdString)) throw new UnauthorizedAccessException("User not logged in");
        var bot = new WebChatChannelBot(userIdString);

        try
        {
            md.Register(bot);

            // 2. Nur die letzte Nachricht verarbeiten
            var lastUserMessage = request.Messages.LastOrDefault(m => m.Role == "user")
                ?? throw new ArgumentException("Keine Benutzernachricht in der Anfrage gefunden");
            var text = Convert.ToString(lastUserMessage.Content)!.Trim();

            bot.OnMessageReceived(bot, text);

            foreach (var item in bot.ReceivedMessages)
            {
                resp.Choices ??= [];
                resp.Choices.Add(new ChatChoice { Message = new DtoChatMessage() { Content = item } });
            }
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Error: {Message}", ex.Message);
            resp.Error = new ApiError { Message = $"Fehler: {ex.Message}" };
        }
        finally
        {
            md.Unregister(bot);
        }
        return resp;
    }

    public class WebChatChannelBot(string channelId) : AbstractChannelBot("WebChat"), IChannelSession
    {
        public List<string> ReceivedMessages { get; } = [];

        public string ChannelId { get; } = channelId;
        public string SenderId { get; set; } = channelId;
        public Guid SessionId { get; set; }

        public override Task StartAsync(CancellationToken cancellationToken = default)
        {
            return Task.CompletedTask;
        }

        public override Task StopAsync(CancellationToken cancellationToken = default)
        {
            return Task.CompletedTask;
        }

        public override Task SendChannelAsync(IChannelSession channelId, ChatMessage message, CancellationToken cancellationToken = default)
        {
            var text = message.Text?.Trim();
            if (!string.IsNullOrWhiteSpace(text))
            {
                ReceivedMessages.Add(text);
            }
            return Task.CompletedTask;
        }

        public Task SendChannelAsync(ChatMessage message, CancellationToken cancellationToken = default)
        {
            return SendChannelAsync(this, message, cancellationToken);
        }
        public override Task SendUserAsync(IChannelSession channelId, ChatMessage message, CancellationToken cancellationToken = default)
        {
            var text = message.Text?.Trim();
            if (!string.IsNullOrWhiteSpace(text))
            {
                ReceivedMessages.Add(text);
            }
            return Task.CompletedTask;
        }

        public Task SendUserAsync(ChatMessage message, CancellationToken cancellationToken = default)
        {
            return SendUserAsync(this, message, cancellationToken);
        }
    }
}