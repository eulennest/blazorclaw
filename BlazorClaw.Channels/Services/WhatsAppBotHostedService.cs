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
    /// Manages multiple WhatsApp accounts
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

            // Create wrapper
            var bot = new WhatsAppChannelBot(accountId, config, _logger, _pathHelper);
            _clients[accountId] = bot;

            // Register with dispatcher
            _messageDispatcher.Register(bot);

            _logger.LogInformation("WhatsApp account '{AccountId}' registered", accountId);

            // TODO: Connect to WhatsApp
            // await bot.ConnectAsync(cancellationToken);
        }

        /// <summary>
        /// Remove a WhatsApp account
        /// </summary>
        public async Task RemoveAccountAsync(string accountId, CancellationToken cancellationToken = default)
        {
            if (!_clients.TryGetValue(accountId, out var client))
                throw new KeyNotFoundException($"Account '{accountId}' not found");

            _logger.LogInformation("WhatsApp account '{AccountId}' disconnecting...", accountId);

            // Disconnect
            await client.DisconnectAsync(cancellationToken);

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
        private readonly ILogger<WhatsAppBotHostedService> _logger;
        private readonly PathHelper _pathHelper;

        public string AccountId => _accountId;

        public WhatsAppChannelBot(
            string accountId,
            WhatsAppAccountConfig config,
            ILogger<WhatsAppBotHostedService> logger,
            PathHelper pathHelper)
            : base("WhatsApp")
        {
            _accountId = accountId;
            _config = config;
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

                // Send text
                if (!string.IsNullOrWhiteSpace(content))
                {
                    await SendMessageAsync(channelId.ChannelId, content, cancellationToken);
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

        private async Task SendMessageAsync(string jid, string text, CancellationToken cancellationToken)
        {
            await SendMessage(jid, new { text }, cancellationToken);
        }

        // IWhatsAppClient implementation
        public Task ConnectAsync(CancellationToken cancellationToken = default)
        {
            // TODO: Connect via WhatsApp WebSocket + Noise Protocol
            return Task.CompletedTask;
        }

        public Task DisconnectAsync(CancellationToken cancellationToken = default)
        {
            // TODO: Disconnect
            return Task.CompletedTask;
        }

        public Task SendMessage(string jid, object messageContent, CancellationToken cancellationToken = default)
        {
            // TODO: Encrypt + send via WebSocket
            return Task.CompletedTask;
        }

        public Task SendReadReceipt(string jid, string messageId, CancellationToken cancellationToken = default)
        {
            // TODO: Send read receipt
            return Task.CompletedTask;
        }
    }
}
