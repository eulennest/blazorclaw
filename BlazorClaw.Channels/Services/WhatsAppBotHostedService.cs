using BlazorClaw.Core.Commands;
using BlazorClaw.Core.DTOs;
using BlazorClaw.Core.Services;
using BlazorClaw.Core.Sessions;
using BlazorClaw.Core.Utils;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;

namespace BlazorClaw.Channels.Services
{
    /// <summary>
    /// WhatsApp Channel - Multi-Account Hosted Service
    /// Manages multiple WhatsApp accounts via Baileys.NET
    /// </summary>
    public class WhatsAppBotHostedService : IHostedService
    {
        private readonly IConfiguration _configuration;
        private readonly IMessageDispatcher _messageDispatcher;
        private readonly IServiceScopeFactory _scopeFactory;
        private readonly ILogger<WhatsAppBotHostedService> _logger;
        private readonly PathHelper _pathHelper;

        private readonly Dictionary<string, IWhatsAppClient> _clients = new();
        private readonly Dictionary<string, WhatsAppAccountConfig> _accounts = new();

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

            // Read all accounts from config
            var whatsappConfigs = _configuration.GetSection("Channels:WhatsApp:Accounts").GetChildren();

            foreach (var accountConfig in whatsappConfigs)
            {
                var accountId = accountConfig.Key;
                var config = new WhatsAppAccountConfig();
                accountConfig.Bind(config);

                if (!config.Enabled)
                {
                    _logger.LogInformation("WhatsApp account '{AccountId}' is disabled", accountId);
                    continue;
                }

                try
                {
                    await AddAccountAsync(accountId, config, cancellationToken);
                }
                catch (Exception ex)
                {
                    _logger.LogError(ex, "Failed to initialize WhatsApp account '{AccountId}'", accountId);
                }
            }

            _logger.LogInformation("WhatsApp Channel Service started with {Count} accounts", _clients.Count);
        }

        /// <summary>
        /// Add a new WhatsApp account dynamically
        /// </summary>
        public async Task AddAccountAsync(
            string accountId,
            WhatsAppAccountConfig config,
            CancellationToken cancellationToken = default)
        {
            if (_clients.ContainsKey(accountId))
                throw new InvalidOperationException($"Account '{accountId}' already exists");

            _logger.LogInformation("WhatsApp account '{AccountId}' initializing...", accountId);

            // Store config
            _accounts[accountId] = config;

            // TODO: Create Baileys client once .NET 10 is available
            // For now: placeholder interface
            // var client = new BaileysClient(new BaileysClientOptions { ... });

            // Create wrapper
            var bot = new WhatsAppChannelBot(accountId, config, _pathHelper);
            _clients[accountId] = bot;

            // Register event handlers
            RegisterEventHandlers(accountId, bot);

            // Register with dispatcher
            _messageDispatcher.Register(bot);

            _logger.LogInformation("WhatsApp account '{AccountId}' registered", accountId);

            // TODO: Connect to WhatsApp
            // await client.ConnectAsync(cancellationToken);
        }

        /// <summary>
        /// Remove a WhatsApp account
        /// </summary>
        public async Task RemoveAccountAsync(string accountId, CancellationToken cancellationToken = default)
        {
            if (!_clients.TryGetValue(accountId, out var client))
                throw new KeyNotFoundException($"Account '{accountId}' not found");

            _logger.LogInformation("WhatsApp account '{AccountId}' disconnecting...", accountId);

            // TODO: Disconnect
            // await client.DisconnectAsync(cancellationToken);

            _messageDispatcher.Unregister(client);
            _clients.Remove(accountId);
            _accounts.Remove(accountId);

            _logger.LogInformation("WhatsApp account '{AccountId}' removed", accountId);
        }

        /// <summary>
        /// Set account enabled/disabled
        /// </summary>
        public async Task SetAccountEnabledAsync(
            string accountId,
            bool enabled,
            CancellationToken cancellationToken = default)
        {
            if (!_accounts.TryGetValue(accountId, out var config))
                throw new KeyNotFoundException($"Account '{accountId}' not found");

            config.Enabled = enabled;

            if (enabled && !_clients.ContainsKey(accountId))
            {
                await AddAccountAsync(accountId, config, cancellationToken);
            }
            else if (!enabled && _clients.ContainsKey(accountId))
            {
                await RemoveAccountAsync(accountId, cancellationToken);
            }
        }

        /// <summary>
        /// Get account status
        /// </summary>
        public Dictionary<string, bool> GetAccountStatus()
        {
            return _accounts.ToDictionary(
                x => x.Key,
                x => _clients.ContainsKey(x.Key));
        }

        /// <summary>
        /// Register event handlers for a WhatsApp client
        /// </summary>
        private void RegisterEventHandlers(string accountId, IWhatsAppClient client)
        {
            // Message events
            client.OnMessageReceived += async (jid, msg) =>
            {
                try
                {
                    var session = new ChannelSession(client, jid);
                    await _messageDispatcher.DispatchAsync(session, msg);
                }
                catch (Exception ex)
                {
                    _logger.LogError(ex, "Error dispatching message from {Jid}", jid);
                }
            };

            // Presence events
            client.OnPresenceUpdate += (jid, presence) =>
            {
                _logger.LogDebug("Presence: {Jid} = {Status}", jid, presence);
            };

            // Connection events
            client.OnConnectionUpdate += (status) =>
            {
                _logger.LogInformation("Connection status: {Status}", status);
            };

            // QR code
            client.OnQRCode += (qr) =>
            {
                _logger.LogWarning("QR Code for account '{AccountId}': {QR}", accountId, qr);
            };
        }

        public async Task StopAsync(CancellationToken cancellationToken)
        {
            _logger.LogInformation("WhatsApp Channel Service stopping...");

            var accountIds = _clients.Keys.ToList();
            foreach (var accountId in accountIds)
            {
                try
                {
                    await RemoveAccountAsync(accountId, cancellationToken);
                }
                catch (Exception ex)
                {
                    _logger.LogError(ex, "Error stopping account '{AccountId}'", accountId);
                }
            }

            _logger.LogInformation("WhatsApp Channel Service stopped");
        }
    }

    /// <summary>
    /// WhatsApp channel handler - sends messages to WhatsApp
    /// </summary>
    public class WhatsAppChannelBot : AbstractChannelBot, IWhatsAppClient
    {
        private readonly string _accountId;
        private readonly WhatsAppAccountConfig _config;
        private readonly PathHelper _pathHelper;

        public string AccountId => _accountId;

        public event EventHandler<(string jid, string message)>? OnMessageReceived;
        public event EventHandler<(string jid, string presence)>? OnPresenceUpdate;
        public event EventHandler<string>? OnConnectionUpdate;
        public event EventHandler<string>? OnQRCode;

        public WhatsAppChannelBot(
            string accountId,
            WhatsAppAccountConfig config,
            PathHelper pathHelper)
            : base("WhatsApp")
        {
            _accountId = accountId;
            _config = config;
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

                // 1. Send images
                if (message.Images?.Count > 0)
                {
                    foreach (var img in message.Images)
                    {
                        await SendImageAsync(
                            channelId.ChannelId,
                            img.ImageUrl?.Url ?? string.Empty,
                            message.GetTextContent() ?? string.Empty,
                            cancellationToken);
                    }
                    content = null;
                }

                // 2. Send media (voice/video/document)
                if (message.MediaContent != null && !string.IsNullOrWhiteSpace(message.MediaContent.Url))
                {
                    await SendMediaAsync(
                        channelId.ChannelId,
                        message.MediaContent,
                        cancellationToken);
                }

                // 3. Send text
                if (!string.IsNullOrWhiteSpace(content))
                {
                    await SendTextAsync(channelId.ChannelId, content, cancellationToken);
                }
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

        private async Task SendTextAsync(string jid, string text, CancellationToken cancellationToken)
        {
            // TODO: Implement Baileys client.SendMessage()
            // await _client.SendMessage(jid, new { text });
        }

        private async Task SendImageAsync(
            string jid,
            string imageUrl,
            string caption,
            CancellationToken cancellationToken)
        {
            // TODO: Implement Baileys client.SendMessage() with image
            // var file = await GetMediaFileAsync(imageUrl);
            // await _client.SendMessage(jid, new { image = file, caption });
        }

        private async Task SendMediaAsync(
            string jid,
            MediaContent media,
            CancellationToken cancellationToken)
        {
            // TODO: Implement based on media type
            // switch (media.Type.ToLower()) {
            //     case "voice": await _client.SendMessage(jid, new { audio = ... }); break;
            //     case "video": await _client.SendMessage(jid, new { video = ... }); break;
            //     default: await _client.SendMessage(jid, new { document = ... }); break;
            // }
        }

        private async Task<object> GetMediaFileAsync(string url)
        {
            // TODO: Download and prepare media for WhatsApp
            var file = await _pathHelper.GetMediaFileAsync(url);
            return file?.Item1 ?? url;
        }

        // IWhatsAppClient implementation stubs
        public Task ConnectAsync(CancellationToken cancellationToken = default)
        {
            // TODO: Initialize Baileys client, start Noise protocol, display QR
            return Task.CompletedTask;
        }

        public Task DisconnectAsync(CancellationToken cancellationToken = default)
        {
            // TODO: Close WebSocket connection
            return Task.CompletedTask;
        }

        public Task SendMessage(string jid, object messageContent, CancellationToken cancellationToken = default)
        {
            // TODO: Delegate to Baileys client
            return Task.CompletedTask;
        }

        public Task SendReadReceipt(string jid, string messageId, CancellationToken cancellationToken = default)
        {
            // TODO: Send read receipt via Baileys
            return Task.CompletedTask;
        }
    }
}
