using BlazorClaw.Core.DTOs;
using BlazorClaw.Core.Models;
using BlazorClaw.Core.Providers;

namespace BlazorClaw.Core.Sessions;

public class ChatSessionState
{
    public ChatSession Session { get; set; } = default!;
    public required IProviderConfiguration Provider { get; set; }
    public List<ChatMessage> MessageHistory { get; set; } = [];
    public List<ChatMessage> SystemPrompts { get; set; } = [];
    public List<FunctionMessage> Tools { get; set; } = [];
    public TokenUsage? LastUsage { get; set; }
    public double Costs { get; set; }
    public double Tokens { get; set; }
}
