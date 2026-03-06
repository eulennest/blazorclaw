using BlazorClaw.Server.Models;
using System.Collections.Concurrent;

namespace BlazorClaw.Server.Services
{
    public class ChatSessionState
    {
        public ChatSession Session { get; set; } = default!;
        public List<BlazorClaw.Core.DTOs.ChatMessage> MessageHistory { get; set; } = new();
    }

    public interface ISessionManager
    {
        Task<ChatSessionState> GetOrCreateSessionAsync(Guid sessionId, string model);
        Task<ChatSessionState?> GetSessionAsync(Guid sessionId);
        Task SaveToDiskAsync(Guid sessionId);
        Task AppendMessageAsync(Guid sessionId, BlazorClaw.Core.DTOs.ChatMessage message);
    }

    public class SessionManager : ISessionManager
    {
        private readonly ConcurrentDictionary<Guid, ChatSessionState> _sessions = new();
        private readonly IServiceProvider _serviceProvider;

        public SessionManager(IServiceProvider serviceProvider)
        {
            _serviceProvider = serviceProvider;
        }

        public async Task<ChatSessionState> GetOrCreateSessionAsync(Guid sessionId, string model)
        {
            if (_sessions.TryGetValue(sessionId, out var state)) return state;

            // Hier später Datenbank-Lookup implementieren
            var newState = new ChatSessionState
            {
                Session = new ChatSession { Id = sessionId, CurrentModel = model }
            };
            
            _sessions.TryAdd(sessionId, newState);
            return newState;
        }

        public Task<ChatSessionState?> GetSessionAsync(Guid sessionId)
        {
            _sessions.TryGetValue(sessionId, out var state);
            return Task.FromResult(state);
        }

        public Task SaveToDiskAsync(Guid sessionId)
        {
            // TODO: Implementierung von session_*.json Persistenz
            return Task.CompletedTask;
        }

        public Task AppendMessageAsync(Guid sessionId, BlazorClaw.Core.DTOs.ChatMessage message)
        {
            if (_sessions.TryGetValue(sessionId, out var state))
            {
                state.MessageHistory.Add(message);
            }
            return Task.CompletedTask;
        }
    }
}
