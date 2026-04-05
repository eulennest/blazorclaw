using System.Net.WebSockets;
using System.Text.Json;
using Microsoft.Extensions.Logging;
using BlazorClaw.WhatsApp.Protocol;
using BlazorClaw.WhatsApp.Events;
using BlazorClaw.WhatsApp.Crypto;

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
        private readonly ILogger<WhatsAppClient>? _logger;

        // State
        private WhatsAppAuthState? _authState;
        private NoiseProtocolHandler? _noiseHandler;
        private SignalProtocolHandler? _signalHandler;
        private bool _isConnected;
        private uint _messageCounter;

        // Events
        public event EventHandler<MessageEvent>? OnMessage;
        public event EventHandler<PresenceEvent>? OnPresence;
        public event EventHandler<ConnectionEvent>? OnConnectionUpdate;
        public event EventHandler<string>? OnQRCode;

        public WhatsAppClient(WhatsAppConfig config, ILogger<WhatsAppClient>? logger = null)
        {
            _config = config;
            _logger = logger;
        }

        /// <summary>
        /// Connect to WhatsApp Web servers and perform Noise Protocol handshake
        /// </summary>
        public async Task ConnectAsync(CancellationToken cancellationToken = default)
        {
            try
            {
                _logger?.LogInformation("Connecting to WhatsApp Web servers...");

                // 1. Load or initialize auth state
                _authState = await WhatsAppAuthState.LoadAsync(_config.AuthDir);

                // 2. Connect WebSocket
                await ConnectWebSocketAsync(cancellationToken);

                _isConnected = true;
                OnConnectionUpdate?.Invoke(this, new ConnectionEvent { Status = "open" });

                // 3. Initialize Noise Protocol
                _noiseHandler = new NoiseProtocolHandler(_authState);

                // 4. Perform Noise handshake
                await PerformNoiseHandshakeAsync(cancellationToken);

                // 5. Initialize Signal Protocol for E2E encryption
                _signalHandler = await SignalProtocolHandler.InitializeSessionAsync(
                    "whatsapp_user",
                    cancellationToken);

                // 6. Save auth state
                await _authState.SaveAsync(_config.AuthDir);

                // 7. Start message receiving loop
                _ = ReceiveLoopAsync(cancellationToken);

                _logger?.LogInformation("✅ Connected to WhatsApp!");
            }
            catch (Exception ex)
            {
                _logger?.LogError(ex, "Connection failed");
                _isConnected = false;
                OnConnectionUpdate?.Invoke(this, new ConnectionEvent { Status = "closed", Error = ex.Message });
                throw;
            }
        }

        /// <summary>
        /// Connect to WhatsApp WebSocket endpoint
        /// </summary>
        private async Task ConnectWebSocketAsync(CancellationToken cancellationToken)
        {
            try
            {
                _logger?.LogDebug("Connecting WebSocket to {Url}", _config.WebSocketUrl);
                await _webSocket.ConnectAsync(
                    new Uri(_config.WebSocketUrl),
                    cancellationToken);
                _logger?.LogDebug("WebSocket connected");
            }
            catch (Exception ex)
            {
                _logger?.LogError(ex, "WebSocket connection failed");
                throw;
            }
        }

        /// <summary>
        /// Perform Noise_XX_25519_AESGCM_SHA256 handshake
        /// Establishes encryption keys with WhatsApp servers
        /// </summary>
        private async Task PerformNoiseHandshakeAsync(CancellationToken cancellationToken)
        {
            try
            {
                _logger?.LogInformation("Starting Noise Protocol handshake...");

                // 1. Generate ephemeral keypair for handshake
                var (clientPubKey, clientPrivKey) = CryptoUtils.GenerateCurve25519Keypair();
                _authState!.ClientPublicKey = clientPubKey;
                _authState.ClientPrivateKey = clientPrivKey;

                // 2. Send client hello (public key)
                var clientHello = new
                {
                    clientHello = new
                    {
                        ephemeral = Convert.ToBase64String(clientPubKey),
                        static_text = "",
                        payload = ""
                    }
                };

                var helloJson = JsonSerializer.Serialize(clientHello);
                await SendRawAsync(helloJson, cancellationToken);
                _logger?.LogDebug("Sent client hello");

                // 3. Receive server hello
                var serverHelloBuffer = new byte[4096];
                var result = await _webSocket.ReceiveAsync(serverHelloBuffer, cancellationToken);

                if (result.MessageType == WebSocketMessageType.Close)
                {
                    throw new InvalidOperationException("WebSocket closed during handshake");
                }

                var serverHelloJson = System.Text.Encoding.UTF8.GetString(
                    serverHelloBuffer, 0, result.Count);
                _logger?.LogDebug("Received server hello");

                // 4. Parse server response (extract server public key, compute shared secret)
                using var doc = JsonDocument.Parse(serverHelloJson);
                var serverEphemeral = doc.RootElement.GetProperty("serverHello")
                    .GetProperty("ephemeral").GetString();

                if (serverEphemeral == null)
                    throw new InvalidOperationException("No server ephemeral key in handshake");

                var serverPubKey = Convert.FromBase64String(serverEphemeral);

                // 5. Compute shared secret (ECDH)
                var sharedSecret = CryptoUtils.Curve25519SharedSecret(clientPrivKey, serverPubKey);
                _logger?.LogDebug("Computed shared secret via ECDH");

                // 6. Derive encryption keys via HKDF
                var derivedKeys = CryptoUtils.HkdfSha256(
                    sharedSecret,
                    null,
                    System.Text.Encoding.UTF8.GetBytes("WhatsApp"),
                    64);

                _authState.SendKey = derivedKeys.Take(32).ToArray();
                _authState.ReceiveKey = derivedKeys.Skip(32).Take(32).ToArray();

                _logger?.LogInformation("✅ Noise handshake complete");
            }
            catch (Exception ex)
            {
                _logger?.LogError(ex, "Noise handshake failed");
                throw;
            }
        }

        /// <summary>
        /// Send raw WebSocket message (unencrypted)
        /// </summary>
        private async Task SendRawAsync(string text, CancellationToken cancellationToken)
        {
            var bytes = System.Text.Encoding.UTF8.GetBytes(text);
            await _webSocket.SendAsync(bytes, WebSocketMessageType.Text, true, cancellationToken);
        }

        /// <summary>
        /// Message receiving loop
        /// </summary>
        private async Task ReceiveLoopAsync(CancellationToken cancellationToken)
        {
            var buffer = new byte[8192];

            while (_isConnected && !cancellationToken.IsCancellationRequested)
            {
                try
                {
                    var result = await _webSocket.ReceiveAsync(buffer, cancellationToken);

                    if (result.MessageType == WebSocketMessageType.Close)
                    {
                        _isConnected = false;
                        OnConnectionUpdate?.Invoke(this, new ConnectionEvent { Status = "closed" });
                        break;
                    }

                    // Decrypt message
                    var messageData = buffer.Take(result.Count).ToArray();
                    var decrypted = await DecryptMessageAsync(messageData);
                    await HandleIncomingMessageAsync(decrypted, cancellationToken);
                }
                catch (Exception ex)
                {
                    _logger?.LogError(ex, "Error in receive loop");
                }
            }
        }

        /// <summary>
        /// Decrypt incoming message using Signal Protocol
        /// </summary>
        private async Task<byte[]> DecryptMessageAsync(byte[] ciphertext)
        {
            if (_signalHandler == null)
                throw new InvalidOperationException("Signal handler not initialized");

            return await _signalHandler.DecryptAsync(ciphertext);
        }

        /// <summary>
        /// Handle incoming message from WhatsApp
        /// </summary>
        private async Task HandleIncomingMessageAsync(byte[] data, CancellationToken cancellationToken)
        {
            try
            {
                var json = System.Text.Encoding.UTF8.GetString(data);
                using var doc = JsonDocument.Parse(json);
                var root = doc.RootElement;

                if (!root.TryGetProperty("type", out var typeElem))
                    return;

                var msgType = typeElem.GetString();

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
                _logger?.LogError(ex, "Error handling incoming message");
            }
        }

        private void HandleMessages(JsonElement elem)
        {
            if (!elem.TryGetProperty("messages", out var messagesElem))
                return;

            foreach (var msg in messagesElem.EnumerateArray())
            {
                var evt = new MessageEvent
                {
                    MessageId = msg.TryGetProperty("id", out var id) ? id.GetString() ?? string.Empty : string.Empty,
                    RemoteJid = msg.TryGetProperty("from", out var from) ? from.GetString() ?? string.Empty : string.Empty,
                    Text = msg.TryGetProperty("text", out var txt) ? txt.GetString() : string.Empty
                };

                OnMessage?.Invoke(this, evt);
            }
        }

        private void HandlePresence(JsonElement elem)
        {
            if (!elem.TryGetProperty("presence", out var presenceElem))
                return;

            foreach (var p in presenceElem.EnumerateArray())
            {
                var evt = new PresenceEvent
                {
                    Jid = p.TryGetProperty("id", out var id) ? id.GetString() ?? string.Empty : string.Empty,
                    Status = p.TryGetProperty("type", out var type) ? type.GetString() ?? "unavailable" : "unavailable"
                };

                OnPresence?.Invoke(this, evt);
            }
        }

        private void HandleQRCode(JsonElement elem)
        {
            if (elem.TryGetProperty("qr", out var qr))
            {
                var qrString = qr.GetString();
                if (qrString != null)
                {
                    _logger?.LogWarning("📱 QR Code: {QR}", qrString);
                    OnQRCode?.Invoke(this, qrString);
                }
            }
        }

        /// <summary>
        /// Send a message to a contact
        /// </summary>
        public async Task SendMessageAsync(
            string jid,
            string text,
            CancellationToken cancellationToken = default)
        {
            if (!_isConnected)
                throw new InvalidOperationException("Not connected to WhatsApp");

            if (_signalHandler == null)
                throw new InvalidOperationException("Signal handler not initialized");

            _messageCounter++;

            // Build message
            var msg = new WhatsAppMessage
            {
                Key = new MessageKey
                {
                    RemoteJid = jid,
                    FromMe = true,
                    Id = GenerateMessageId()
                },
                Message = new MessageContent { Conversation = text },
                MessageTimestamp = DateTimeOffset.UtcNow.ToUnixTimeSeconds()
            };

            // Serialize + encrypt
            var json = JsonSerializer.Serialize(msg);
            var plaintext = System.Text.Encoding.UTF8.GetBytes(json);
            var ciphertext = await _signalHandler.EncryptAsync(plaintext);

            // Send via WebSocket
            await _webSocket.SendAsync(ciphertext, WebSocketMessageType.Binary, true, cancellationToken);

            _logger?.LogDebug("Message sent to {Jid}", jid);
        }

        private string GenerateMessageId()
        {
            return $"{DateTimeOffset.UtcNow.ToUnixTimeMilliseconds()}-{_messageCounter}";
        }

        /// <summary>
        /// Disconnect from WhatsApp
        /// </summary>
        public async Task DisconnectAsync()
        {
            if (_webSocket.State == WebSocketState.Open)
            {
                await _webSocket.CloseAsync(
                    WebSocketCloseStatus.NormalClosure,
                    "User disconnected",
                    CancellationToken.None);
            }
            _isConnected = false;
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
