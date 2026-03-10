using BlazorClaw.Core.Commands;
using BlazorClaw.Core.DTOs;
using BlazorClaw.Core.Sessions;
using Microsoft.AspNetCore.SignalR;
using System.Security.Claims;

namespace BlazorClaw.Server.Hubs;

public class ChatHub : Hub
{
    private readonly ISessionManager _sessionManager;
    private readonly ILogger<ChatHub> _logger;

    public ChatHub(ISessionManager sessionManager, ILogger<ChatHub> logger)
    {
        _sessionManager = sessionManager;
        _logger = logger;
    }

    public async Task SendMessage(Guid sessionId, string message)
    {
        var userId = Context.User?.FindFirst(ClaimTypes.NameIdentifier)?.Value ?? "anonymous";
        
        _logger.LogInformation("Received message for session {SessionId} from user {UserId}", sessionId, userId);

        try
        {
            var state = await _sessionManager.GetOrCreateSessionAsync(sessionId);
            
            if (state.Provider == null)
            {
                _logger.LogError("Provider is null for session {SessionId}", sessionId);
                await Clients.Caller.SendAsync("Error", sessionId, "Provider not configured");
                return;
            }
            
            // Add user message to history
            var userMsg = new ChatMessage { Role = "user", Content = message };
            await _sessionManager.AppendMessageAsync(sessionId, userMsg);
            
            // Notify client that we're processing
            await Clients.Caller.SendAsync("MessageStarted", sessionId);

            _logger.LogInformation("Sending to LLM with provider {Provider} for session {SessionId}", state.Provider.Uri, sessionId);

            // Send to LLM and stream response
            var context = new MessageContext 
            { 
                UserId = userId, 
                Session = state.Session,
                Provider = state.Services
            };
            await foreach (var response in _sessionManager.DispatchToLLMAsync(state, context))
            {
                if (response.Content != null)
                {
                    await Clients.Caller.SendAsync("ReceiveMessage", sessionId, response);
                }
            }
            
            // Update session timestamp
            state.Session.LastUsedAt = DateTime.UtcNow;
            
            // Notify completion
            await Clients.Caller.SendAsync("MessageCompleted", sessionId);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error processing message for session {SessionId}: {Error}", sessionId, ex.Message);
            await Clients.Caller.SendAsync("Error", sessionId, ex.Message);
        }
    }

    public async Task LoadHistory(Guid sessionId)
    {
        try
        {
            var state = await _sessionManager.GetSessionAsync(sessionId);
            if (state != null)
            {
                await Clients.Caller.SendAsync("HistoryLoaded", sessionId, state.MessageHistory);
            }
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error loading history for session {SessionId}", sessionId);
        }
    }
}
