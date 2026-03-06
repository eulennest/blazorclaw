using Microsoft.AspNetCore.Identity;
using Telegram.Bot;
using Telegram.Bot.Types;
using BlazorClaw.Server.Data;
using Microsoft.Extensions.DependencyInjection;

namespace BlazorClaw.Server.Services
{
    public record TelegramBotInstance(string Id, string Token, TelegramBotClient Client);

    public class TelegramBotHostedService : IHostedService
    {
        private readonly List<TelegramBotInstance> _bots = new();
        private readonly IConfiguration _configuration;
        private readonly IServiceProvider _serviceProvider;

        public TelegramBotHostedService(IConfiguration configuration, IServiceProvider serviceProvider)
        {
            _configuration = configuration;
            _serviceProvider = serviceProvider;
        }

        public async Task StartAsync(CancellationToken cancellationToken)
        {
            var telegramConfigs = _configuration.GetSection("Channels:Telegram").GetChildren();
            foreach (var botConfig in telegramConfigs)
            {
                var id = botConfig["Id"] ?? "default";
                var token = botConfig["Token"];
                if (!string.IsNullOrEmpty(token))
                {
                    var client = new TelegramBotClient(token);
                    _bots.Add(new TelegramBotInstance(id, token, client));
                    
                    // Start Polling (non-blocking)
                    client.StartReceiving(
                        updateHandler: HandleUpdateAsync,
                        pollingErrorHandler: (c, e, t) => Task.CompletedTask,
                        cancellationToken: cancellationToken
                    );
                }
            }
        }

        private async Task HandleUpdateAsync(ITelegramBotClient botClient, Update update, CancellationToken cancellationToken)
        {
            var logger = _serviceProvider.GetRequiredService<ILogger<TelegramBotHostedService>>();
            logger.LogInformation("Telegram Update received: {Type}", update.Type);

            if (update.Message?.Text == null) return;

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
            await botClient.SendTextMessageAsync(update.Message.Chat.Id, reply, cancellationToken: cancellationToken);
        }

        public Task StopAsync(CancellationToken cancellationToken) => Task.CompletedTask;
    }
}
