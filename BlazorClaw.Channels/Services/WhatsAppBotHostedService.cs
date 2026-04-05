using BlazorClaw.Core.DTOs;
using BlazorClaw.Core.Services;
using BlazorClaw.Core.Sessions;
using BlazorClaw.Core.Utils;
using BlazorClaw.WhatsApp;
using BlazorClaw.WhatsApp.Events;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;

namespace BlazorClaw.Channels.Services
{
    /// <summary>
    /// WhatsApp Channel - Multi-Account Hosted Service
    /// Manages multiple WhatsApp accounts via WhatsAppClient
    /// </summary>
    public class WhatsAppBotHostedService : IHostedService
    {
        private readonly IConfiguration _configuration;
        private readonly IMessageDispatcher _messageDispatcher;
        private readonly IServiceScopeFactory _scopeFactory;
        private readonly ILogger<WhatsAppBotHostedService> _logger;
        private readonly PathHelper _pathHelper;

        private readonly Dictionary<string, WhatsAppChannelBot> _bots = new();

        public WhatsAppBotHostedService(
            IConfiguration configuration,
            IMessageDispatcher messageDispatcher,
            IServiceScopeFactory scopeFactory,
            ILogger<WhatsAppBotHostedService> logger,
            PathHelper pathHelper)
        {
            _configuration = configuration;
            _messageDispatcher = messageDispatcher;
            _scopeFactory = scopeFactory;
            _logger = logger;
            _pathHelper = pathHelper;
        }

        public async Task StartAsync(CancellationToken cancellationToken)
        {
            _logger.LogInformation("WhatsApp Channel Service starting...");

            // Read accounts from config
            var accounts = _configuration.GetSection("Channels:WhatsApp:Accounts").GetChildren();

            foreach (var accountConfig in accounts)
            {
                var accountId = accountConfig.Key;
                var enabled = accountConfig.GetValue<bool>("Enabled", true);

                if (!enabled)
                {
                    _logger.LogInformation("WhatsApp account '{AccountId}' is disabled", accountId);
                    continue;
                }

                try
                {
                    await AddAccountAsync(accountId, accountConfig, cancellationToken);
                }
                catch (Exception ex)
                {
                    _logger.LogError(ex, "Failed to initialize WhatsApp account '{AccountId}'", accountId);
                }
            }

            _logger.LogInformation("WhatsApp Channel Service started with {Count} accounts", _bots.Count);
        }

        private async Task AddAccountAsync(
            string accountId,
            IConfigurationSection config,
            CancellationToken cancellationToken)
        {
            _logger.LogInformation("Initializing WhatsApp account '{AccountId}'...", accountId);

            var authDir = config.GetValue<string>("AuthDir") ?? $"./whatsapp_auth/{accountId}";
            var pushName = config.GetValue<string>("PushName") ?? "BlazorClaw";

            var whatsappConfig = new WhatsAppConfig
            {
                AuthDir = authDir,
                PushName = pushName
            };

            var client = new WhatsAppClient(whatsappConfig);
            var bot = new WhatsAppChannelBot(accountId, client, _logger, _pathHelper);

            // Register event handlers
            client.OnMessage += (sender, evt) =>
            {
                _ = bot.OnMessageReceivedAsync(
                    new ChannelSession(bot, evt.RemoteJid),
                    evt.Text);
            };

            client.OnQRCode += (sender, qr) =>
            {
                _logger.LogWarning("📱 WhatsApp QR Code for '{AccountId}':\n{QR}", accountId, qr);
                // TODO: Display QR in frontend
            };

            client.OnConnectionUpdate += (sender, evt) =>
            {
                _logger.LogInformation("WhatsApp '{AccountId}' connection: {Status}", accountId, evt.Status);
                if (!string.IsNullOrEmpty(evt.Error))
                {
                    _logger.LogError("WhatsApp '{AccountId}' error: {Error}", accountId, evt.Error);
                }
            };

            // Connect
            await client.ConnectAsync(cancellationToken);

            _bots[accountId] = bot;
            _messageDispatcher.Register(bot);

            _logger.LogInformation("WhatsApp account '{AccountId}' registered", accountId);
        }

        public async Task StopAsync(CancellationToken cancellationToken)
        {
            _logger.LogInformation("WhatsApp Channel Service stopping...");

            foreach (var (accountId, bot) in _bots)
            {
                try
                {
                    _messageDispatcher.Unregister(bot);
                    await bot.DisconnectAsync();
                    _logger.LogInformation("WhatsApp account '{AccountId}' stopped", accountId);
                }
                catch (Exception ex)
                {
                    _logger.LogError(ex, "Error stopping account '{AccountId}'", accountId);
                }
            }

            _bots.Clear();
            _logger.LogInformation("WhatsApp Channel Service stopped");
        }
    }

    /// <summary>
    /// WhatsApp channel handler - sends messages to WhatsApp
    /// </summary>
    public class WhatsAppChannelBot : AbstractChannelBot, IWhatsAppClient
    {
        private readonly string _accountId;
        private readonly WhatsAppClient _client;
        private readonly ILogger<WhatsAppBotHostedService> _logger;
        private readonly PathHelper _pathHelper;

        public string AccountId => _accountId;

        public WhatsAppChannelBot(
            string accountId,
            WhatsAppClient client,
            ILogger<WhatsAppBotHostedService> logger,
            PathHelper pathHelper)
            : base("WhatsApp")
        {
            _accountId = accountId;
            _client = client;
            _logger = logger;
            _pathHelper = pathHelper;
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
