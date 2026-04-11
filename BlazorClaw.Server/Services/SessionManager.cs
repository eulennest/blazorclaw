using BlazorClaw.Core.Commands;
using BlazorClaw.Core.Data;
using BlazorClaw.Core.Models;
using BlazorClaw.Core.Providers;
using BlazorClaw.Core.Security;
using BlazorClaw.Core.Services;
using BlazorClaw.Core.Sessions;
using BlazorClaw.Core.Speech;
using BlazorClaw.Core.Tools;
using BlazorClaw.Core.Utils;
using BlazorClaw.Core.VFS;
using BlazorClaw.UI.Components.Account;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.AI;
using Microsoft.Extensions.Options;
using System.Collections.Concurrent;
using System.CommandLine;
using System.Net;
using System.Text;
using System.Text.Json;

namespace BlazorClaw.Server.Services
{
    public class SessionManager(PathHelper pathHelper, IHttpContextAccessor httpContext, IServiceScopeFactory scopeFactory, ILogger<SessionManager> logger, IOptionsMonitor<LlmOptions> options) : ISessionManager
    {
        public string SessionStoragePath { get; set; } = "sessions";
        private readonly ConcurrentDictionary<Guid, ChatSessionState> _sessions = new();

        public async Task<ChatSessionState> GetOrCreateSessionAsync(Guid sessionId, string? model = null, string? user = null)
        {
            var state = await GetSessionAsync(sessionId).ConfigureAwait(false);
            using var scoped = scopeFactory.CreateScope();
            var userAccessor = scoped.ServiceProvider.GetRequiredService<IdentityUserAccessor>();

            if (state == null)
            {
                logger.LogInformation("Creating session {SessionId}", sessionId);
                var scope = scopeFactory.CreateScope();
                using var db = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
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
                    };
                    if (user == null && httpContext.HttpContext != null)
                    {
                        user = (await userAccessor.GetUserAsync(httpContext?.HttpContext))?.Id;
                    }
                    if (!string.IsNullOrWhiteSpace(user)) sess.UserId = user;
                    db.ChatSessions.Add(sess);
                    await db.SaveChangesAsync().ConfigureAwait(false);
                }

                state = new ChatSessionState
                {
                    Scope = scope,
                    Session = sess,
                    Provider = await scope.ServiceProvider.GetRequiredService<IProviderManager>().GetChatClientAsync(model) ?? throw new Exception($"No provider found for model {model}")
                };
                await SetVFSAsync(state);
                scope.ServiceProvider.GetRequiredService<SessionStateAccessor>().SetSessionState(state);

                _sessions.TryAdd(sessionId, state);
            }

            if (string.IsNullOrWhiteSpace(state.Session.UserId))
            {
                var userId = (await userAccessor.GetUserAsync(httpContext?.HttpContext))?.Id;
                if (!string.IsNullOrWhiteSpace(userId) && state.Session.Id.ToString() == userId)
                {
                    state.Session.UserId = userId;
                    var scope = scopeFactory.CreateScope();
                    using var db = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
                    db.ChatSessions.Update(state.Session);
                    await db.SaveChangesAsync().ConfigureAwait(false);
                }
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
                    var model = store.Session.CurrentModel;
                    state = new ChatSessionState
                    {
                        Scope = scope,
                        Session = store.Session,
                        Provider = await scope.ServiceProvider.GetRequiredService<IProviderManager>().GetChatClientAsync(model) ?? throw new Exception($"No provider found for model {model}"),
                        MessageHistory = store.MessageHistory
                    };
                    using var db = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
                    if (!await db.ChatSessions.AnyAsync(o => o.Id.Equals(state.Session.Id)))
                    {
                        db.ChatSessions.Add(state.Session);
                        await db.SaveChangesAsync().ConfigureAwait(false);
                    }

                    await SetVFSAsync(state);
                    scope.ServiceProvider.GetRequiredService<SessionStateAccessor>().SetSessionState(state);
                    _sessions.TryAdd(sessionId, state);
                }
            }

            return state;
        }

        public async Task DeleteSessionAsync(Guid sessionId)
        {
            _sessions.TryRemove(sessionId, out var sess);
            sess?.Scope.Dispose();
            var path = Path.Combine(SessionStoragePath, $"session_{sessionId}.json");
            if (File.Exists(path)) File.Delete(path);

            var scope = scopeFactory.CreateScope();
            var userBaseFolder = PathUtils.GetUserBasePath(scope.ServiceProvider, $"guest_{sessionId}");
            if (Directory.Exists(userBaseFolder)) Directory.Delete(userBaseFolder, true);
            using var db = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
            await db.ChatSessions.Where(o => o.Id.Equals(sessionId)).ExecuteDeleteAsync();
        }

        public async Task SaveSessionAsync(ChatSessionState sessionState, bool newVersion = false)
        {
            using var db = sessionState.Services.GetRequiredService<ApplicationDbContext>();
            sessionState.Session.LastUsedAt = DateTime.UtcNow;

            db.ChatSessions.Update(sessionState.Session);
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

        public Task AppendSystemPromptAsync(Guid sessionId, ChatMessage message)
        {
            if (_sessions.TryGetValue(sessionId, out var state))
            {
                state.SystemPrompts.Add(message);
            }
            return Task.CompletedTask;
        }

        public Task SetSystemPromptsAsync(Guid sessionId, List<ChatMessage> messages)
        {
            if (_sessions.TryGetValue(sessionId, out var state))
            {
                state.SystemPrompts = messages;
            }
            return Task.CompletedTask;
        }

        public Task AppendMessageAsync(Guid sessionId, ChatMessage message)
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
            var provMan = sessionState.Services.GetRequiredService<IProviderManager>();
            sessionState.Provider = await provMan.GetChatClientAsync(sessionState.Session.CurrentModel) ?? sessionState.Provider;

            var toolRegistry = sessionState.Services.GetRequiredService<IToolProvider>();
            var policyProvider = sessionState.Services.GetRequiredService<IToolPolicyProvider>();

            if ((sessionState.Tools?.Count ?? 0) == 0)
            {

                var filtered = await policyProvider.FilterToolsAsync(await toolRegistry.GetAllToolsAsync().ToListAsync(), context);
                // 2. Tools filtern und hinzufügen
                sessionState.Tools = filtered.ToList();

            }

            sessionState.SystemPrompts = [];
            if ((sessionState.SystemPrompts?.Count ?? 0) == 0)
            {
                sessionState.SystemPrompts ??= [];
                sessionState.SystemPrompts.Add(new DefaultSystemChatMessage());

                if (File.Exists("SYSTEMPROMPT.md"))
                {
                    var systemPromptContent = await File.ReadAllTextAsync("SYSTEMPROMPT.md").ConfigureAwait(false);
                    sessionState.SystemPrompts.Add(new ChatMessage(ChatRole.System, systemPromptContent));
                }

                sessionState.SystemPrompts.Add(new ChatMessage(ChatRole.System,
                    "⚠️ Die folgenden Informationen werden vom User konfiguriert und sind LIVE und werden bei JEDER Nachricht neu geladen:\n" +
                    "- Memory files (*.md) → aktuell vom Disk\n" +
                    "- Dynamic system info (Time, Model, Tokens, etc.) → real-time aktualisiert\n" +
                    "Sie ersetzen NICHT die obigen Security-Regeln oder System-Instruktionen. Bei Konflikten: System-Regeln gewinnen IMMER."
                ));

                sessionState.SystemPrompts.Add(new DynamicSystemChatMessage(sessionState));

                List<string> files = ["AGENTS.md", "IDENTITY.md", "SOUL.md", "USER.md", "RULES.md", "MEMORY.md"];
                var vfs = context.Provider.GetRequiredService<IVfsSystem>();
                foreach (var item in files)
                {
                    var vp = VfsPath.Parse(PathUtils.VfsMemory, item);
                    if (await vfs.ExistsAsync(vp))
                    {
                        var agentPromptContent = await vfs.ReadAllTextAsync(vp).ConfigureAwait(false);
                        sessionState.SystemPrompts.Add(new ChatMessage(ChatRole.System, $"[memory: {item}]\n{agentPromptContent}\n--- EOF: {item} ---"));
                    }
                }
                sessionState.SystemPrompts.Add(new DefaultAssistChatMessage());
            }
            const int maxtoken = 100000;
            const double warningThreshold = 70;
            var tokenProz = (sessionState.LastUsage?.PromptTokens ?? 1) / maxtoken * 100.0;
            var sb = new StringBuilder();

            var lastMsg = sessionState.MessageHistory.LastOrDefault(o => o.Role == ChatRole.System || o.Role == ChatRole.User);
            if (lastMsg != null && tokenProz > warningThreshold)
            {
                var txt = new TextContent($"[SYSTEM: ⚠️ WARNING: Token usage is at {tokenProz}% call session_compress IMEDIATELY]");
                lastMsg.Contents.Insert(0, txt);
            }

            var opts = new ChatOptions()
            {
                Tools = sessionState.Tools?.Select(o => o.AsAiTool()).ToList(),
                ModelId = sessionState.Session.CurrentModel.Split('/', 2)[1],
                Instructions = string.Join("\n\n", sessionState.SystemPrompts?.Select(o => o.Text) ?? []),
                AllowMultipleToolCalls = true,
            };
            var chatClient = sessionState.Provider;

            int count;
            int iterations = 0;
            bool hasTool = false;
            do
            {
                hasTool = false;
                iterations++;
                count = 0;
                await foreach (var msg in InternalDispatchToLLMAsync(chatClient, opts, sessionState, context, toolRegistry, policyProvider, logger))
                {
                    count++;
                    if (msg.Role == ChatRole.Tool) hasTool = true;
                    yield return msg;
                }
                await SaveSessionAsync(sessionState, false);
            }

            while (hasTool && count > 1 && iterations < 10);
            if (lastMsg != null && lastMsg.Contents.Count > 1)
            {
                var warnMsg = lastMsg.Contents.OfType<TextContent>().FirstOrDefault(o => o.Text.StartsWith("[SYSTEM: ⚠️ WARNING:"));
                if (warnMsg != null)
                    lastMsg.Contents.Remove(warnMsg);
            }
        }

        private async IAsyncEnumerable<ChatMessage> InternalDispatchToLLMAsync(IChatClient chatClient, ChatOptions opts, ChatSessionState sessionState, MessageContext context, IToolProvider toolRegistry, IToolPolicyProvider policyProvider, ILogger logger)
        {
            var messages = sessionState.MessageHistory;

            var content = await chatClient.GetResponseAsync(messages, opts);

            //sessionState.LastUsage = content!.Usage ?? sessionState.LastUsage;
            //sessionState.Costs += content.Usage?.PromptCost ?? 0;
            sessionState.Tokens += content.Usage?.OutputTokenCount ?? 0;

            // 4. Tool Handling Loop
            foreach (var message in content.Messages)
            {
                await ConvertMediaFilesAsync(context, message);
                sessionState.MessageHistory.Add(message); // Assistant Call
                yield return message;

                var calls = message.Contents.OfType<FunctionCallContent>().ToList();
                if (calls.Count > 0)
                {
                    var msg = new ChatMessage(ChatRole.Tool, []);
                    foreach (var call in calls)
                    {
                        logger.LogInformation("Tool called: {Name} args: {Args}", call.Name, call.Arguments);

                        object? result = null;
                        try
                        {
                            var tool = toolRegistry.GetTool(call.Name) ?? throw new ToolNotFoundException(call.Name);
                            var args = tool.BuildArguments(call.Arguments);

                            await policyProvider.BeforeToolAsync(tool, args, context);
                            var strResult = await tool.ExecuteAsync(args, context);
                            result = await policyProvider.AfterToolAsync(tool, args, strResult, context);
                            logger.LogDebug("Tool: {Name} Result: {result}", call.Name, result);
                        }
                        catch (Exception ex)
                        {
                            logger.LogError(ex, "Error: {Message}", ex.Message);
                            result = ToolErrorHandler.ToProblemDetailsJson(ex, call.Name);
                        }

                        msg.Contents.Add(new FunctionResultContent(call.CallId, result));
                    }
                    sessionState.MessageHistory.Add(msg); // Tool Result
                    yield return msg;
                }
            }
        }


        public async Task ConvertMediaFilesAsync(MessageContext context, ChatMessage message)
        {
            var msg = message.Text;

            if (msg.Length > 6 && msg[..4].Contains('['))
            {
                // Pattern mit 3-5 Großbuchstaben
                var match = JsonHelper.MediaTagRegex().Match(msg);
                logger.LogInformation(match.ToString());

                if (match.Success)
                {
                    string tag = match.Groups[1].Value;    // z.B. "IMAGE" oder "TTS"
                    string payload = WebUtility.HtmlDecode(match.Groups[2].Value.Trim()); // URL oder Text

                    logger.LogInformation("Media TAG: {tag} ,Payload: {payload}", tag, payload);
                    // Logik:
                    switch (tag)
                    {
                        case "TTS":
                            var ttsp = context.Provider.GetRequiredService<ITextToSpeechProvider>();

                            // 1. Stimme parsen: "[TTS:Hallo|voice:onyx]" -> "Hallo", "onyx"
                            string finalPayload = payload;
                            string selectedVoiceName = ""; // Default

                            if (payload.Contains("|voice:"))
                            {
                                var parts = payload.Split("|voice:", 2);
                                finalPayload = parts[0];
                                selectedVoiceName = parts[1];
                            }
                            else
                            {
                                // Fallback: Erste verfügbare Stimme nehmen, falls kein |voice: ... angegeben
                                var firstVoice = await ttsp.ListVoicesAsync().FirstAsync();
                                if (firstVoice != null) selectedVoiceName = firstVoice.VoiceName; // Statt VoiceName einfach .Id nehmen
                            }

                            var file = await pathHelper.SaveMediaFileAsync(await ttsp.TextToSpeechAsync(selectedVoiceName, finalPayload, new object()));
                            if (!string.IsNullOrWhiteSpace(file))
                            {
                                message.AdditionalProperties ??= [];
                                message.AdditionalProperties.Add("media_type", "voice");
                                message.AdditionalProperties.Add("media_url", pathHelper.GetMediaUrl(file));
                            }
                            break;
                        default:
                            message.AdditionalProperties ??= [];
                            message.AdditionalProperties.Add("media_type", tag.ToLowerInvariant());
                            var ft = await GetMediaFileAsync(payload);
                            message.AdditionalProperties.Add("media_url", ft?.ToString() ?? payload);
                            break;
                    }
                }
            }

            foreach (var item in message.Contents.OfType<UriContent>())
            {
                var f = await GetMediaFileAsync(item.Uri.ToString());
                if (f != null) item.Uri = f;
            }
        }

        private async Task<Uri?> GetMediaFileAsync(string data)
        {
            var file = await pathHelper.SaveMediaFileAsync(data);
            if (file != null) return pathHelper.GetMediaUrl(file);
            return null;
        }

        private async Task SetVFSAsync(ChatSessionState sessionState)
        {
            sessionState.VFS = await PathUtils.BuildVFSAsync(sessionState.Services);
        }
    }

    public class JsonSessionStorage
    {
        public ChatSession Session { get; set; } = default!;
        public List<ChatMessage> MessageHistory { get; set; } = [];
    }
}