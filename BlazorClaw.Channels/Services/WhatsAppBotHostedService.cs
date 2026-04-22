using BlazorClaw.Core.Sessions;
using BlazorClaw.WhatsApp;
using Microsoft.Extensions.AI;
using Microsoft.Extensions.Logging;

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
    /// WhatsApp channel handler - sends messages to WhatsApp
    /// </summary>
    public class WhatsAppChannelBot(ILogger<WhatsAppChannelBot> logger) : AbstractConfigChannelBot<WhatsAppBotEntry>("WhatsApp")
    {
        private WhatsAppClient? client;
        public WhatsAppQRCodeData? CurrentQRCode { get; private set; }

        public event EventHandler<QrCodeEventArgs>? OnQRCode;

        protected override ValueTask<bool> ConfigureAsync()
        {
            CurrentQRCode = null;

            if (client != null)
            {
                client.OnMessage -= Client_OnMessage;
                client.OnQRCode -= Client_OnQRCode;
                client.OnConnectionUpdate -= Client_OnConnectionUpdate;
            }

            var whatsappConfig = new WhatsAppConfig
            {
                AccountId = Key,
                PhoneNumber = Config?.PhoneNumber,
                PushName = Config?.PushName ?? "BlazorClaw"
            };

            client = new WhatsAppClient(whatsappConfig, logger);
            client.OnMessage += Client_OnMessage;
            client.OnQRCode += Client_OnQRCode;
            client.OnConnectionUpdate += Client_OnConnectionUpdate;
            return ValueTask.FromResult(true);
        }

        private void Client_OnQRCode(object? sender, QrCodeEventArgs e)
        {
            CurrentQRCode = new WhatsAppQRCodeData
            {
                AccountId = Key,
                QRCode = e.QrData,
                GeneratedAt = DateTime.UtcNow
            };
            logger.LogWarning("📱 WhatsApp QR Code for '{Key}':\n{QR}", Key, e.QrData);
            OnQRCode?.Invoke(this, e);
        }

        private void Client_OnConnectionUpdate(object? sender, Baileys.Types.ConnectionUpdateEventArgs e)
        {
            var status = e.Connection?.ToString().ToLowerInvariant();
            logger.LogInformation("WhatsApp '{Key}' connection: {Status}", Key, status);

            if (status == "open" || status == "paired")
            {
                CurrentQRCode = null;
            }
        }

        private async void Client_OnMessage(object? sender, WhatsApp.MessageReceiveEventArgs e)
        {
            OnMessageReceived(new ChannelSession(this, e.From), e.Message);
        }

        public override async Task StartAsync(CancellationToken cancellationToken = default)
        {
            if (client == null) throw new InvalidOperationException("Not configured");
            await client.ConnectAsync(cancellationToken);
        }

        public override async Task StopAsync(CancellationToken cancellationToken = default)
        {
            CurrentQRCode = null;
            if (client != null)
                await client.DisconnectAsync();
        }

        public override async Task SendChannelAsync(
            IChannelSession channelId,
            ChatMessage message,
            CancellationToken cancellationToken = default)
        {
            try
            {
                var content = message.Text;

                if (!string.IsNullOrWhiteSpace(content) && client != null)
                {
                    await client.SendMessageAsync(channelId.ChannelId, content, cancellationToken);
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
