using BlazorClaw.Core.Commands;
using BlazorClaw.Core.Data;
using BlazorClaw.Core.DTOs;
using BlazorClaw.Core.Models;
using BlazorClaw.Core.Providers;
using BlazorClaw.Core.Security;
using BlazorClaw.Core.Sessions;
using BlazorClaw.Core.Tools;
using BlazorClaw.Core.Utils;
using Microsoft.Extensions.Options;
using System.Collections.Concurrent;
using System.CommandLine;
using System.Text.Json;

namespace BlazorClaw.Server.Services
{

    public class SessionManager(IServiceScopeFactory scopeFactory, ILogger<SessionManager> logger, IOptionsMonitor<LlmOptions> options) : ISessionManager
    {
        public string SessionStoragePath { get; set; } = "sessions";
        private readonly ConcurrentDictionary<Guid, ChatSessionState> _sessions = new();

        public async Task<ChatSessionState> GetOrCreateSessionAsync(Guid sessionId, string? model = null)
        {
            var state = await GetSessionAsync(sessionId).ConfigureAwait(false);

            if (state == null)
            {
                logger.LogInformation("Creating session {SessionId}", sessionId);
                var scope = scopeFactory.CreateScope();
                var db = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
                var sess = await db.ChatSessions.FindAsync(sessionId);
                model ??= sess?.CurrentModel ?? options.CurrentValue.Model;
                if (sess == null)
                {
                    sess = new ChatSession
                    {
                        Id = sessionId,
                        CurrentModel = model,
                        CreatedAt = DateTime.UtcNow,
                        LastUsedAt = DateTime.UtcNow,
                        Title = "New Session"
                    };
                    db.ChatSessions.Add(sess);
                    await db.SaveChangesAsync().ConfigureAwait(false);
                }

                state = new ChatSessionState
                {
                    Scope = scope,
                    Session = sess,
                    Provider = scope.ServiceProvider.GetRequiredService<IProviderManager>().GetProviderConfig(model?.Split('/')[0] ?? "openrouter") ?? throw new Exception($"No provider found for model {model}")
                };
                scope.ServiceProvider.GetRequiredService<SessionStateAccessor>().SetSessionState(state);


                _sessions.TryAdd(sessionId, state);
            }
            return state;
        }

        public async Task<ChatSessionState?> GetSessionAsync(Guid sessionId)
        {
            if (_sessions.TryGetValue(sessionId, out var state)) return state;
            var path = Path.Combine(SessionStoragePath, $"session_{sessionId}.json");

            if (state == null && File.Exists(path))
            {
                using var jsonStream = File.OpenRead(path);
                var store = await JsonSerializer.DeserializeAsync<JsonSessionStorage>(jsonStream, JsonHelper.DefaultOptions).ConfigureAwait(false);
                if (store != null)
                {
                    var scope = scopeFactory.CreateScope();
                    var model = store.Session?.CurrentModel;
                    state = new ChatSessionState
                    {
                        Scope = scope,
                        Session = store.Session,
                        Provider = scope.ServiceProvider.GetRequiredService<IProviderManager>().GetProviderConfig(model?.Split('/')[0] ?? "openrouter") ?? throw new Exception($"No provider found for model {model}"),
                        MessageHistory = store.MessageHistory
                    };
                    _sessions.TryAdd(sessionId, state);
                    return state;
                }
            }

            return state;
        }

        public async Task SaveSessionAsync(ChatSessionState sessionState, bool newVersion = false)
        {
            var db = sessionState.Services.GetRequiredService<ApplicationDbContext>();
            sessionState.Session.LastUsedAt = DateTime.UtcNow;
            db.ChatSessions
            .Update(sessionState.Session);
            await db.SaveChangesAsync().ConfigureAwait(false);
            await SaveToDiskAsync(sessionState, newVersion).ConfigureAwait(false);
        }

        public async Task SaveToDiskAsync(ChatSessionState sessionState, bool newVersion = false)
        {
            var path = Path.Combine(SessionStoragePath, $"session_{sessionState.Session.Id}.json");
            Directory.CreateDirectory(SessionStoragePath);
            var store = new JsonSessionStorage
            {
                Session = sessionState.Session,
                MessageHistory = sessionState.MessageHistory
            };
            if (newVersion)
            {
                if (File.Exists(path))
                {
                    var newPath = Path.Combine(SessionStoragePath, $"session_{sessionState.Session.Id}_{DateTime.UtcNow:yyyyMMddHHmmss}.json");
                    File.Move(path, newPath);
                }
            }
            using var jsonStream = File.Create(path);
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

        public async Task<object?> DispatchCommandAsync(string cmdline, MessageContext cmdContext, RootCommand rootCmd, ICommandProvider commandProvider)
        {
            if (cmdline.StartsWith('/'))
            {
                var commandText = cmdline[1..].Split(' ')[0].ToLower(); // Get command without '/'
                logger.LogInformation("Received command: {Command}", commandText);
                var command = commandProvider.GetCommands()
                    .FirstOrDefault(o => o.GetCommand().Name.Equals(commandText, StringComparison.OrdinalIgnoreCase));

                if (command != null)
                {
                    logger.LogInformation("Executing command: {Command}", commandText);
                    return await commandProvider.ExecuteAsync(command, rootCmd.Parse(cmdline[1..]), cmdContext);
                }
            }
            return null;
        }

        public async IAsyncEnumerable<ChatMessage> DispatchToLLMAsync(ChatSessionState sessionState, MessageContext context)
        {
            logger.LogInformation("Dispatching LLM request for session {SessionId}", sessionState.Session.Id);
            using var httpClient = sessionState.Services.GetRequiredService<HttpClient>();
            httpClient.BaseAddress = new Uri(sessionState.Provider.Uri.TrimEnd('/') + "/");
            if (!string.IsNullOrWhiteSpace(sessionState.Provider.Uri))
                httpClient.DefaultRequestHeaders.Add("Authorization", $"Bearer {sessionState.Provider.Token}");

            var toolRegistry = sessionState.Services.GetRequiredService<IToolRegistry>();
            var policyProvider = sessionState.Services.GetRequiredService<IToolPolicyProvider>();

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
            if ((sessionState.SystemPrompts?.Count ?? 0) == 0)
            {
                sessionState.SystemPrompts ??= [];
                if (File.Exists("SYSTEMPROMPT.md"))
                {
                    var systemPromptContent = await File.ReadAllTextAsync("SYSTEMPROMPT.md").ConfigureAwait(false);
                    sessionState.SystemPrompts.Add(new ChatMessage { Role = "system", Content = systemPromptContent });
                }
                sessionState.SystemPrompts.Add(new DynamicSystemChatMessage(sessionState));

                if (File.Exists("AGENTS.md"))
                {
                    var agentPromptContent = await File.ReadAllTextAsync("AGENTS.md").ConfigureAwait(false);
                    sessionState.SystemPrompts.Add(new ChatMessage { Role = "system", Content = "[File: AGENTS.md]" + Environment.NewLine + "-----" + Environment.NewLine + agentPromptContent });
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
                await SaveSessionAsync(sessionState, false);
            }
            while (count > 1 && iterations < 10);
        }

        private static async IAsyncEnumerable<ChatMessage> InternalDispatchToLLMAsync(ChatSessionState sessionState, MessageContext context, HttpClient httpClient, IToolRegistry toolRegistry, IToolPolicyProvider policyProvider, ILogger logger)
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

            sessionState.LastUsage = content.Usage ?? sessionState.LastUsage;
            sessionState.Costs += content.Usage?.PromptCost ?? 0;
            sessionState.Tokens += content.Usage?.TotalTokens ?? 0;

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

                                if (sessionState.MessageHistory.Any(o => o.ToolCalls?.Any(o => o.Id == call.Id) ?? false))
                                {
                                    msg = new ChatMessage
                                    {
                                        Role = "tool",
                                        Content = result,
                                        ExtensionData = new Dictionary<string, object> { { "tool_call_id", call.Id } }
                                    };
                                }
                                else
                                {
                                    msg = new ChatMessage
                                    {
                                        Role = "system",
                                        Content = $"Tool Call (tool_call_id not found in history): {result}"
                                    };
                                }
                            }
                            catch (Exception ex)
                            {
                                logger.LogError(ex, "Error: {Message}", ex.Message);
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