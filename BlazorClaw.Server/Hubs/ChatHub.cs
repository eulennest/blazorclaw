using BlazorClaw.Core.DTOs;
using BlazorClaw.Core.Sessions;
using Microsoft.AspNetCore.SignalR;
using Microsoft.Extensions.AI;
using System.Security.Claims;

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
        await SendChannelAsync(canid, new(ChatRole.User, message));

        try
        {
            await OnMessageReceivedAsync(canid, message);
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Error: {Message}", ex);
            await SendUserAsync(canid, new(new("error"), ex.Message));
        }
    }

    public async Task LoadHistory(Guid sessionId)
    {
        try
        {
            var state = await sessionManager.GetSessionAsync(sessionId);
            if (state != null)
            {
                var msgs = state.MessageHistory;
                await Groups.AddToGroupAsync(Context.ConnectionId, sessionId.ToString());
                await Clients.Caller.SendAsync("HistoryLoaded", sessionId, msgs);
            }
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Error loading history for session {SessionId}", sessionId);
        }
    }

    public Task SendChannelAsync(IChannelSession channelId, ChatMessage message, CancellationToken cancellationToken = default)
    {
        logger.LogInformation("Sending message to channel {ChannelId} in session {SessionId}", channelId.ChannelId, channelId.SessionId);
        return Clients.Group(channelId.ChannelId).SendAsync("ReceiveMessage", channelId.SessionId, message, cancellationToken: cancellationToken);
    }
    public Task SendUserAsync(IChannelSession channelId, ChatMessage message, CancellationToken cancellationToken = default)
    {
        logger.LogInformation("Sending message to user {UserId} in session {SessionId}", channelId.SenderId, channelId.SessionId);
        return Clients.Caller.SendAsync("ReceiveMessage", channelId.SessionId, message, cancellationToken: cancellationToken);
    }

    public Task OnMessageReceivedAsync(IChannelSession channelSession, object message)
    {
        return MessageReceived?.Invoke(channelSession, message) ?? Task.CompletedTask;
    }
}
