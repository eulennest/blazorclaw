using BlazorClaw.Core.DTOs;

namespace BlazorClaw.Core.Sessions;

public interface ISessionManager
{
    Task<ChatSessionState> GetOrCreateSessionAsync(Guid sessionId, string? model = null);
    Task<ChatSessionState?> GetSessionAsync(Guid sessionId);
    Task SaveToDiskAsync(ChatSessionState sessionState);
    Task AppendMessageAsync(Guid sessionId, ChatMessage message);
    IAsyncEnumerable<ChatMessage> DispatchToLLMAsync(ChatSessionState sess);
}