using BlazorClaw.Core.Commands;
using BlazorClaw.Core.Data;
using BlazorClaw.Core.Sessions;
using Microsoft.AspNetCore.Identity;
using System.Collections.Concurrent;
using System.CommandLine;

namespace BlazorClaw.Server.Services
{
    public class MessageDispatcher(IServiceProvider serviceProvider, ILogger<MessageDispatcher> logger) : IMessageDispatcher
    {
        private readonly ConcurrentDictionary<string, Guid> _sessIds = [];

        public async Task<ChatSessionState?> GetSessionAsync(MessageContext context)
        {
            if (context?.Channel == null) return null;

            Guid? uid = !string.IsNullOrWhiteSpace(context.UserId) ? Guid.Parse(context.UserId) : null;
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
            var sm = serviceProvider.GetRequiredService<ISessionManager>();
            var session = await sm.GetOrCreateSessionAsync(uid.Value);
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
                var userManager = serviceProvider.GetRequiredService<UserManager<ApplicationUser>>();
                // Suche User via Provider "Telegram"
                var user = await userManager.FindByLoginAsync(channelSession.ChannelProvider, channelSession.SenderId);

                var cmdContext = new MessageContext
                {
                    UserId = user?.Id,
                    Channel = channelSession,
                    Provider = serviceProvider,
                };
                var session = await GetSessionAsync(cmdContext);
                cmdContext.Provider = session.Services;

                var sm = cmdContext.Provider.GetRequiredService<ISessionManager>();
                cmdContext.Session = session?.Session;

                if (message is string msgString)
                {
                    if (msgString.StartsWith('/'))
                    {
                        try
                        {
                            var rootCmd = await BuildRootCommand(cmdContext);
                            var ret = await sm.DispatchCommandAsync(msgString, cmdContext, rootCmd, cmdContext.Provider.GetRequiredService<ICommandProvider>());
                            if (ret != null)
                            {
                                var textRes = Convert.ToString(ret);
                                if (!string.IsNullOrWhiteSpace(textRes))
                                {
                                    logger.LogInformation("Sending command result to {ChannelProvider}:{ChannelId} : {Result}", cmdContext.Channel.ChannelProvider, cmdContext.Channel.ChannelId, ret);
                                    await cmdContext.Channel.SendMessageAsync(textRes);
                                }
                                return; // Command handled, exit early
                            }
                        }
                        catch (Exception ex)
                        {
                            logger.LogError(ex, "Error processing command '{Command}' for {ChannelProvider}:{ChannelId} : {Message}", msgString, cmdContext.Channel.ChannelProvider, cmdContext.Channel.ChannelId, ex.Message);
                            var textRes = $"Error processing command: {ex.Message}";
                            await cmdContext.Channel.SendMessageAsync(textRes);
                            return; // Exit early on command error
                        }
                    }

                    session.MessageHistory.Add(new() { Role = "user", Content = msgString });

                    await foreach (var msg in sm.DispatchToLLMAsync(session))
                    {
                        if (!msg.IsAssistant) continue;
                        var content = Convert.ToString(msg.Content);
                        if (!string.IsNullOrWhiteSpace(content))
                        {
                            logger.LogInformation("Sending reply to {ChannelProvider}:{ChannelId} : {content}", cmdContext.Channel.ChannelProvider, cmdContext.Channel.ChannelId, content);
                            await cmdContext.Channel.SendMessageAsync(content);
                        }
                    }
                }
            }
            catch (Exception ex)
            {
                await channelSession.SendMessageAsync($"Error: {ex.Message}");
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
