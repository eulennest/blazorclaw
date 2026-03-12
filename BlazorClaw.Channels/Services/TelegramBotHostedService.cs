using BlazorClaw.Core.Commands;
using BlazorClaw.Core.DTOs;
using BlazorClaw.Core.Sessions;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using System.CommandLine;
using Telegram.Bot;
using Telegram.Bot.Polling;
using Telegram.Bot.Types;

namespace BlazorClaw.Channels.Services
{
    public class TelegramBotHostedService(IConfiguration configuration, IMessageDispatcher md, IServiceScopeFactory scopeFactory, ILogger<TelegramBotHostedService> logger) : IHostedService
    {
        private readonly List<TelegramChannelBot> _bots = [];

        private readonly IServiceScope Scope = scopeFactory.CreateScope();

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

                    var cmds = Scope.ServiceProvider.GetRequiredService<ICommandProvider>();
                    RootCommand rootCommand = new("Commands for Telegram");
                    foreach (var cmd in cmds.GetCommands())
                    {
                        var command = cmd.GetCommand();
                        rootCommand.Add(command);
                    }

                    var client = new TelegramBotClient(token);
                    var bot = new TelegramChannelBot(client);
                    _bots.Add(bot);
                    md.Register(bot);
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
                if (inst == null || update.Message?.Text == null) return;
                var telegramId = update.Message.From!.Id.ToString();
                await botClient.SendChatAction(update.Message.Chat.Id, Telegram.Bot.Types.Enums.ChatAction.Typing, cancellationToken: cancellationToken);
                await inst.OnMessageReceivedAsync(new ChannelSession(inst, telegramId), update.Message.Text);
            }
            catch (Exception ex)
            {
                logger.LogError(ex, "Error: {Messsage}", ex.Message);
            }
        }

        public Task StopAsync(CancellationToken cancellationToken)
        {
            foreach (var bot in _bots)
            {
                md.Unregister(bot);
            }
            return Task.CompletedTask;
        }
    }


    public class TelegramChannelBot(TelegramBotClient Client) : AbstractChannelBot("Telegram")
    {
        internal TelegramBotClient Client { get; } = Client;
        public override Task SendChannelAsync(IChannelSession channelId, ChatMessage message, CancellationToken cancellationToken = default)
        {
            return Client.SendMessage(channelId.ChannelId, Convert.ToString(message.Content) ?? string.Empty, cancellationToken: cancellationToken);
        }

        public override Task SendUserAsync(IChannelSession channelId, ChatMessage message, CancellationToken cancellationToken = default)
        {
            return Client.SendMessage(channelId.ChannelId, Convert.ToString(message.Content) ?? string.Empty, cancellationToken: cancellationToken);
        }
    }

}