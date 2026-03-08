using BlazorClaw.Core.Commands;
using BlazorClaw.Core.DTOs;
using System.CommandLine;

namespace BlazorClaw.Core.Sessions;

public interface ISessionManager
{
    Task<ChatSessionState> GetOrCreateSessionAsync(Guid sessionId, string? model = null);
    Task<ChatSessionState?> GetSessionAsync(Guid sessionId);
    Task SaveToDiskAsync(ChatSessionState sessionState);
    Task AppendMessageAsync(Guid sessionId, ChatMessage message);
    IAsyncEnumerable<ChatMessage> DispatchToLLMAsync(ChatSessionState sess);
    Task<object?> DispatchCommandAsync(string cmdline, CommandContext cmdContext, RootCommand rootCmd, ICommandProvider commandProvider);
}