using Baileys.Types;
using BlazorClaw.Core.DTOs;
using BlazorClaw.Core.Sessions;
using BlazorClaw.WhatsApp;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace BlazorClaw.Channels.Services
{
    /// <summary>
    /// WhatsApp QR Code Data
    /// </summary>
    public record WhatsAppQRCodeData
    {
        public string AccountId { get; init; } = string.Empty;
        public string QRCode { get; init; } = string.Empty;
        public DateTime GeneratedAt { get; init; }
    }

    /// <summary>
    /// WhatsApp Channel - Multi-Account Hosted Service
    /// Manages multiple WhatsApp accounts via WhatsAppClient
    /// </summary>
    public class WhatsAppBotHostedService(
        IOptionsMonitor<BotConfigs<WhatsAppBotEntry>> configuration,
        IMessageDispatcher messageDispatcher,
        ILogger<WhatsAppBotHostedService> logger) : IHostedService
    {
        private readonly Dictionary<string, WhatsAppChannelBot> _bots = [];
        private readonly Dictionary<string, WhatsAppQRCodeData> _qrCodes = [];
        private readonly object _qrLock = new();

        public async Task StartAsync(CancellationToken cancellationToken)
        {
            logger.LogInformation("WhatsApp Channel Service starting...");

            // Register OnChange handler for config reload
            configuration.OnChange(async (newConfig) =>
            {
                logger.LogInformation("WhatsApp config changed — reloading bots...");
                await ReloadBotsAsync(newConfig);
            });

            // Initial load
            await LoadBotsAsync(configuration.CurrentValue, cancellationToken);

            logger.LogInformation("WhatsApp Channel Service started with {Count} accounts", _bots.Count);
        }

        private async Task LoadBotsAsync(BotConfigs<WhatsAppBotEntry> config, CancellationToken cancellationToken)
        {
            foreach (var accountConfig in config)
            {
                var accountId = accountConfig.Key;
                var enabled = accountConfig.Value.Enabled;

                if (!enabled)
                {
                    logger.LogInformation("WhatsApp account '{AccountId}' is disabled", accountId);
                    continue;
                }

                try
                {
                    await AddAccountAsync(accountId, accountConfig.Value, cancellationToken);
                }
                catch (Exception ex)
                {
                    logger.LogError(ex, "Failed to initialize WhatsApp account '{AccountId}'", accountId);
                }
            }
        }

        private async Task ReloadBotsAsync(BotConfigs<WhatsAppBotEntry> newConfig)
        {
            logger.LogInformation("Reloading WhatsApp bots...");

            // Find removed accounts
            var currentIds = _bots.Keys.ToList();
            var newIds = newConfig.Keys.ToList();
            var removedIds = currentIds.Except(newIds).ToList();

            // Remove old bots
            foreach (var accountId in removedIds)
            {
                await RemoveBotAsync(accountId);
            }

            // Add/update bots
            foreach (var accountConfig in newConfig)
            {
                var accountId = accountConfig.Key;
                var enabled = accountConfig.Value.Enabled;

                if (!enabled)
                {
                    if (_bots.ContainsKey(accountId))
                    {
                        await RemoveBotAsync(accountId);
                    }
                    continue;
                }

                if (!_bots.ContainsKey(accountId))
                {
                    try
                    {
                        await AddAccountAsync(accountId, accountConfig.Value, CancellationToken.None);
                    }
                    catch (Exception ex)
                    {
                        logger.LogError(ex, "Failed to add WhatsApp account '{AccountId}'", accountId);
                    }
                }
            }

            logger.LogInformation("WhatsApp bots reloaded — {Count} accounts active", _bots.Count);
        }

        private async Task RemoveBotAsync(string accountId)
        {
            if (!_bots.TryGetValue(accountId, out var bot))
                return;

            try
            {
                messageDispatcher.Unregister(bot);
                await bot.DisconnectAsync();
                _bots.Remove(accountId);

                lock (_qrLock)
                {
                    _qrCodes.Remove(accountId);
                }

                logger.LogInformation("WhatsApp account '{AccountId}' removed", accountId);
            }
            catch (Exception ex)
            {
                logger.LogError(ex, "Failed to remove WhatsApp account '{AccountId}'", accountId);
            }
        }

        private async Task AddAccountAsync(
            string accountId,
            WhatsAppBotEntry config,
            CancellationToken cancellationToken)
        {
            logger.LogInformation("Initializing WhatsApp account '{AccountId}'...", accountId);

            var authDir = $"./whatsapp_auth/{accountId}";
            var pushName = config.PushName ?? "BlazorClaw";

            var whatsappConfig = new WhatsAppConfig
            {
                PushName = pushName
            };

            var client = new WhatsAppClient(whatsappConfig, logger);
            var bot = new WhatsAppChannelBot(accountId, client, logger);

            bot.OnQRCode += (sender, e) =>
            {
                if (sender is not WhatsAppChannelBot wbot) return;
                logger.LogWarning("📱 WhatsApp QR Code for '{AccountId}':\n{QR}", wbot.AccountId, e.QrData);

                // Store QR code
                lock (_qrLock)
                {
                    _qrCodes[wbot.AccountId] = new WhatsAppQRCodeData
                    {
                        AccountId = wbot.AccountId,
                        QRCode = e.QrData,
                        GeneratedAt = DateTime.UtcNow
                    };
                }
            };

            client.OnConnectionUpdate += (sender, e) =>
            {
                if (sender is not WhatsAppChannelBot bot) return;
                var status = e.Connection?.ToString().ToLower();
                logger.LogInformation("WhatsApp '{AccountId}' connection: {Status}", bot.AccountId, status);

                // Clear QR code when connected
                if (status == "open" || status == "paired")
                {
                    lock (_qrLock)
                    {
                        _qrCodes.Remove(bot.AccountId);
                    }
                }
            };

            // Connect
            await client.ConnectAsync(cancellationToken);

            _bots[accountId] = bot;
            messageDispatcher.Register(bot);

            logger.LogInformation("WhatsApp account '{AccountId}' registered", accountId);
        }

        /// <summary>
        /// Get all current QR codes for all accounts
        /// </summary>
        public List<WhatsAppQRCodeData> GetCurrentQRCodes()
        {
            lock (_qrLock)
            {
                return _qrCodes.Values.ToList();
            }
        }

        /// <summary>
        /// Get QR code for specific account
        /// </summary>
        public WhatsAppQRCodeData? GetQRCode(string accountId)
        {
            lock (_qrLock)
            {
                return _qrCodes.GetValueOrDefault(accountId);
            }
        }

        /// <summary>
        /// Get total number of configured accounts
        /// </summary>
        public int GetAccountCount()
        {
            return _bots.Count;
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

            lock (_qrLock)
            {
                _qrCodes.Clear();
            }

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

        public event EventHandler<QrCodeEventArgs>? OnQRCode;

        public WhatsAppChannelBot(
            string accountId,
            WhatsAppClient client,
            ILogger logger)
            : base("WhatsApp")
        {
            _accountId = accountId;
            _client = client;
            _logger = logger;


            // Register event handlers
            client.OnMessage += Client_OnMessage;
            client.OnQRCode += Client_OnQRCode;
        }

        private void Client_OnQRCode(object? sender, QrCodeEventArgs e)
        {
            OnQRCode?.Invoke(this, e);
        }

        private async void Client_OnMessage(object? sender, MessageReceiveEventArgs e)
        {
            await OnMessageReceivedAsync(new ChannelSession(this, e.From), e.Message);
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

        public ValueTask DisconnectAsync(CancellationToken cancellationToken = default)
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
    public class WhatsAppBotEntry : BotEntry
    {
        public string? PhoneNumber { get; set; }
        public string? PushName { get; set; }
    }

}
