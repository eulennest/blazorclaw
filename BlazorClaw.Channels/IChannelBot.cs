namespace BlazorClaw.Channels;

public interface IChannelBot
{
    string ProviderName { get; }
    Task SendMessageAsync(string channelId, string message);
    event Func<string, string, string, Task>? OnMessageReceived;
}
