using BlazorClaw.Core.Data;
using BlazorClaw.Core.Sessions;
using Microsoft.AspNetCore.Identity;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using System.Collections.Concurrent;
using Telegram.Bot;
using Telegram.Bot.Polling;
using Telegram.Bot.Types;

namespace BlazorClaw.Channels.Services
{
    public record TelegramBotInstance(string Id, string Token, TelegramBotClient Client);


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

                    var client = new TelegramBotClient(token);
                    _bots.Add(new TelegramBotInstance(id, token, client));

                    var receiverOptions = new ReceiverOptions
                    {
                    };

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
