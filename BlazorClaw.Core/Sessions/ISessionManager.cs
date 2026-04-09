using BlazorClaw.Core.Commands;
using BlazorClaw.Core.DTOs;
using System.CommandLine;

namespace BlazorClaw.Core.Sessions;

public interface ISessionManager
{
    Task<ChatSessionState> GetOrCreateSessionAsync(Guid sessionId, string? model = null, string? user = null);
    Task<ChatSessionState?> GetSessionAsync(Guid sessionId);
    Task SaveSessionAsync(ChatSessionState sessionState, bool newVersion = false);
    Task SaveToDiskAsync(ChatSessionState sessionState, bool newVersion = false);
    Task AppendMessageAsync(Guid sessionId, ChatMessage message);
    IAsyncEnumerable<ChatMessage> DispatchToLLMAsync(ChatSessionState sess, MessageContext context);
    Task<object?> DispatchCommandAsync(string cmdline, MessageContext cmdContext, RootCommand rootCmd, ICommandProvider commandProvider);
    Task DeleteSessionAsync(Guid sessionId);
}

public interface IChannelBot
{
    string ChannelProvider { get; }
    event Func<IChannelSession, object, Task>? MessageReceived; // channelId, userId, message
    Task SendChannelAsync(IChannelSession channelId, ChatMessage message, CancellationToken cancellationToken = default);
    Task SendUserAsync(IChannelSession channelId, ChatMessage message, CancellationToken cancellationToken = default);
}

public interface IChannelSession : IChannelBot
{
    string ChannelId { get; }
    string SenderId { get; }
    Guid SessionId { get; set; }

    Task SendChannelAsync(ChatMessage message, CancellationToken cancellationToken = default);
    Task SendUserAsync(ChatMessage message, CancellationToken cancellationToken = default);
}

public class ChannelSession(IChannelBot bot, string channelId, string? senderId = null) : IChannelSession
{
    public string ChannelProvider => bot.ChannelProvider;
    public string ChannelId { get; } = channelId;
    public string SenderId { get; } = senderId ?? channelId;
    public Guid SessionId { get; set; }

    public override string ToString()
    {
        return $"{ChannelProvider}:{ChannelId}, SenderId={SenderId}, SessionId={SessionId}";
    }


    public event Func<IChannelSession, object, Task>? MessageReceived
    {
        add { bot.MessageReceived += value; }
        remove { bot.MessageReceived -= value; }
    }
    public Task SendChannelAsync(ChatMessage message, CancellationToken cancellationToken = default)
    {
        return bot.SendChannelAsync(this, message, cancellationToken);
    }
    public Task SendUserAsync(ChatMessage message, CancellationToken cancellationToken = default)
    {
        return bot.SendUserAsync(this, message, cancellationToken);
    }
    public Task SendChannelAsync(IChannelSession channelId, ChatMessage message, CancellationToken cancellationToken = default)
    {
        return bot.SendChannelAsync(channelId, message, cancellationToken);
    }
    public Task SendUserAsync(IChannelSession channelId, ChatMessage message, CancellationToken cancellationToken = default)
    {
        return bot.SendUserAsync(channelId, message, cancellationToken);
    }
}

public abstract class AbstractChannelBot(string channelProvider) : IChannelBot
{
    public virtual string ChannelProvider { get; protected set; } = channelProvider;

    public event Func<IChannelSession, object, Task>? MessageReceived;

    public abstract Task SendChannelAsync(IChannelSession channelId, ChatMessage message, CancellationToken cancellationToken = default);
    public abstract Task SendUserAsync(IChannelSession channelId, ChatMessage message, CancellationToken cancellationToken = default);

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
