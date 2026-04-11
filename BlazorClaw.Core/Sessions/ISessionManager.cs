using BlazorClaw.Core.Commands;
using Microsoft.Extensions.AI;
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
    Task StartAsync(CancellationToken cancellationToken = default);
    Task StopAsync(CancellationToken cancellationToken = default);

    string ChannelProvider { get; }
    event Func<IChannelSession, object, Task>? MessageReceived; // channelId, userId, message
    Task SendChannelAsync(IChannelSession channelId, ChatMessage message, CancellationToken cancellationToken = default);
    Task SendUserAsync(IChannelSession channelId, ChatMessage message, CancellationToken cancellationToken = default);
}
public interface IConfigure<in T>
{
    ValueTask<bool> ConfigureAsync(T config); 
}

public interface IKeyedConfigure<in T>
{
    ValueTask<bool> ConfigureAsync(string key, T config);
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
    public Task StartAsync(CancellationToken cancellationToken = default)
    {
        return bot.StartAsync(cancellationToken);
    }
    public Task StopAsync(CancellationToken cancellationToken = default)
    {
        return bot.StopAsync(cancellationToken);
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

    public abstract Task StartAsync(CancellationToken cancellationToken = default);
    public abstract Task StopAsync(CancellationToken cancellationToken = default);
}

public abstract class AbstractConfigChannelBot<T>(string channelProvider) : AbstractChannelBot(channelProvider), IConfigure<T>, IKeyedConfigure<T>
{
    public string Key { get; protected set; } = string.Empty;
    public T? Config { get; protected set; }

    public ValueTask<bool> ConfigureAsync(T config)
    {
        Config = config;
        return ConfigureAsync();
    }

    public ValueTask<bool> ConfigureAsync(string key, T config)
    {
        Key = key;
        Config = config;
        return ConfigureAsync();
    }

    protected abstract ValueTask<bool> ConfigureAsync();
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
