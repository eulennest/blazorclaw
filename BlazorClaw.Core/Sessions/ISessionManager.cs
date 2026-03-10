using BlazorClaw.Core.Commands;
using BlazorClaw.Core.DTOs;
using Microsoft.EntityFrameworkCore.Diagnostics;
using System.CommandLine;

namespace BlazorClaw.Core.Sessions;

public interface ISessionManager
{
    Task<ChatSessionState> GetOrCreateSessionAsync(Guid sessionId, string? model = null);
    Task<ChatSessionState?> GetSessionAsync(Guid sessionId);
    Task SaveSessionAsync(ChatSessionState sessionState, bool newVersion = false);
    Task SaveToDiskAsync(ChatSessionState sessionState, bool newVersion = false);
    Task AppendMessageAsync(Guid sessionId, ChatMessage message);
    IAsyncEnumerable<ChatMessage> DispatchToLLMAsync(ChatSessionState sess, MessageContext context);
    Task<object?> DispatchCommandAsync(string cmdline, MessageContext cmdContext, RootCommand rootCmd, ICommandProvider commandProvider);
}

public interface IChannelBot
{
    string ChannelProvider { get; }
    event Func<IChannelSession, object, Task>? MessageReceived; // channelId, userId, message
    Task SendMessageAsync(IChannelSession channelId, object message, CancellationToken cancellationToken = default);
}

public interface IChannelSession : IChannelBot
{
    string ChannelId { get; }
    string SenderId { get; }

    Task SendMessageAsync(object message, CancellationToken cancellationToken = default);
}

public class ChannelSession(IChannelBot bot, string channelId, string? senderId = null) : IChannelSession
{
    public string ChannelProvider => bot.ChannelProvider;
    public string ChannelId { get; } = channelId;
    public string SenderId { get; } = senderId ?? channelId;

    public event Func<IChannelSession, object, Task>? MessageReceived
    {
        add { bot.MessageReceived += value; }
        remove { bot.MessageReceived -= value; }
    }
    public Task SendMessageAsync(object message, CancellationToken cancellationToken = default)
    {
        return bot.SendMessageAsync(this, message, cancellationToken);
    }

    public Task SendMessageAsync(IChannelSession channelId, object message, CancellationToken cancellationToken = default)
    {
        return bot.SendMessageAsync(channelId, message, cancellationToken);
    }
}

public abstract class AbstractChannelBot(string channelProvider) : IChannelBot
{
    public virtual string ChannelProvider { get; protected set; } = channelProvider;

    public event Func<IChannelSession, object, Task>? MessageReceived;

    public abstract Task SendMessageAsync(IChannelSession channelId, object message, CancellationToken cancellationToken = default);

    public Task OnMessageReceivedAsync(IChannelSession channelSession, object message)
    {
        return MessageReceived?.Invoke(channelSession, message) ?? Task.CompletedTask;
    }
}

public interface IMessageDispatcher
{
    /// <summary>
    /// Gibt die SessionId zurück, die zu dieser User-Kanal-Kombination gehört.
    /// </summary>
    void Register(IChannelBot bot);
    void Unregister(IChannelBot bot);

    Task DispatchMessageAsync(IChannelSession channelSession, object message);
}
