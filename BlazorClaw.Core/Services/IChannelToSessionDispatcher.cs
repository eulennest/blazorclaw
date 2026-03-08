using BlazorClaw.Core.Sessions;

namespace BlazorClaw.Core.Services;

public interface IChannelToSessionDispatcher
{
    Task<ChatSessionState> GetOrCreateSessionForChannelAsync(string channelProvider, string channelId);
}
