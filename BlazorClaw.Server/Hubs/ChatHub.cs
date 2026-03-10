using BlazorClaw.Core.Commands;
using BlazorClaw.Core.DTOs;
using BlazorClaw.Core.Sessions;
using Microsoft.AspNetCore.SignalR;
using System.Security.Claims;
using Telegram.Bot.Types;
using static BlazorClaw.Server.Controllers.OpenAiController;

namespace BlazorClaw.Server.Hubs;

public class ChatHub(IMessageDispatcher md, ISessionManager sessionManager, ILogger<ChatHub> logger) : Hub
{
    public async Task SendMessage(Guid sessionId, string message)
    {
        var userIdString = Context.User?.FindFirst(ClaimTypes.NameIdentifier)?.Value ?? "anonymous";
        logger.LogInformation("Received message for session {SessionId} from user {userIdString}", sessionId, userIdString);


        var state = await sessionManager.GetOrCreateSessionAsync(sessionId);
        var bot = new WebChatChannelBot(state.Session!.Id.ToString())
        {
            SenderId = userIdString
        };
        try
        {
            md.Register(bot);
            await bot.OnMessageReceivedAsync(bot, message);

            foreach (var item in bot.ReceivedMessages)
            {
                await Clients.Caller.SendAsync("ReceiveMessage", sessionId, item);
            }
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Error: {Message}", ex);
            await Clients.Caller.SendAsync("Error", sessionId, $"Error: {ex}");
        }
        finally
        {
            md.Unregister(bot);
        }
    }

    public async Task LoadHistory(Guid sessionId)
    {
        try
        {
            var state = await sessionManager.GetSessionAsync(sessionId);
            if (state != null)
            {
                await Clients.Caller.SendAsync("HistoryLoaded", sessionId, state.MessageHistory);
            }
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Error loading history for session {SessionId}", sessionId);
        }
    }
}
