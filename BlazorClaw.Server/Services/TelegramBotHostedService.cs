using Microsoft.AspNetCore.Identity;
using Telegram.Bot;
using Telegram.Bot.Types;
using Telegram.Bot.Polling;
using BlazorClaw.Server.Data;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;

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

        public Task StartAsync(CancellationToken cancellationToken)
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
                    
                    var receiverOptions = new ReceiverOptions
                    {
                        AllowedUpdates = Array.Empty<Telegram.Bot.Types.Enums.UpdateType>() // receive all update types
                    };

                    client.StartReceiving(
                        updateHandler: async (bot, update, token) => await HandleUpdateAsync(bot, update, token),
                        pollingErrorHandler: async (bot, exception, token) => await HandlePollingErrorAsync(bot, exception, token),
                        receiverOptions: receiverOptions,
                        cancellationToken: cancellationToken
                    );
                }
            }
            return Task.CompletedTask;
        }

        private Task HandlePollingErrorAsync(ITelegramBotClient botClient, Exception exception, CancellationToken cancellationToken)
        {
            return Task.CompletedTask;
        }

        private async Task HandleUpdateAsync(ITelegramBotClient botClient, Update update, CancellationToken cancellationToken)
        {
            if (update.Message?.Text == null) return;

            using var scope = _serviceProvider.CreateScope();
            var userManager = scope.ServiceProvider.GetRequiredService<UserManager<ApplicationUser>>();
            
            var telegramId = update.Message.From!.Id.ToString();
            var user = await userManager.FindByLoginAsync("Telegram", telegramId);
            
            string reply = user != null 
                ? $"Hallo {user.FirstName}, ich habe dich erkannt!" 
                : "Ich kenne dich leider noch nicht. Bitte registriere dich.";

            await botClient.SendMessage(update.Message.Chat.Id, reply, cancellationToken: cancellationToken);
        }

        public Task StopAsync(CancellationToken cancellationToken) => Task.CompletedTask;
    }
}
