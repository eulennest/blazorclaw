using BlazorClaw.Core.Sessions;
using BlazorClaw.Server.Services;
using Microsoft.AspNetCore.SignalR;
using Microsoft.Extensions.AI;
using System.Collections.Concurrent;
using System.Security.Claims;

namespace BlazorClaw.Server.Hubs;

public class ChatHub : Hub
{
    private readonly WebChatChannelBot bot;
    private readonly ISessionManager sessionManager;
    private readonly ILogger<ChatHub> logger;

    // Simple in-memory mapping example
    private static readonly ConcurrentDictionary<string, HashSet<string>> _connectionGroups = new();

    public async Task JoinGroupAsync(string groupName)
    {
        await Groups.AddToGroupAsync(Context.ConnectionId, groupName);

        // Add to our manual tracking
        _connectionGroups.AddOrUpdate(Context.ConnectionId,
            [groupName],
            (key, set) => { set.Add(groupName); return set; });
    }

    public async Task RemoveGroupAsync(string groupName)
    {
        await Groups.RemoveFromGroupAsync(Context.ConnectionId, groupName);

        // Add to our manual tracking
        _connectionGroups.AddOrUpdate(Context.ConnectionId,
            [groupName],
            (key, set) => { set.Remove(groupName); return set; });
    }

    public override Task OnDisconnectedAsync(Exception? exception)
    {
        // Clean up tracking on disconnect
        _connectionGroups.TryRemove(Context.ConnectionId, out _);
        return base.OnDisconnectedAsync(exception);
    }

    // Method to get groups for a connection
    public static IEnumerable<string> GetGroupsForConnection(string connectionId)
    {
        return _connectionGroups.TryGetValue(connectionId, out var groups) ? groups : (IEnumerable<string>)[];
    }

    public ChatHub(WebChatChannelBot bot, ISessionManager sessionManager, ILogger<ChatHub> logger)
    {
        this.bot = bot;
        this.logger = logger;
        this.sessionManager = sessionManager;
    }

    public Task StartAsync(CancellationToken cancellationToken = default)
    {
        return Task.CompletedTask;
    }

    public Task StopAsync(CancellationToken cancellationToken = default)
    {
        return Task.CompletedTask;
    }

    public async Task SendMessage(Guid sessionId, string message)
    {
        var userIdString = Context.User?.FindFirst(ClaimTypes.NameIdentifier)?.Value ?? Guid.Empty.ToString();
        logger.LogInformation("Received message for session {SessionId} from user {userIdString}", sessionId, userIdString);

        var canid = new ChannelSession(bot, sessionId.ToString(), userIdString)
        {
            SessionId = sessionId
        };
        await bot.SendChannelAsync(canid, new(ChatRole.User, message) { CreatedAt = DateTimeOffset.UtcNow }).ConfigureAwait(false);

        try
        {
            await Task.WhenAny(bot.HandelIncomingAsync(sessionId, userIdString, message), Task.Delay(1000)).ConfigureAwait(false);
        }
        catch (TaskCanceledException)
        {
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Error: {Message}", ex);
            await bot.SendChannelAsync(canid, new(new("error"), ex.Message) { CreatedAt = DateTimeOffset.UtcNow }).ConfigureAwait(false);
        }
    }

    public async Task LoadHistory(Guid sessionId)
    {
        try
        {
            var grps = GetGroupsForConnection(Context.ConnectionId);
            foreach (var grp in grps)
            {
                await RemoveGroupAsync(grp);
            }
            await JoinGroupAsync(sessionId.ToString());

            var state = await sessionManager.GetSessionAsync(sessionId);
            if (state != null)
            {
                var msgs = state.MessageHistory;
                await Clients.Caller.SendAsync("HistoryLoaded", sessionId, msgs);
            }
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Error loading history for session {SessionId}", sessionId);
        }
    }
}
