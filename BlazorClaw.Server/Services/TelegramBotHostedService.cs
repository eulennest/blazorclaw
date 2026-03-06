using BlazorClaw.Server.Data;
using Microsoft.AspNetCore.Identity;
using Telegram.Bot;
using Telegram.Bot.Polling;
using Telegram.Bot.Types;

namespace BlazorClaw.Server.Services
{
    public record TelegramBotInstance(string Id, string Token, TelegramBotClient Client);

    public class TelegramBotHostedService : IHostedService
    {
        private readonly List<TelegramBotInstance> _bots = new();
        private readonly IConfiguration _configuration;
        private readonly IServiceProvider _serviceProvider;
        private readonly ILogger<TelegramBotHostedService> logger;

        public TelegramBotHostedService(IConfiguration configuration, IServiceProvider serviceProvider, ILogger<TelegramBotHostedService> logger)
        {
            _configuration = configuration;
            _serviceProvider = serviceProvider;
            this.logger = logger;
        }

        public Task StartAsync(CancellationToken cancellationToken)
        {
            var telegramConfigs = _configuration.GetSection("Channels:Telegram").GetChildren();
            foreach (var botConfig in telegramConfigs)
            {
                var id = botConfig["Id"] ?? "default";
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

            using var scope = _serviceProvider.CreateScope();
            var userManager = scope.ServiceProvider.GetRequiredService<UserManager<ApplicationUser>>();

            var telegramId = update.Message.From!.Id.ToString();
            logger.LogInformation("Looking up user for Telegram ID: {TelegramId}", telegramId);

            // Suche User via Provider "Telegram"
            var user = await userManager.FindByLoginAsync("Telegram", telegramId);

            string reply = user != null
                ? $"Hallo {user.FirstName}, ich habe dich erkannt!"
                : "Ich kenne dich leider noch nicht. Bitte registriere dich.";

            logger.LogInformation("Sending reply to {ChatId}: {Reply}", update.Message.Chat.Id, reply);
            await botClient.SendMessage(update.Message.Chat.Id, reply, cancellationToken: cancellationToken);
        }

        public Task StopAsync(CancellationToken cancellationToken) => Task.CompletedTask;
    }
}
