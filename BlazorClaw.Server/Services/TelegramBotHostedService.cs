using BlazorClaw.Server.Data;
using Microsoft.AspNetCore.Identity;
using Telegram.Bot;
using Telegram.Bot.Polling;
using Telegram.Bot.Types;

namespace BlazorClaw.Server.Services
{
    public record TelegramBotInstance(string Id, string Token, TelegramBotClient Client);

    public class TelegramBotHostedService(IConfiguration configuration, IServiceProvider serviceProvider, ILogger<TelegramBotHostedService> logger) : IHostedService
    {
        private readonly List<TelegramBotInstance> _bots = [];

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
            if (update.Message?.Text == null) return;
            logger.LogInformation("Telegram Bot '{BotId}' update received: {Message}", botClient.BotId, update.Message);

            using var scope = serviceProvider.CreateScope();
            var userManager = scope.ServiceProvider.GetRequiredService<UserManager<ApplicationUser>>();

            var telegramId = update.Message.From!.Id.ToString();
            logger.LogInformation("Looking up user for Telegram ID: {TelegramId}", telegramId);

            // Suche User via Provider "Telegram"
            var user = await userManager.FindByLoginAsync("Telegram", telegramId);

            var sm = scope.ServiceProvider.GetRequiredService<ISessionManager>();

            var uid = user != null ? Guid.Parse(user.Id) : Guid.NewGuid();

            var sess = await sm.GetOrCreateSessionAsync(uid, "openrouter/google/gemini-3.1-flash-lite-preview");

            sess.MessageHistory.Add(new() { Role = "user", Content = update.Message.Text });

            await foreach (var msg in sm.DispatchToLLMAsync(sess))
            {
                var content = Convert.ToString(msg.Content);
                if (!string.IsNullOrWhiteSpace(content))
                {
                    logger.LogInformation("Sending reply to {ChatId}: {Reply}", update.Message.Chat.Id, content);
                    await botClient.SendMessage(update.Message.Chat.Id, content, cancellationToken: cancellationToken);
                }
            }
        }

        public Task StopAsync(CancellationToken cancellationToken) => Task.CompletedTask;
    }
}
