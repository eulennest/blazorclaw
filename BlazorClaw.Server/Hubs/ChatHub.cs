using BlazorClaw.Core.DTOs;
using BlazorClaw.Core.Sessions;
using Microsoft.AspNetCore.SignalR;
using System.Security.Claims;
using static BlazorClaw.Server.Controllers.OpenAiController;

namespace BlazorClaw.Server.Hubs;

public class ChatHub : Hub, IChannelBot
{
    private readonly IMessageDispatcher md;
    private readonly ISessionManager sessionManager;
    private readonly ILogger<ChatHub> logger;

    public ChatHub(IMessageDispatcher md, ISessionManager sessionManager, ILogger<ChatHub> logger)
    {
        this.md = md;
        this.sessionManager = sessionManager;
        this.logger = logger;
        md.Register(this);
    }

    public string ChannelProvider => "WebChat";

    public event Func<IChannelSession, object, Task>? MessageReceived;

    public async Task SendMessage(Guid sessionId, string message)
    {
        var userIdString = Context.User?.FindFirst(ClaimTypes.NameIdentifier)?.Value ?? Guid.Empty.ToString();
        logger.LogInformation("Received message for session {SessionId} from user {userIdString}", sessionId, userIdString);

        var canid = new ChannelSession(this, sessionId.ToString(), userIdString)
        {
            SessionId = sessionId
        };
        await SendChannelAsync(canid, ChatMessage.Build(message));

        try
        {
            await OnMessageReceivedAsync(canid, message);
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Error: {Message}", ex);
            await SendUserAsync(canid, ChatMessage.Build(ex));
        }
    }

    public async Task LoadHistory(Guid sessionId)
    {
        try
        {
            var state = await sessionManager.GetSessionAsync(sessionId);
            if (state != null)
            {
                await Groups.AddToGroupAsync(Context.ConnectionId, sessionId.ToString());
                await Clients.Caller.SendAsync("HistoryLoaded", sessionId, state.MessageHistory);
            }
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Error loading history for session {SessionId}", sessionId);
        }
    }

    public Task SendChannelAsync(IChannelSession channelId, ChatMessage message, CancellationToken cancellationToken = default)
    {
        return Clients.Group(channelId.ChannelId).SendAsync("ReceiveMessage", channelId.SessionId, message, cancellationToken: cancellationToken);
    }
    public Task SendUserAsync(IChannelSession channelId, ChatMessage message, CancellationToken cancellationToken = default)
    {
        return Clients.Caller.SendAsync("ReceiveMessage", channelId.SessionId, message, cancellationToken: cancellationToken);
    }

    public Task OnMessageReceivedAsync(IChannelSession channelSession, object message)
    {
        return MessageReceived?.Invoke(channelSession, message) ?? Task.CompletedTask;
    }
}
