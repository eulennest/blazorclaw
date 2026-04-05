using System.Net.WebSockets;
using Microsoft.Extensions.Logging;
using Google.Protobuf;
using BlazorClaw.WhatsApp.Protocol;
using BlazorClaw.WhatsApp.Events;
using BlazorClaw.WhatsApp.Crypto;

namespace BlazorClaw.WhatsApp
{
    /// <summary>
    /// WhatsApp Web Client - Protobuf + WebSocket implementation
    /// Based on Baileys.js architecture
    /// Reference: https://github.com/WhiskeySockets/Baileys
    /// </summary>
    public class WhatsAppClient : IDisposable
    {
        private readonly ClientWebSocket _webSocket = new();
        private readonly WhatsAppConfig _config;
        private readonly ILogger<WhatsAppClient>? _logger;

        // State
        private WhatsAppAuthState? _authState;
        private NoiseProtocolHandler? _noiseHandler;
        private bool _isConnected;
        private uint _epoch = 1;

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
        /// Connect to WhatsApp Web servers and perform handshake
        /// </summary>
        public async Task ConnectAsync(CancellationToken cancellationToken = default)
        {
            try
            {
                _logger?.LogInformation("Connecting to WhatsApp Web...");

                // 1. Load auth state
                _authState = await WhatsAppAuthState.LoadAsync(_config.AuthDir);

                // 2. Connect WebSocket
                await _webSocket.ConnectAsync(new Uri(_config.WebSocketUrl), cancellationToken);
                _logger?.LogInformation("WebSocket connected");

                _isConnected = true;
                OnConnectionUpdate?.Invoke(this, new ConnectionEvent { Status = "connecting" });

                // 3. Initialize Noise handler
                var (ephemeralPub, ephemeralPriv) = CryptoUtils.GenerateCurve25519Keypair();
                _noiseHandler = new NoiseProtocolHandler(_authState);

                // 4. Perform handshake
                await PerformHandshakeAsync(ephemeralPub, ephemeralPriv, cancellationToken);

                // 5. Save auth state
                await _authState.SaveAsync(_config.AuthDir);

                // 6. Start receive loop
                _ = ReceiveLoopAsync(cancellationToken);

                OnConnectionUpdate?.Invoke(this, new ConnectionEvent { Status = "open" });
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
        /// Perform Noise_XX handshake with WhatsApp servers
        /// Follows Baileys validateConnection() pattern
        /// </summary>
        private async Task PerformHandshakeAsync(
            byte[] ephemeralPub,
            byte[] ephemeralPriv,
            CancellationToken cancellationToken)
        {
            try
            {
                _logger?.LogInformation("Starting Noise handshake...");

                // 1. Build ClientHello with ephemeral public key
                var clientHello = new Proto.HandshakeMessage
                {
                    ClientHello = new Proto.HandshakeMessage.Types.ClientHello
                    {
                        Ephemeral = ByteString.CopyFrom(ephemeralPub)
                    }
                };

                // 2. Send ClientHello
                var helloBytes = clientHello.ToByteArray();
                await SendRawAsync(helloBytes, cancellationToken);
                _logger?.LogDebug("Sent ClientHello ({Length} bytes)", helloBytes.Length);

                // 3. Receive ServerHello
                var serverHelloData = await ReceiveRawAsync(cancellationToken);
                _logger?.LogWarning("Received ServerHello: {Length} bytes, Hex: {Hex}", serverHelloData.Length, BitConverter.ToString(serverHelloData.Take(64).ToArray()));
                var serverHello = Proto.HandshakeMessage.Parser.ParseFrom(serverHelloData);

                if (serverHello.ServerHello == null)
                    throw new InvalidOperationException("No ServerHello in response");

                _logger?.LogDebug("Received ServerHello");

                // 4. Extract server ephemeral key
                var serverEphemeral = serverHello.ServerHello.Ephemeral.ToByteArray();
                var serverStatic = serverHello.ServerHello.Static.ToByteArray();

                // 5. Compute shared secret (ECDH)
                var sharedSecret = CryptoUtils.Curve25519SharedSecret(ephemeralPriv, serverEphemeral);

                // 6. Derive encryption keys (HKDF)
                var keys = CryptoUtils.HkdfSha256(
                    sharedSecret,
                    null,
                    System.Text.Encoding.UTF8.GetBytes("WhatsApp Noise Protocol"),
                    64);

                _authState!.SendKey = [.. keys.Take(32)];
                _authState.ReceiveKey = [.. keys.Skip(32).Take(32)];

                // 7. Build ClientPayload
                var clientPayload = new Proto.ClientPayload
                {
                    Username = 0, // Will be set by WhatsApp
                    Passive = false,
                    UserAgent = new Proto.ClientPayload.Types.UserAgent
                    {
                        Platform = Proto.ClientPayload.Types.UserAgent.Types.Platform.Web,
                        AppVersion = new Proto.ClientPayload.Types.UserAgent.Types.AppVersion
                        {
                            Primary = 2,
                            Secondary = 3000,
                            Tertiary = 1029496320
                        }
                    },
                    WebInfo = new Proto.ClientPayload.Types.WebInfo
                    {
                        RefToken = "",
                        Version = "2.3000.1029496320",
                        WebSubPlatform = Proto.ClientPayload.Types.WebInfo.Types.WebSubPlatform.WebBrowser
                    },
                    PushName = _config.PushName ?? "BlazorClaw",
                    SessionId = -1,
                    ShortConnect = true,
                    ConnectType = Proto.ClientPayload.Types.ConnectType.WifiUnknown,
                    ConnectReason = Proto.ClientPayload.Types.ConnectReason.UserActivated
                };

                // 8. Encrypt payload
                var payloadBytes = clientPayload.ToByteArray();
                var encryptedPayload = CryptoUtils.AesGcmEncrypt(
                    payloadBytes,
                    _authState.SendKey!,
                    CryptoUtils.DeriveNonce(0));

                // 9. Compute static key encryption (noise key)
                byte[] keyEnc;
                if (_authState.NoiseKey == null || _authState.NoiseKey.Length == 0)
                {
                    // Generate new noise key on first connection
                    var (noisePub, noisePriv) = CryptoUtils.GenerateCurve25519Keypair();
                    _authState.NoiseKey = noisePriv;
                    _authState.NoiseKeyPublic = noisePub;
                    keyEnc = noisePub;
                }
                else
                {
                    keyEnc = _authState.NoiseKeyPublic!;
                }

                // 10. Send ClientFinish
                var clientFinish = new Proto.HandshakeMessage
                {
                    ClientFinish = new Proto.HandshakeMessage.Types.ClientFinish
                    {
                        Static = ByteString.CopyFrom(keyEnc),
                        Payload = ByteString.CopyFrom(encryptedPayload)
                    }
                };

                await SendRawAsync(clientFinish.ToByteArray(), cancellationToken);
                _logger?.LogInformation("✅ Noise handshake complete");
            }
            catch (Exception ex)
            {
                _logger?.LogError(ex, "Handshake failed");
                throw;
            }
        }

        /// <summary>
        /// Send raw bytes over WebSocket (unencrypted)
        /// </summary>
        private async Task SendRawAsync(byte[] data, CancellationToken cancellationToken)
        {
            if (!_webSocket.State.HasFlag(WebSocketState.Open))
                throw new InvalidOperationException("WebSocket not open");

            await _webSocket.SendAsync(data, WebSocketMessageType.Binary, true, cancellationToken);
        }

        /// <summary>
        /// Receive raw bytes from WebSocket
        /// </summary>
        private async Task<byte[]> ReceiveRawAsync(CancellationToken cancellationToken)
        {
            using var ms = new MemoryStream();
            var buffer = new byte[8192];
            WebSocketReceiveResult result;

            do
            {
                result = await _webSocket.ReceiveAsync(buffer, cancellationToken);

                if (result.MessageType == WebSocketMessageType.Close)
                    throw new InvalidOperationException("WebSocket closed during receive");

                ms.Write(buffer, 0, result.Count);
            }
            while (!result.EndOfMessage);

            return ms.ToArray();
        }

        /// <summary>
        /// Message receiving loop
        /// </summary>
        private async Task ReceiveLoopAsync(CancellationToken cancellationToken)
        {
            while (_isConnected && !cancellationToken.IsCancellationRequested)
            {
                try
                {
                    var frameData = await ReceiveRawAsync(cancellationToken);

                    // Decrypt frame (after handshake, all frames are encrypted)
                    if (_authState?.ReceiveKey != null)
                    {
                        var nonce = CryptoUtils.DeriveNonce(_epoch++);
                        var decrypted = CryptoUtils.AesGcmDecrypt(frameData, _authState.ReceiveKey, nonce);

                        await HandleIncomingFrameAsync(decrypted, cancellationToken);
                    }
                    else
                    {
                        await HandleIncomingFrameAsync(frameData, cancellationToken);
                    }
                }
                catch (WebSocketException)
                {
                    _isConnected = false;
                    OnConnectionUpdate?.Invoke(this, new ConnectionEvent { Status = "closed" });
                    break;
                }
                catch (Exception ex)
                {
                    _logger?.LogError(ex, "Error in receive loop");
                }
            }
        }

        /// <summary>
        /// <summary>
        /// Handle incoming frame (protobuf or binary node)
        /// </summary>
        private async Task HandleIncomingFrameAsync(byte[] data, CancellationToken cancellationToken)
        {
            try
            {
                // Try Binary Node first (most common)
                var node = BinaryNodeCodec.Decode(data);
                _logger?.LogDebug("Node: <{Tag}>", node.Tag);

                switch (node.Tag)
                {
                    case "iq" when node.Attrs.GetValueOrDefault("type") == "set" 
                                && node.GetChild("pair-device") != null:
                        await HandlePairDeviceNodeAsync(node, cancellationToken);
                        break;

                    case "success":
                        await HandleSuccessNodeAsync(node, cancellationToken);
                        break;

                    case "message":
                        HandleMessageBinaryNode(node);
                        break;

                    case "presence":
                        HandlePresenceBinaryNode(node);
                        break;

                    case "stream:error":
                    case "failure":
                        _logger?.LogError("Error node: {Node}", node);
                        OnConnectionUpdate?.Invoke(this, new ConnectionEvent
                        {
                            Status = "closed",
                            Error = node.Content?.ToString() ?? "Unknown"
                        });
                        break;

                    default:
                        _logger?.LogDebug("Unhandled: {Node}", node);
                        break;
                }
            }
            catch
            {
                // Fallback: Try Protobuf
                try
                {
                    var msg = Proto.WebMessageInfo.Parser.ParseFrom(data);
                    if (msg?.Message != null)
                    {
                        OnMessage?.Invoke(this, new MessageEvent
                        {
                            MessageId = msg.Key?.Id ?? "",
                            RemoteJid = msg.Key?.RemoteJid ?? "",
                            Text = msg.Message.Conversation ?? "",
                            Timestamp = (long)msg.MessageTimestamp
                        });
                    }
                }
                catch
                {
                    _logger?.LogDebug("Unknown frame ({Length}b)", data.Length);
                }
            }
        }

        // ========== Binary Node Handlers ==========

        private async Task HandlePairDeviceNodeAsync(BinaryNode node, CancellationToken ct)
        {
            _logger?.LogInformation("📱 Received pair-device request");

            // Send ACK
            await SendBinaryNodeAsync(new BinaryNode("iq", new Dictionary<string, string>
            {
                ["to"] = "s.whatsapp.net",
                ["type"] = "result",
                ["id"] = node.Attrs.GetValueOrDefault("id", "")
            }), ct);

            // Extract QR refs
            var pairNode = node.GetChild("pair-device");
            var refs = pairNode?.GetChildren("ref") ?? new();

            if (refs.Count == 0 || _authState?.NoiseKeyPublic == null) return;

            var noiseB64 = Convert.ToBase64String(_authState.NoiseKeyPublic);
            var identB64 = Convert.ToBase64String(_authState.IdentityPublicKey ?? new byte[32]);
            var advB64 = Convert.ToBase64String(new byte[32]);

            foreach (var r in refs)
            {
                if (r.Content is byte[] refBytes)
                {
                    var refStr = System.Text.Encoding.UTF8.GetString(refBytes);
                    var qr = $"{refStr},{noiseB64},{identB64},{advB64}";
                    _logger?.LogWarning("📱 QR: {QR}", qr);
                    OnQRCode?.Invoke(this, qr);
                    break;
                }
            }
        }

        private async Task HandleSuccessNodeAsync(BinaryNode node, CancellationToken ct)
        {
            _logger?.LogInformation("✅ Login successful!");
            OnConnectionUpdate?.Invoke(this, new ConnectionEvent { Status = "open" });
            await Task.CompletedTask;
        }

        private void HandleMessageBinaryNode(BinaryNode node)
        {
            OnMessage?.Invoke(this, new MessageEvent
            {
                MessageId = node.Attrs.GetValueOrDefault("id", ""),
                RemoteJid = node.Attrs.GetValueOrDefault("from", ""),
                Text = node.Content?.ToString() ?? ""
            });
        }

        private void HandlePresenceBinaryNode(BinaryNode node)
        {
            OnPresence?.Invoke(this, new PresenceEvent
            {
                Jid = node.Attrs.GetValueOrDefault("from", ""),
                Status = node.Attrs.GetValueOrDefault("type", "unavailable")
            });
        }

        private async Task SendBinaryNodeAsync(BinaryNode node, CancellationToken ct)
        {
            var nodeBytes = BinaryNodeCodec.Encode(node);
            if (_authState?.SendKey != null)
            {
                var nonce = CryptoUtils.DeriveNonce(_epoch++);
                var encrypted = CryptoUtils.AesGcmEncrypt(nodeBytes, _authState.SendKey, nonce);
                await SendRawAsync(encrypted, ct);
            }
            else
            {
                await SendRawAsync(nodeBytes, ct);
            }
        }
        /// Send a text message
        /// </summary>
        public async Task SendMessageAsync(
            string jid,
            string text,
            CancellationToken cancellationToken = default)
        {
            if (!_isConnected)
                throw new InvalidOperationException("Not connected");

            if (_authState?.SendKey == null)
                throw new InvalidOperationException("Handshake not complete");

            // Build message
            var message = new Proto.Message { Conversation = text };
            var key = new Proto.MessageKey
            {
                RemoteJid = jid,
                FromMe = true,
                Id = GenerateMessageId()
            };

            var msgInfo = new Proto.WebMessageInfo
            {
                Key = key,
                Message = message,
                MessageTimestamp = (ulong)DateTimeOffset.UtcNow.ToUnixTimeSeconds()
            };

            // Serialize + encrypt
            var msgBytes = msgInfo.ToByteArray();
            var nonce = CryptoUtils.DeriveNonce(_epoch++);
            var encrypted = CryptoUtils.AesGcmEncrypt(msgBytes, _authState.SendKey, nonce);

            // Send
            await SendRawAsync(encrypted, cancellationToken);
            _logger?.LogDebug("Message sent to {Jid}", jid);
        }

        private string GenerateMessageId()
        {
            return $"{DateTimeOffset.UtcNow.ToUnixTimeMilliseconds()}-{Random.Shared.Next()}";
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
        public string WebSocketUrl { get; set; } = "wss://web.whatsapp.com/ws/chat";
        public string AuthDir { get; set; } = "./whatsapp_auth";
        public string? PhoneNumber { get; set; }
        public string? PushName { get; set; }
    }
}
