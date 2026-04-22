using BlazorClaw.Core.Commands;
using Microsoft.Extensions.AI;
using System.CommandLine;
using System.Text;

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
    string BotId { get; }

    Task StartAsync(CancellationToken cancellationToken = default);
    Task StopAsync(CancellationToken cancellationToken = default);

    string ChannelProvider { get; }
    event EventHandler<MessageReceiveEventArgs>? MessageReceived;
    Task SendChannelAsync(IChannelSession channelId, ChatMessage message, CancellationToken cancellationToken = default);
    Task SendUserAsync(IChannelSession channelId, ChatMessage message, CancellationToken cancellationToken = default);
}
public class MessageReceiveEventArgs(IChannelSession channel, object message)
{
    public IChannelSession Channel { get; } = channel;
    public object Message { get; } = message;
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
    public string BotId => bot.BotId;
    public string ChannelId { get; } = channelId;
    public string SenderId { get; } = senderId ?? channelId;
    public Guid SessionId { get; set; }

    public override string ToString()
    {
        return $"{ChannelProvider}:{ChannelId}, SenderId={SenderId}, SessionId={SessionId}";
    }


    public event EventHandler<MessageReceiveEventArgs>? MessageReceived
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
    public virtual string BotId { get; protected set; }

    public event EventHandler<MessageReceiveEventArgs>? MessageReceived;

    public abstract Task SendChannelAsync(IChannelSession channelId, ChatMessage message, CancellationToken cancellationToken = default);
    public abstract Task SendUserAsync(IChannelSession channelId, ChatMessage message, CancellationToken cancellationToken = default);

    public void OnMessageReceived(IChannelSession channelSession, object message)
    {
        MessageReceived?.Invoke(this, new(channelSession, message));
    }

    public abstract Task StartAsync(CancellationToken cancellationToken = default);
    public abstract Task StopAsync(CancellationToken cancellationToken = default);
}

public abstract class AbstractConfigChannelBot<T>(string channelProvider) : AbstractChannelBot(channelProvider), IKeyedConfigure<T>
{
    public string Key => BotId;
    public T? Config { get; protected set; }
    public ValueTask<bool> ConfigureAsync(string key, T config)
    {
        BotId = key;
        Config = config;
        return ConfigureAsync();
    }

    protected abstract ValueTask<bool> ConfigureAsync();



    public static IEnumerable<string> SplitMessageHybrid(
    string message,
    int maxLength = 4000,
    bool addIndicator = false)
    {
        if (string.IsNullOrEmpty(message))
            yield break;

        if (message.Length <= maxLength)
        {
            yield return message;
            yield break;
        }

        var lines = message.Split(new[] { "\r\n", "\r", "\n" }, StringSplitOptions.None);
        var currentChunk = new StringBuilder();
        int chunkCount = 0;

        // Erste Iteration: Zähle Chunks
        var tempChunk = new StringBuilder();
        foreach (var line in lines)
        {
            var trimmedLine = line.Trim();
            if (string.IsNullOrEmpty(trimmedLine))
                continue;

            if (tempChunk.Length + trimmedLine.Length + 1 > maxLength && tempChunk.Length > 0)
            {
                chunkCount++;
                tempChunk.Clear();
            }
            tempChunk.AppendLine(trimmedLine);
        }
        if (tempChunk.Length > 0)
            chunkCount++;

        int currentChunkNumber = 1;

        // Zweite Iteration: Generiere Chunks
        foreach (var line in lines)
        {
            var trimmedLine = line.Trim();
            if (string.IsNullOrEmpty(trimmedLine))
                continue;

            int reservedSpace = addIndicator ? 10 : 0;

            // Wenn die Zeile selbst zu lang ist → Satzweise aufteilen
            if (trimmedLine.Length > maxLength - reservedSpace)
            {
                // Gib den aktuellen Chunk aus, falls nicht leer
                if (currentChunk.Length > 0)
                {
                    var chunkText = currentChunk.ToString().TrimEnd();
                    if (addIndicator)
                        chunkText += $"\n\n[{currentChunkNumber}/{chunkCount}]";
                    yield return chunkText;
                    currentChunk.Clear();
                    currentChunkNumber++;
                }

                // Teile lange Zeile nach Sätzen auf
                foreach (var sentenceChunk in SplitLongLineBySentences(trimmedLine, maxLength - reservedSpace))
                {
                    var chunkText = sentenceChunk;
                    if (addIndicator)
                        chunkText += $"\n\n[{currentChunkNumber}/{chunkCount}]";
                    yield return chunkText;
                    currentChunkNumber++;
                }
            }
            // Normale Verarbeitung: Zeilengrenzen respektieren
            else if (currentChunk.Length + trimmedLine.Length + 1 > maxLength - reservedSpace && currentChunk.Length > 0)
            {
                var chunkText = currentChunk.ToString().TrimEnd();
                if (addIndicator)
                    chunkText += $"\n\n[{currentChunkNumber}/{chunkCount}]";
                yield return chunkText;
                currentChunk.Clear();
                currentChunkNumber++;
                currentChunk.AppendLine(trimmedLine);
            }
            else
            {
                currentChunk.AppendLine(trimmedLine);
            }
        }

        // Letzter Chunk
        if (currentChunk.Length > 0)
        {
            var chunkText = currentChunk.ToString().TrimEnd();
            if (addIndicator)
                chunkText += $"\n\n[{currentChunkNumber}/{chunkCount}]";
            yield return chunkText;
        }
    }

    /// <summary>
    /// Teilt eine zu lange Zeile nach Satzgrenzen auf.
    /// Wenn Sätze immer noch zu lang sind, wird nach Zeichen aufgeteilt.
    /// </summary>
    private static IEnumerable<string> SplitLongLineBySentences(string line, int maxLength)
    {
        var sentences = System.Text.RegularExpressions.Regex.Split(
            line,
            @"(?<=[.!?;])\s+"
        );

        var currentChunk = new StringBuilder();

        foreach (var sentence in sentences)
        {
            var trimmedSentence = sentence.Trim();

            // Wenn ein einzelner Satz länger ist als maxLength → aufteilen
            if (trimmedSentence.Length > maxLength)
            {
                // Gib den aktuellen Chunk aus
                if (currentChunk.Length > 0)
                {
                    yield return currentChunk.ToString().TrimEnd();
                    currentChunk.Clear();
                }

                // Teile den langen Satz nach Worten auf
                foreach (var wordChunk in SplitLongSentenceByWords(trimmedSentence, maxLength))
                {
                    yield return wordChunk;
                }
            }
            else if (currentChunk.Length + trimmedSentence.Length + 1 > maxLength && currentChunk.Length > 0)
            {
                yield return currentChunk.ToString().TrimEnd();
                currentChunk.Clear();
                currentChunk.Append(trimmedSentence + " ");
            }
            else
            {
                currentChunk.Append(trimmedSentence + " ");
            }
        }

        if (currentChunk.Length > 0)
        {
            yield return currentChunk.ToString().TrimEnd();
        }
    }

    /// <summary>
    /// Teilt einen langen Satz nach Worten auf (Fallback).
    /// </summary>
    private static IEnumerable<string> SplitLongSentenceByWords(string sentence, int maxLength)
    {
        var words = sentence.Split(new[] { ' ' }, StringSplitOptions.RemoveEmptyEntries);
        var currentChunk = new StringBuilder();

        foreach (var word in words)
        {
            if (currentChunk.Length + word.Length + 1 > maxLength && currentChunk.Length > 0)
            {
                yield return currentChunk.ToString().TrimEnd();
                currentChunk.Clear();
            }

            currentChunk.Append(word + " ");
        }

        if (currentChunk.Length > 0)
        {
            yield return currentChunk.ToString().TrimEnd();
        }
    }
}

public interface IMessageDispatcher
{
    /// <summary>
    /// Gibt die SessionId zurück, die zu dieser User-Kanal-Kombination gehört.
    /// </summary>
    void Register(IChannelBot bot);
    void Unregister(IChannelBot bot);

    void DispatchMessage(object? sender, Core.Sessions.MessageReceiveEventArgs e);
}
