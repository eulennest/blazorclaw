using BlazorClaw.Core.DTOs;
using BlazorClaw.Core.Providers;
using BlazorClaw.Core.Security;
using BlazorClaw.Core.Tools;
using BlazorClaw.Core.Utils;
using BlazorClaw.Server.Models;
using System.Collections.Concurrent;
using System.Text.Json;

namespace BlazorClaw.Server.Services
{
    public class ChatSessionState
    {
        public ChatSession Session { get; set; } = default!;
        public required IProviderConfiguration Provider { get; set; }
        public List<ChatMessage> MessageHistory { get; set; } = [];
        public List<ChatMessage> SystemPrompts { get; set; } = [];
        public List<FunctionMessage> Tools { get; set; } = [];
    }

    public interface ISessionManager
    {
        Task<ChatSessionState> GetOrCreateSessionAsync(Guid sessionId, string model);
        Task<ChatSessionState?> GetSessionAsync(Guid sessionId);
        Task SaveToDiskAsync(ChatSessionState sessionState);
        Task AppendMessageAsync(Guid sessionId, BlazorClaw.Core.DTOs.ChatMessage message);
        IAsyncEnumerable<ChatMessage> DispatchToLLMAsync(ChatSessionState sess);
    }

    public class SessionManager(IProviderManager providerManager, IServiceScopeFactory scopeFactory, ILogger<SessionManager> logger) : ISessionManager
    {
        private readonly ConcurrentDictionary<Guid, ChatSessionState> _sessions = new();

        public Task<ChatSessionState> GetOrCreateSessionAsync(Guid sessionId, string model)
        {
            if (!_sessions.TryGetValue(sessionId, out var state))
            {
                logger.LogInformation("Creating session {SessionId}", sessionId);
                var prov = providerManager.GetProviderConfig(model) ?? throw new Exception($"No provider found for model {model}");

                // Hier später Datenbank-Lookup implementieren
                state = new ChatSessionState
                {
                    Session = new ChatSession { Id = sessionId, CurrentModel = model },
                    Provider = prov
                };

                _sessions.TryAdd(sessionId, state);
            }
            return Task.FromResult(state);
        }

        public async Task<ChatSessionState?> GetSessionAsync(Guid sessionId)
        {
            _sessions.TryGetValue(sessionId, out var state);

            if (state == null && File.Exists("session_{sessionId}.json"))
            {
                using var jsonStream = File.OpenRead($"session_{sessionId}.json");
                var store = await JsonSerializer.DeserializeAsync<JsonSessionStorage>(jsonStream).ConfigureAwait(false);
                if (store != null)
                {
                    var prov = providerManager.GetProviderConfig(store.Session.CurrentModel) ?? throw new Exception($"No provider found for model {store.Session.CurrentModel}");
                    state = new ChatSessionState
                    {
                        Session = store.Session,
                        Provider = prov,
                        MessageHistory = store.MessageHistory
                    };
                    _sessions.TryAdd(sessionId, state);
                    return state;
                }
            }

            return state;
        }

        public async Task SaveToDiskAsync(ChatSessionState sessionState)
        {
            var store = new JsonSessionStorage
            {
                Session = sessionState.Session,
                MessageHistory = sessionState.MessageHistory
            };
            using var jsonStream = File.Create($"session_{sessionState.Session.Id}.json");
            await JsonSerializer.SerializeAsync(jsonStream, store, JsonHelper.DefaultOptions).ConfigureAwait(false);
        }

        public Task AppendSystemPromptAsync(Guid sessionId, BlazorClaw.Core.DTOs.ChatMessage message)
        {
            if (_sessions.TryGetValue(sessionId, out var state))
            {
                state.SystemPrompts.Add(message);
            }
            return Task.CompletedTask;
        }

        public Task SetSystemPromptsAsync(Guid sessionId, List<BlazorClaw.Core.DTOs.ChatMessage> messages)
        {
            if (_sessions.TryGetValue(sessionId, out var state))
            {
                state.SystemPrompts = messages;
            }
            return Task.CompletedTask;
        }

        public Task AppendMessageAsync(Guid sessionId, BlazorClaw.Core.DTOs.ChatMessage message)
        {
            if (_sessions.TryGetValue(sessionId, out var state))
            {
                logger.LogDebug("Appending message to session {SessionId}, role: {Role}", sessionId, message.Role);
                state.MessageHistory.Add(message);
            }
            return Task.CompletedTask;
        }


        public async IAsyncEnumerable<ChatMessage> DispatchToLLMAsync(ChatSessionState sessionState)
        {
            logger.LogInformation("Dispatching LLM request for session {SessionId}", sessionState.Session.Id);
            using var scope = scopeFactory.CreateScope();
            using var httpClient = scope.ServiceProvider.GetRequiredService<HttpClient>();
            httpClient.BaseAddress = new Uri(sessionState.Provider.Uri.TrimEnd('/') + "/");
            if (!string.IsNullOrWhiteSpace(sessionState.Provider.Uri))
                httpClient.DefaultRequestHeaders.Add("Authorization", $"Bearer {sessionState.Provider.Token}");

            // Context für Security/Policies
            var context = new ToolContext
            {
                SessionId = sessionState.Session.Id,
                ServiceProvider = scope.ServiceProvider,
                UserId = sessionState.Session.Participants.FirstOrDefault()?.UserId,
                HttpContext = scope.ServiceProvider.GetService<IHttpContextAccessor>()?.HttpContext
            };

            var toolRegistry = scope.ServiceProvider.GetRequiredService<IToolRegistry>();
            var policyProvider = scope.ServiceProvider.GetRequiredService<IToolPolicyProvider>();

            if ((sessionState.Tools?.Count ?? 0) == 0)
            {

                // 2. Tools filtern und hinzufügen
                var tools = policyProvider.FilterTools(toolRegistry.GetAllTools(), context);
                if (tools.Any())
                {
                    sessionState.Tools ??= [];
                    foreach (var tool in tools)
                    {
                        sessionState.Tools.Add(
                            new()
                            {
                                Function = new() { Name = tool.Name, Description = tool.Description, Parameters = tool.GetSchema() }
                            });
                    }
                }
            }

            int count;
            int iterations = 0;
            do
            {
                iterations++;
                count = 0;
                await foreach (var msg in InternalDispatchToLLMAsync(sessionState, context, httpClient, toolRegistry, policyProvider, logger))
                {
                    count++;
                    yield return msg;
                }
                await SaveToDiskAsync(sessionState);
            }
            while (count > 1 && iterations < 10);
        }

        private static async IAsyncEnumerable<ChatMessage> InternalDispatchToLLMAsync(ChatSessionState sessionState, ToolContext context, HttpClient httpClient, IToolRegistry toolRegistry, IToolPolicyProvider policyProvider, ILogger logger)
        {
            var request = new ChatCompletionRequest
            {
                Model = sessionState.Session.CurrentModel.Split('/', 2)[1],
                Messages = [.. sessionState.SystemPrompts, .. sessionState.MessageHistory],
                Tools = sessionState.Tools
            };

            // 3. OpenAI Request
            var response = await httpClient.PostAsJsonAsync("chat/completions", request);
            var content = await response.Content.ReadFromJsonAsync<ChatCompletionResponse>();
            if (content == null) throw new ArgumentNullException(nameof(content), "Invalid response");

            if (content.Choices != null)
            {
                // 4. Tool Handling Loop
                foreach (var choice in content.Choices)
                {
                    var message = choice.Message;
                    sessionState.MessageHistory.Add(message); // Assistant Call
                    yield return message;

                    if (message.ToolCalls != null && message.ToolCalls.Count != 0)
                    {
                        foreach (var call in message.ToolCalls)
                        {
                            logger.LogInformation("Tool called: {Name} args: {Args}", call.Function.Name, call.Function.Arguments);
                            ChatMessage msg;
                            try
                            {
                                var tool = toolRegistry.GetTool(call.Function.Name) ?? throw new ToolNotFoundException(call.Function.Name);
                                var args = tool.BuidlArguments(call.Function.Arguments);

                                policyProvider.BeforeTool(tool, args, context);
                                var result = await tool.ExecuteAsync(args, context);
                                result = policyProvider.AfterTool(tool, args, result, context);
                                logger.LogDebug("Tool: {Name} Result: {result}", call.Function.Name, result);

                                msg = new ChatMessage
                                {
                                    Role = "tool",
                                    Content = result,
                                    ExtensionData = new Dictionary<string, object> { { "tool_call_id", call.Id } }
                                };
                            }
                            catch (Exception ex)
                            {
                                msg = new ChatMessage
                                {
                                    Role = "tool",
                                    Content = ToolErrorHandler.ToProblemDetailsJson(ex, call.Function.Name),
                                    ExtensionData = new Dictionary<string, object> { { "tool_call_id", call.Id } }
                                };
                            }
                            sessionState.MessageHistory.Add(msg); // Tool Result
                            yield return msg;
                        }
                    }
                }
            }
        }
    }

    public class JsonSessionStorage
    {
        public ChatSession Session { get; set; } = default!;
        public List<BlazorClaw.Core.DTOs.ChatMessage> MessageHistory { get; set; } = [];
    }
}