using Baileys.Defaults;
using Baileys.Extensions;
using Baileys.Session;
using Baileys.Socket;
using Baileys.Types;
using Baileys.Utils;
using BlazorClaw.WhatsApp.Protocol;
using Microsoft.Extensions.Logging;
using Proto;
using MsILogger = Microsoft.Extensions.Logging.ILogger;

namespace BlazorClaw.WhatsApp
{
    /// <summary>
    /// WhatsApp Web Client with Baileys-style Noise XX handshake
    /// Completely rebuilt for proper protocol implementation
    /// </summary>
    public class WhatsAppClient : IAsyncDisposable
    {
        private readonly MsILogger? _logger;
        private readonly IAuthStateProvider _authStateProvider;
        private BaileysSocket? _socket;
        private bool autoReconnect = true;

        public event EventHandler<ConnectionUpdateEventArgs>? OnConnectionUpdate;
        public event EventHandler<QrCodeEventArgs>? OnQRCode;
        public event EventHandler<MessageReceiveEventArgs>? OnMessage;

        public WhatsAppClient(WhatsAppConfig config, MsILogger logger)
        {
            _logger = logger;
            _authStateProvider = new DirectoryAuthStateProvider(Path.Combine("whatsapp_auth", config.AccountId));
        }

        /// <summary>
        /// Initiates a connection to WhatsApp.
        /// </summary>
        public virtual async Task ConnectAsync(CancellationToken cancellationToken = default)
        {
            var authState = await _authStateProvider.LoadAuthStateAsync(cancellationToken: cancellationToken).ConfigureAwait(false);

            _socket = new BaileysSocket(authState, new MsLogger(_logger));
            _socket.ConnectionUpdate += Socket_ConnectionUpdate;
            _socket.MessageRecieved += Socket_MessageRecieved;
            await _socket.ConnectAsync(BaileysDefaults.WaWebSocketUrl, cancellationToken).ConfigureAwait(false);
        }

        private void Socket_MessageRecieved(object? sender, MessageRecieveEventArgs e)
        {
            OnMessage?.Invoke(this, new("", e.Node.GetString() ?? string.Empty));
        }

        private void Socket_ConnectionUpdate(object? sender, Baileys.Types.ConnectionUpdateEventArgs update)
        {
            OnConnectionUpdate?.Invoke(this, update);
            if (!string.IsNullOrWhiteSpace(update.Qr))
            {
                _logger?.LogInformation("📱 QR Code received: {Qr}", update.Qr);
                OnQRCode?.Invoke(this, new($"https://wa.me/settings/linked_devices#{update.Qr}"));
            }
            else if (update.Connection == WaConnectionState.Open)
            {
                _logger?.LogInformation("✅ Connected to WhatsApp!");
            }
            else if (update.Connection == WaConnectionState.Close)
            {
                _logger?.LogWarning("❌ Disconnected from WhatsApp.");
                if (autoReconnect)
                    _ = ConnectAsync();
            }
        }

        public virtual async ValueTask DisconnectAsync()
        {
            autoReconnect = false;
            if (_socket != null)
            {
                await _socket.DisposeAsync().ConfigureAwait(false);
                _socket = null;
            }
        }
        /// <summary>
        /// Send message to WhatsApp contact
        /// </summary>
        public async Task SendMessageAsync(string jid, string message, CancellationToken cancellationToken = default)
        {
            // TODO: Implement message sending with Binary Nodes
            _logger?.LogWarning("SendMessageAsync not yet implemented: {Jid} {Message}", jid, message);
            await Task.CompletedTask;
        }

        public ValueTask DisposeAsync()
        {
            return DisconnectAsync();
        }
    }
}

/// <summary>
/// WhatsApp Client Configuration
/// </summary>
public class WhatsAppConfig
{
    public string AccountId { get; set; } = "default";
    public string WebSocketUrl { get; set; } = "wss://web.whatsapp.com:5222/ws/chat";
    public string? PhoneNumber { get; set; }
    public string? PushName { get; set; }
}


public class QrCodeEventArgs : EventArgs
{
    public string QrData { get; }
    public QrCodeEventArgs(string qrData)
    {
        QrData = qrData;
    }
}


public class MessageReceiveEventArgs : EventArgs
{
    public string From { get; }
    public string Message { get; }
    public MessageReceiveEventArgs(string from, string message)
    {
        From = from;
        Message = message;
    }
}

