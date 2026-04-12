using BlazorClaw.Core.DTOs;
using BlazorClaw.Core.Models;
using BlazorClaw.Core.Providers;
using BlazorClaw.Core.Tools;
using BlazorClaw.Core.VFS;
using Microsoft.Extensions.AI;
using Microsoft.Extensions.DependencyInjection;

namespace BlazorClaw.Core.Sessions;

public class ChatSessionState
{
    public required IServiceScope Scope { get; set; }
    public IServiceProvider Services => Scope.ServiceProvider;
    public ChatSession Session { get; set; } = default!;
    public required IChatClient Provider { get; set; }
    public List<ChatMessage> MessageHistory { get; set; } = [];
    public List<ChatMessage> SystemPrompts { get; set; } = [];
    public List<ITool>? Tools { get; set; }
    public TokenUsage? LastUsage { get; set; }
    public double Costs { get; set; }
    public double Tokens { get; set; }

    public IVfsSystem? VFS { get; set; }
}

public class SessionStateAccessor
{
    public void SetSessionState(ChatSessionState state)
    {
        SessionState = state;
    }
    public ChatSessionState? SessionState { get; private set; }
}

