using BlazorClaw.Core.Commands;
using BlazorClaw.Core.Data;
using BlazorClaw.Core.Sessions;
using Microsoft.AspNetCore.Identity;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using System.Collections.Concurrent;
using System.CommandLine;
using Telegram.Bot;
using Telegram.Bot.Polling;
using Telegram.Bot.Types;

namespace BlazorClaw.Channels.Services
{
    public record TelegramBotInstance(string Id, string Token, TelegramBotClient Client, RootCommand Cmds);


    public class TelegramBotHostedService(IConfiguration configuration, IServiceProvider serviceProvider, ILogger<TelegramBotHostedService> logger) : IHostedService
    {
        private readonly List<TelegramBotInstance> _bots = [];
        public ConcurrentDictionary<string, Guid> sessIds = [];
        public Task StartAsync(CancellationToken cancellationToken)
        {
            var telegramConfigs = configuration.GetSection("Channels:Telegram").GetChildren();
            foreach (var botConfig in telegramConfigs)
            {
                var id = botConfig.Key; // Use Key (e.g. main/alerts)
                var token = botConfig["Token"];
                if (!string.IsNullOrEmpty(token))
                {
                    logger.LogInformation("Telegram Bot '{id}' registering ...", id);

                    var cmds = serviceProvider.GetRequiredService<ICommandProvider>();
                    RootCommand rootCommand = new("Commands for Telegram");
                    foreach (var cmd in cmds.GetCommands())
                    {
                        var command = cmd.GetCommand();
                        rootCommand.Add(command);
                    }

                    var client = new TelegramBotClient(token);
                    _bots.Add(new TelegramBotInstance(id, token, client, rootCommand));

                    var receiverOptions = new ReceiverOptions
                    {
                    };

                    var commands = cmds.GetCommands()
                        .Select(o => o.GetCommand())
                        .Select(c => new BotCommand { Command = c.Name.ToLower(), Description = c.Description ?? string.Empty }).ToArray();
                    client.SetMyCommands(commands, cancellationToken: cancellationToken);

                    client.StartReceiving(
                        updateHandler: HandleUpdateAsync,
                        errorHandler: HandlePollingErrorAsync,
                        receiverOptions: receiverOptions,
                        cancellationToken: cancellationToken
                    );
                }
            }
            return Task.CompletedTask;
        }

        private Task HandlePollingErrorAsync(ITelegramBotClient botClient, Exception exception, CancellationToken cancellationToken)
        {
            logger.LogError(exception, "Telegram Bot '{BotId}' error received: {Name} : {Message}", botClient.BotId, exception.GetType().Name, exception.Message);
            return Task.CompletedTask;
        }

        private async Task HandleUpdateAsync(ITelegramBotClient botClient, Update update, CancellationToken cancellationToken)
        {
            try
            {
                var inst = _bots.FirstOrDefault(b => b.Client.BotId == botClient.BotId);

                if (update.Message?.Text == null) return;
                logger.LogInformation("Telegram Bot '{BotId}' update received: {Message}", botClient.BotId, update.Message);

                using var scope = serviceProvider.CreateScope();
                var userManager = scope.ServiceProvider.GetRequiredService<UserManager<ApplicationUser>>();

                var telegramId = update.Message.From!.Id.ToString();
                logger.LogInformation("Looking up user for Telegram ID: {TelegramId}", telegramId);

                // Suche User via Provider "Telegram"
                var user = await userManager.FindByLoginAsync("Telegram", telegramId);

                var sm = scope.ServiceProvider.GetRequiredService<ISessionManager>();
                await botClient.SendChatAction(update.Message.Chat.Id, Telegram.Bot.Types.Enums.ChatAction.Typing, cancellationToken: cancellationToken);

                Guid? uid = user != null ? Guid.Parse(user.Id) : null;
                if (uid == null)
                {
                    if (!sessIds.TryGetValue(telegramId, out var existingUid))
                    {
                        uid = Guid.NewGuid();
                        sessIds[telegramId] = uid.Value;
                        logger.LogInformation("No user found for Telegram ID {TelegramId}. Assigned temporary session ID: {SessionId}", telegramId, uid);
                    }
                    else
                    {
                        uid = existingUid;
                        logger.LogInformation("No user found for Telegram ID {TelegramId}. Using existing temporary session ID: {SessionId}", telegramId, uid);
                    }
                }
                var sess = await sm.GetOrCreateSessionAsync(uid.Value);

                if (inst != null && update.Message.Text.StartsWith('/'))
                {
                    var commandText = update.Message.Text[1..].Split(' ')[0].ToLower(); // Get command without '/'
                    logger.LogInformation("Received command: {Command}", commandText);
                    var cmds = scope.ServiceProvider.GetRequiredService<ICommandProvider>();
                    var command = cmds.GetCommands()
                        .FirstOrDefault(o => o.GetCommand().Name.Equals(commandText, StringComparison.OrdinalIgnoreCase));

                    if (command != null)
                    {
                        var cmdContext = new CommandContext
                        {
                            UserId = user?.Id,
                            ChannelProvider = "Telegram",
                            ChannelId = telegramId,
                            Session = sess?.Session,
                            Provider = scope.ServiceProvider
                        };
                        logger.LogInformation("Executing command: {Command}", commandText);

                        var result = await cmds.ExecuteAsync(command, inst.Cmds.Parse(update.Message.Text[1..]), cmdContext);
                        var textRes = Convert.ToString(result);
                        if (!string.IsNullOrWhiteSpace(textRes))
                        {
                            logger.LogInformation("Sending command result to {ChatId}: {Result}", update.Message.Chat.Id, result);
                            await botClient.SendMessage(update.Message.Chat.Id, textRes, cancellationToken: cancellationToken);
                        }
                        return; // Command handled, exit early
                    }
                }

                sess.MessageHistory.Add(new() { Role = "user", Content = update.Message.Text });

                await foreach (var msg in sm.DispatchToLLMAsync(sess))
                {
                    if (!msg.IsAssistant) continue;
                    var content = Convert.ToString(msg.Content);
                    if (!string.IsNullOrWhiteSpace(content))
                    {
                        logger.LogInformation("Sending reply to {ChatId}: {Reply}", update.Message.Chat.Id, content);
                        await botClient.SendMessage(update.Message.Chat.Id, content, cancellationToken: cancellationToken);
                    }
                }
            }
            catch (Exception ex)
            {
                logger.LogError(ex, ex.Message);
            }
        }

        public Task StopAsync(CancellationToken cancellationToken) => Task.CompletedTask;
    }
}
