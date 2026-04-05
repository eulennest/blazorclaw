using System.Net.WebSockets;
using System.Text.Json;
using BlazorClaw.WhatsApp.Protocol;
using BlazorClaw.WhatsApp.Events;

namespace BlazorClaw.WhatsApp
{
    /// <summary>
    /// WhatsApp Web Client - WebSocket-based implementation
    /// Reverse-engineered from Baileys TypeScript library
    /// </summary>
    public class WhatsAppClient : IDisposable
    {
        private readonly ClientWebSocket _webSocket = new();
        private readonly WhatsAppConfig _config;
        private readonly ILogger<WhatsAppClient> _logger;

        // State
        private WhatsAppAuthState? _authState;
        private NoiseProtocolHandler? _noiseHandler;
        private bool _isConnected;

        // Events
        public event EventHandler<MessageEvent>? OnMessage;
        public event EventHandler<PresenceEvent>? OnPresence;
        public event EventHandler<ConnectionEvent>? OnConnectionUpdate;
        public event EventHandler<string>? OnQRCode;

        public WhatsAppClient(WhatsAppConfig config, ILogger<WhatsAppClient> logger)
        {
            _config = config;
            _logger = logger;
        }

        /// <summary>
        /// Connect to WhatsApp Web servers
        /// </summary>
        public async Task ConnectAsync(CancellationToken cancellationToken = default)
        {
            try
            {
                _logger.LogInformation("Connecting to WhatsApp Web servers...");

                // 1. Load or initialize auth state
                _authState = await WhatsAppAuthState.LoadAsync(_config.AuthDir);

                // 2. Connect WebSocket
                await _webSocket.ConnectAsync(
                    new Uri(_config.WebSocketUrl),
                    cancellationToken);

                _isConnected = true;
                OnConnectionUpdate?.Invoke(this, new ConnectionEvent { Status = "open" });

                // 3. Initialize Noise protocol
                _noiseHandler = new NoiseProtocolHandler(_authState);

                // 4. Start message receiving loop
                _ = ReceiveLoopAsync(cancellationToken);

                _logger.LogInformation("Connected to WhatsApp!");
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Connection failed");
                _isConnected = false;
                OnConnectionUpdate?.Invoke(this, new ConnectionEvent { Status = "closed", Error = ex.Message });
                throw;
            }
        }

        /// <summary>
        /// Send a message
        /// </summary>
        public async Task SendMessageAsync(
            string jid,
            string text,
            CancellationToken cancellationToken = default)
        {
            if (!_isConnected)
                throw new InvalidOperationException("Not connected");

            // Build message
            var msg = new WhatsAppMessage
            {
                Key = new MessageKey { RemoteJid = jid, FromMe = true, Id = GenerateMessageId() },
                Message = new MessageContent { Conversation = text },
                MessageTimestamp = DateTimeOffset.UtcNow.ToUnixTimeSeconds()
            };

            await SendAsync(msg, cancellationToken);
        }

        /// <summary>
        /// Message receiving loop
        /// </summary>
        private async Task ReceiveLoopAsync(CancellationToken cancellationToken)
        {
            var buffer = new byte[4096];

            while (_isConnected && !cancellationToken.IsCancellationRequested)
            {
                try
                {
                    var result = await _webSocket.ReceiveAsync(
                        buffer,
                        cancellationToken);

                    if (result.MessageType == WebSocketMessageType.Close)
                    {
                        _isConnected = false;
                        OnConnectionUpdate?.Invoke(this, new ConnectionEvent { Status = "closed" });
                        break;
                    }

                    // Decrypt and parse message
                    var decrypted = await _noiseHandler!.DecryptAsync(buffer.Take(result.Count).ToArray());
                    await HandleIncomingMessageAsync(decrypted, cancellationToken);
                }
                catch (Exception ex)
                {
                    _logger.LogError(ex, "Error in receive loop");
                }
            }
        }

        /// <summary>
        /// Handle incoming message from WhatsApp
        /// </summary>
        private async Task HandleIncomingMessageAsync(byte[] data, CancellationToken cancellationToken)
        {
            try
            {
                // Parse JSON
                var json = System.Text.Encoding.UTF8.GetString(data);
                using var doc = JsonDocument.Parse(json);
                var root = doc.RootElement;

                // Route based on type
                var msgType = root.GetProperty("type").GetString();

                switch (msgType)
                {
                    case "messages":
                        HandleMessages(root);
                        break;
                    case "presence":
                        HandlePresence(root);
                        break;
                    case "qr":
                        HandleQRCode(root);
                        break;
                }
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error handling incoming message");
            }
        }

        private void HandleMessages(JsonElement elem)
        {
            var messages = elem.GetProperty("messages").EnumerateArray();
            foreach (var msg in messages)
            {
                var evt = new MessageEvent
                {
                    MessageId = msg.GetProperty("id").GetString() ?? string.Empty,
                    RemoteJid = msg.GetProperty("from").GetString() ?? string.Empty,
                    Text = msg.TryGetProperty("text", out var txt) ? txt.GetString() : string.Empty
                };

                OnMessage?.Invoke(this, evt);
            }
        }

        private void HandlePresence(JsonElement elem)
        {
            var presence = elem.GetProperty("presence").EnumerateArray();
            foreach (var p in presence)
            {
                var evt = new PresenceEvent
                {
                    Jid = p.GetProperty("id").GetString() ?? string.Empty,
                    Status = p.GetProperty("type").GetString() ?? "unavailable"
                };

                OnPresence?.Invoke(this, evt);
            }
        }

        private void HandleQRCode(JsonElement elem)
        {
            var qr = elem.GetProperty("qr").GetString();
            if (qr != null)
            {
                OnQRCode?.Invoke(this, qr);
            }
        }

        /// <summary>
        /// Send message via WebSocket
        /// </summary>
        private async Task SendAsync(WhatsAppMessage msg, CancellationToken cancellationToken)
        {
            var json = JsonSerializer.Serialize(msg);
            var encrypted = await _noiseHandler!.EncryptAsync(System.Text.Encoding.UTF8.GetBytes(json));

            await _webSocket.SendAsync(
                encrypted,
                WebSocketMessageType.Binary,
                true,
                cancellationToken);
        }

        private string GenerateMessageId()
        {
            return $"{DateTimeOffset.UtcNow.ToUnixTimeMilliseconds()}-{Random.Shared.Next()}";
        }

        public void Dispose()
        {
            _webSocket?.Dispose();
        }
    }

    /// <summary>
    /// WhatsApp Client Configuration
    /// </summary>
    public class WhatsAppConfig
    {
        public string WebSocketUrl { get; set; } = "wss://web.whatsapp.com/ws";
        public string AuthDir { get; set; } = "./whatsapp_auth";
        public string? PhoneNumber { get; set; }
        public bool PrintQRInTerminal { get; set; } = true;
    }

    /// <summary>
    /// WhatsApp Message
    /// </summary>
    public class WhatsAppMessage
    {
        public MessageKey? Key { get; set; }
        public MessageContent? Message { get; set; }
        public long MessageTimestamp { get; set; }
    }

    public class MessageKey
    {
        public string RemoteJid { get; set; } = string.Empty;
        public string Id { get; set; } = string.Empty;
        public bool FromMe { get; set; }
    }

    public class MessageContent
    {
        public string? Conversation { get; set; }
        public string? ExtendedTextMessage { get; set; }
        public object? ImageMessage { get; set; }
        public object? AudioMessage { get; set; }
    }
}
