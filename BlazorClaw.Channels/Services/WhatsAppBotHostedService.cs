using BlazorClaw.Core.DTOs;
using BlazorClaw.Core.Sessions;
using BlazorClaw.WhatsApp;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;

namespace BlazorClaw.Channels.Services
{
    /// <summary>
    /// WhatsApp Channel - Multi-Account Hosted Service
    /// Manages multiple WhatsApp accounts via WhatsAppClient
    /// </summary>
    public class WhatsAppBotHostedService(
        IConfiguration configuration,
        IMessageDispatcher messageDispatcher,
        ILogger<WhatsAppBotHostedService> logger) : IHostedService
    {
        private readonly Dictionary<string, WhatsAppChannelBot> _bots = [];

        public async Task StartAsync(CancellationToken cancellationToken)
        {
            logger.LogInformation("WhatsApp Channel Service starting...");

            // Read accounts from config
            var accounts = configuration.GetSection("Channels:WhatsApp:Accounts").GetChildren();

            foreach (var accountConfig in accounts)
            {
                var accountId = accountConfig.Key;
                var enabled = accountConfig.GetValue<bool>("Enabled", true);

                if (!enabled)
                {
                    logger.LogInformation("WhatsApp account '{AccountId}' is disabled", accountId);
                    continue;
                }

                try
                {
                    await AddAccountAsync(accountId, accountConfig, cancellationToken);
                }
                catch (Exception ex)
                {
                    logger.LogError(ex, "Failed to initialize WhatsApp account '{AccountId}'", accountId);
                }
            }

            logger.LogInformation("WhatsApp Channel Service started with {Count} accounts", _bots.Count);
        }

        private async Task AddAccountAsync(
            string accountId,
            IConfigurationSection config,
            CancellationToken cancellationToken)
        {
            logger.LogInformation("Initializing WhatsApp account '{AccountId}'...", accountId);

            var authDir = config.GetValue<string>("AuthDir") ?? $"./whatsapp_auth/{accountId}";
            var pushName = config.GetValue<string>("PushName") ?? "BlazorClaw";

            var whatsappConfig = new WhatsAppConfig
            {
                AuthDir = authDir,
                PushName = pushName
            };

            var client = new WhatsAppClient(whatsappConfig);
            var bot = new WhatsAppChannelBot(accountId, client, logger);

            // Register event handlers
            client.OnMessage += (sender, evt) =>
            {
                _ = bot.OnMessageReceivedAsync(
                    new ChannelSession(bot, evt.RemoteJid),
                    evt.Text);
            };

            client.OnQRCode += (sender, qr) =>
            {
                logger.LogWarning("📱 WhatsApp QR Code for '{AccountId}':\n{QR}", accountId, qr);
                // TODO: Display QR in frontend
            };

            client.OnConnectionUpdate += (sender, evt) =>
            {
                logger.LogInformation("WhatsApp '{AccountId}' connection: {Status}", accountId, evt.Status);
                if (!string.IsNullOrEmpty(evt.Error))
                {
                    logger.LogError("WhatsApp '{AccountId}' error: {Error}", accountId, evt.Error);
                }
            };

            // Connect
            await client.ConnectAsync(cancellationToken);

            _bots[accountId] = bot;
            messageDispatcher.Register(bot);

            logger.LogInformation("WhatsApp account '{AccountId}' registered", accountId);
        }

        public async Task StopAsync(CancellationToken cancellationToken)
        {
            logger.LogInformation("WhatsApp Channel Service stopping...");

            foreach (var (accountId, bot) in _bots)
            {
                try
                {
                    messageDispatcher.Unregister(bot);
                    await bot.DisconnectAsync();
                    logger.LogInformation("WhatsApp account '{AccountId}' stopped", accountId);
                }
                catch (Exception ex)
                {
                    logger.LogError(ex, "Error stopping account '{AccountId}'", accountId);
                }
            }

            _bots.Clear();
            logger.LogInformation("WhatsApp Channel Service stopped");
        }
    }

    /// <summary>
    /// WhatsApp channel handler - sends messages to WhatsApp
    /// </summary>
    public class WhatsAppChannelBot : AbstractChannelBot, IWhatsAppClient
    {
        private readonly string _accountId;
        private readonly WhatsAppClient _client;
        private readonly ILogger _logger;

        public string AccountId => _accountId;

        public WhatsAppChannelBot(
            string accountId,
            WhatsAppClient client,
            ILogger logger)
            : base("WhatsApp")
        {
            _accountId = accountId;
            _client = client;
            _logger = logger;
        }

        public override async Task SendChannelAsync(
            IChannelSession channelId,
            ChatMessage message,
            CancellationToken cancellationToken = default)
        {
            try
            {
                var content = message.GetTextContent() ?? string.Empty;

                if (!string.IsNullOrWhiteSpace(content))
                {
                    await _client.SendMessageAsync(channelId.ChannelId, content, cancellationToken);
                }

                // TODO: Send images, media, etc.
            }
            catch (Exception ex)
            {
                throw new InvalidOperationException(
                    $"Failed to send WhatsApp message to {channelId.ChannelId}", ex);
            }
        }

        public override Task SendUserAsync(
            IChannelSession channelId,
            ChatMessage message,
            CancellationToken cancellationToken = default)
        {
            return SendChannelAsync(channelId, message, cancellationToken);
        }

        // IWhatsAppClient implementation
        public Task ConnectAsync(CancellationToken cancellationToken = default)
        {
            return _client.ConnectAsync(cancellationToken);
        }

        public Task DisconnectAsync(CancellationToken cancellationToken = default)
        {
            return _client.DisconnectAsync();
        }

        public Task SendMessage(string jid, object messageContent, CancellationToken cancellationToken = default)
        {
            if (messageContent is string text)
            {
                return _client.SendMessageAsync(jid, text, cancellationToken);
            }
            return Task.CompletedTask;
        }

        public Task SendReadReceipt(string jid, string messageId, CancellationToken cancellationToken = default)
        {
            // TODO: Implement read receipts
            return Task.CompletedTask;
        }
    }
}
