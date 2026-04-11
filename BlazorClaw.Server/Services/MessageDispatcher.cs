using BlazorClaw.Core.Commands;
using BlazorClaw.Core.Data;
using BlazorClaw.Core.DTOs;
using BlazorClaw.Core.Services;
using BlazorClaw.Core.Sessions;
using BlazorClaw.Core.Speech;
using Microsoft.AspNetCore.Identity;
using Microsoft.Extensions.AI;
using System.Collections.Concurrent;
using System.CommandLine;

namespace BlazorClaw.Server.Services
{
    public class MessageDispatcher(PathHelper pathHelper, IServiceScopeFactory scopeFactory, ILogger<MessageDispatcher> logger) : IMessageDispatcher
    {
        private readonly ConcurrentDictionary<string, Guid> _sessIds = [];
        private readonly IServiceScope Scope = scopeFactory.CreateScope();

        public async Task<ChatSessionState?> GetSessionAsync(MessageContext context)
        {
            if (context?.Channel == null) return null;

            Guid? uid = context.Channel.SessionId != Guid.Empty ? context.Channel.SessionId : null;
            uid ??= !string.IsNullOrWhiteSpace(context.UserId) ? Guid.Parse(context.UserId) : null;
            if (uid == null)
            {
                var ekey = $"{context.Channel.ChannelProvider}:{context.Channel.ChannelId}";
                if (_sessIds.TryGetValue(ekey, out var existingUid))
                {
                    uid = existingUid;
                    logger.LogInformation("No user ID in context. Using existing temporary session ID {SessionId} for channel {ChannelProvider}:{ChannelId}", uid, context.Channel.ChannelProvider, context.Channel.ChannelId);
                }
                else
                {
                    uid = Guid.NewGuid();
                    _sessIds[ekey] = uid.Value;
                    logger.LogInformation("No user ID in context. Assigned new temporary session ID {SessionId} for channel {ChannelProvider}:{ChannelId}", uid, context.Channel.ChannelProvider, context.Channel.ChannelId);
                }
            }
            var sm = Scope.ServiceProvider.GetRequiredService<ISessionManager>();
            var session = await sm.GetOrCreateSessionAsync(uid.Value);
            context.Channel.SessionId = session.Session.Id;
            return session;
        }

        public async Task<RootCommand> BuildRootCommand(MessageContext context)
        {
            var rootCommand = new RootCommand($"Commands for {context.Channel?.ChannelProvider}:{context.Channel?.ChannelId}");
            var cmds = context.Provider.GetRequiredService<ICommandProvider>();
            foreach (var cmd in cmds.GetCommands())
            {
                var command = cmd.GetCommand();
                rootCommand.Add(command);
            }
            return rootCommand;
        }


        public async Task DispatchMessageAsync(IChannelSession channelSession, object message)
        {
            try
            {
                var userManager = Scope.ServiceProvider.GetRequiredService<UserManager<ApplicationUser>>();
                // Suche User via Provider "Telegram"
                var user = await userManager.FindByLoginAsync(channelSession.ChannelProvider, channelSession.SenderId);
                if (user == null && Guid.TryParse(channelSession.SenderId, out var uid))
                    user = await userManager.FindByIdAsync(uid.ToString());

                var cmdContext = new MessageContext
                {
                    UserId = user?.Id,
                    Channel = channelSession,
                    Provider = Scope.ServiceProvider,
                };
                var session = await GetSessionAsync(cmdContext);
                cmdContext.Provider = session!.Services;

                var sm = cmdContext.Provider.GetRequiredService<ISessionManager>();
                cmdContext.Session = session?.Session;
                var mca = cmdContext.Provider.GetRequiredService<MessageContextAccessor>();
                mca.SetContext(cmdContext);
                if (message is Tuple<Stream, string> strm)
                {
                    var file = await pathHelper.SaveMediaFileAsync(strm) ?? throw new Exception("Can't save media data");
                    var uri = pathHelper.GetMediaUrl(file);
                    var sst = cmdContext.Provider.GetRequiredService<ISpeechToTextProvider>();
                    strm = await pathHelper.GetMediaFileAsync(file);
                    var transText = await sst.SpeechToTextAsync(strm.Item1, strm.Item2);

                    var newMsg = new ChatMessage(ChatRole.User, $"[VOICE:{uri}] Transcription:\n{transText}");
                    newMsg.Contents.Add(new UriContent(uri));
                    message = newMsg;
                }
                else if (message is string msgString)
                {
                    if (msgString.StartsWith('/'))
                    {
                        try
                        {
                            var rootCmd = await BuildRootCommand(cmdContext);
                            var ret = await sm.DispatchCommandAsync(msgString, cmdContext, rootCmd, cmdContext.Provider.GetRequiredService<ICommandProvider>());
                            var msg = new ChatMessage(new("command"), Convert.ToString(ret) ?? string.Empty);
                            if (ret != null)
                            {
                                var textRes = Convert.ToString(ret);
                                if (!string.IsNullOrWhiteSpace(textRes))
                                {
                                    logger.LogInformation("Sending command result to {ChannelProvider}:{ChannelId} : {Result}", cmdContext.Channel.ChannelProvider, cmdContext.Channel.ChannelId, ret);
                                    await cmdContext.Channel.SendUserAsync(msg);
                                }
                                return; // Command handled, exit early
                            }
                        }
                        catch (Exception ex)
                        {
                            logger.LogError(ex, "Error processing command '{Command}' for {ChannelProvider}:{ChannelId} : {Message}", msgString, cmdContext.Channel.ChannelProvider, cmdContext.Channel.ChannelId, ex.Message);
                            var textRes = $"Error processing command: {ex.Message}";
                            await cmdContext.Channel.SendUserAsync(new(new("error"), ex.Message));
                            return; // Exit early on command error
                        }
                    }
                    message = new ChatMessage(ChatRole.User, msgString);
                }

                if (message is ChatMessage chatMsg)
                {
                    session!.MessageHistory.Add(chatMsg);

                    await foreach (var msg in sm.DispatchToLLMAsync(session, cmdContext).ConfigureAwait(false))
                    {
                        if (cmdContext.Channel == null) continue;
                        var textContent = msg.Text;
                        if (!string.IsNullOrWhiteSpace(textContent))
                        {
                            textContent = textContent.Trim('`', ' ', '\r', '\n', '\t');
                            if ("NO_REPLY".Equals(textContent)) continue;
                            if ("HEARTBEAT_OK".Equals(textContent)) continue;
                        }
                        logger.LogInformation("Sending reply to {ChannelProvider}:{ChannelId} : {content}", cmdContext.Channel.ChannelProvider, cmdContext.Channel.ChannelId, textContent);
                        await cmdContext.Channel.SendChannelAsync(msg).ConfigureAwait(false);
                    }
                }
            }
            catch (Exception ex)
            {
                await channelSession.SendUserAsync(new(new("error"), ex.Message));
                logger.LogError(ex, "Error: {Messsage}", ex.Message);
            }
        }

        public void Register(IChannelBot bot)
        {
            bot.MessageReceived -= DispatchMessageAsync;
            bot.MessageReceived += DispatchMessageAsync;
        }

        public void Unregister(IChannelBot bot)
        {
            bot.MessageReceived -= DispatchMessageAsync;
        }
    }
}
