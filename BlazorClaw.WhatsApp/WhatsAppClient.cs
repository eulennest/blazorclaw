using System.Net.WebSockets;
using System.Security.Cryptography;
using BlazorClaw.WhatsApp.Crypto;
using BlazorClaw.WhatsApp.Protocol;
using Google.Protobuf;
using Microsoft.Extensions.Logging;

namespace BlazorClaw.WhatsApp
{
    /// <summary>
    /// WhatsApp Web Client with Baileys-style Noise XX handshake
    /// Completely rebuilt for proper protocol implementation
    /// </summary>
    public class WhatsAppClient : IDisposable
    {
        private readonly ClientWebSocket _webSocket = new();
        private readonly WhatsAppConfig _config;
        private readonly ILogger? _logger;
        
        private WhatsAppAuthState? _authState;
        private NoiseHandler? _noise;
        private bool _handshakeComplete;
        private bool _isConnected;
        private bool _sentIntro;

        public event Action<string, string>? OnConnectionUpdate;
        public event Action<string, string, string>? OnQRCode;
        public event Action<string, string, string>? OnMessage;

        public WhatsAppClient(WhatsAppConfig config, ILogger? logger = null)
        {
            _config = config;
            _logger = logger;
        }

        /// <summary>
        /// Connect and perform full Noise XX handshake
        /// </summary>
        public async Task ConnectAsync(CancellationToken cancellationToken = default)
        {
            try
            {
                _logger?.LogInformation("Connecting to WhatsApp Web...");

                // 1. Load or create auth state
                _authState = await WhatsAppAuthState.LoadAsync(_config.AuthDir);

                // 2. Generate ephemeral keypair for this session
                var (ephemeralPub, ephemeralPriv) = CryptoUtils.GenerateCurve25519Keypair();

                // 3. Get or generate noise keypair (long-term identity)
                var (noisePub, noisePriv) = GetOrGenerateNoiseKey();
                
                // 4. Initialize Noise handler
                _noise = new NoiseHandler(noisePriv, noisePub);

                // 5. Set WebSocket headers (matching real browser)
                _webSocket.Options.SetRequestHeader("Origin", "https://web.whatsapp.com");
                _webSocket.Options.SetRequestHeader("User-Agent", "Mozilla/5.0 (Windows NT 10.0; Win64; x64) AppleWebKit/537.36 (KHTML, like Gecko) Chrome/146.0.0.0 Safari/537.36");
                _webSocket.Options.SetRequestHeader("Accept-Encoding", "gzip, deflate, br, zstd");
                _webSocket.Options.SetRequestHeader("Accept-Language", "de-DE,de;q=0.9");
                _webSocket.Options.SetRequestHeader("Cache-Control", "no-cache");
                _webSocket.Options.SetRequestHeader("Pragma", "no-cache");
                
                var sessionId = Guid.NewGuid().ToString();
                _webSocket.Options.SetRequestHeader("Cookie", $"wa_ul={sessionId}; wa_web_lang_pref=de_DE");

                // 6. Connect WebSocket
                await _webSocket.ConnectAsync(new Uri(_config.WebSocketUrl), cancellationToken);
                _logger?.LogInformation("WebSocket connected");
                _isConnected = true;

                // 7. Perform Noise handshake
                await PerformNoiseHandshakeAsync(ephemeralPub, ephemeralPriv, cancellationToken);

                // 8. Start receive loop
                _ = Task.Run(() => ReceiveLoopAsync(cancellationToken), cancellationToken);

                _logger?.LogInformation("✅ Connected to WhatsApp!");
                OnConnectionUpdate?.Invoke(_config.AccountId, "open");
            }
            catch (Exception ex)
            {
                _logger?.LogError(ex, "Connection failed");
                OnConnectionUpdate?.Invoke(_config.AccountId, "close");
                throw;
            }
        }

        /// <summary>
        /// Full Noise XX handshake following Baileys pattern
        /// </summary>
        private async Task PerformNoiseHandshakeAsync(byte[] ephemeralPub, byte[] ephemeralPriv, CancellationToken cancellationToken)
        {
            try
            {
                _logger?.LogInformation("Starting Noise handshake...");

                // === STEP 1: Send ClientHello ===
                
                // Mix noise header and our public key into hash chain
                _noise!.MixHash(_noise.IntroHeader);
                _noise.MixHash(_noise.PublicKey);

                // Build ClientHello protobuf
                var clientHello = new Proto.HandshakeMessage
                {
                    ClientHello = new Proto.HandshakeMessage.Types.ClientHello
                    {
                        Ephemeral = ByteString.CopyFrom(ephemeralPub)
                    }
                };

                var helloBytes = clientHello.ToByteArray();
                var framedHello = EncodeFrame(helloBytes, sendIntro: true);
                
                await SendRawAsync(framedHello, cancellationToken);
                _logger?.LogDebug("Sent ClientHello ({Length} bytes)", framedHello.Length);

                // === STEP 2: Receive ServerHello ===
                
                var serverHelloFrame = await ReceiveRawAsync(cancellationToken);
                var (_, serverHelloBytes) = DecodeFrame(serverHelloFrame);
                
                var serverHello = Proto.HandshakeMessage.Parser.ParseFrom(serverHelloBytes);
                if (serverHello.ServerHello == null)
                    throw new InvalidOperationException("No ServerHello in response");

                _logger?.LogDebug("Received ServerHello");

                // Extract server components
                var serverEphemeral = serverHello.ServerHello.Ephemeral.ToByteArray();
                var serverStaticCiphertext = serverHello.ServerHello.Static.ToByteArray();
                var serverPayloadCiphertext = serverHello.ServerHello.Payload.ToByteArray();

                // Mix in server ephemeral key
                _noise.MixHash(serverEphemeral);
                
                // Compute ECDH: our ephemeral private × server ephemeral public
                var sharedEphemeral = CryptoUtils.Curve25519SharedSecret(ephemeralPriv, serverEphemeral);
                _noise.MixIntoKey(sharedEphemeral);

                // Decrypt server static key
                var serverStatic = _noise.Decrypt(serverStaticCiphertext);
                _noise.MixHash(serverStatic);
                
                // Compute ECDH: our ephemeral private × server static public
                var sharedStatic = CryptoUtils.Curve25519SharedSecret(ephemeralPriv, serverStatic);
                _noise.MixIntoKey(sharedStatic);

                // Decrypt cert chain (skip parsing for now - not critical for connection)
                var certBytes = _noise.Decrypt(serverPayloadCiphertext);
                // var certChain = Proto.CertChain.Parser.ParseFrom(certBytes); // TODO: Add CertChain to proto
                
                _logger?.LogDebug("Received cert payload ({Length} bytes)", certBytes.Length);

                // === STEP 3: Send ClientFinish ===

                // Encrypt our long-term noise public key
                var encryptedNoiseKey = _noise.Encrypt(_noise.PublicKey);

                // Build ClientPayload
                var clientPayload = new Proto.ClientPayload
                {
                    Username = 0, // Will be set by WhatsApp after pairing
                    Passive = false,
                    UserAgent = new Proto.ClientPayload.Types.UserAgent
                    {
                        Platform = Proto.ClientPayload.Types.UserAgent.Types.Platform.Web,
                        AppVersion = new Proto.ClientPayload.Types.UserAgent.Types.AppVersion
                        {
                            Primary = 2,
                            Secondary = 3000,
                            Tertiary = 1035194821 // Current Baileys version
                        }
                    },
                    WebInfo = new Proto.ClientPayload.Types.WebInfo
                    {
                        RefToken = "",
                        Version = "2.3000.1035194821",
                        WebSubPlatform = Proto.ClientPayload.Types.WebInfo.Types.WebSubPlatform.WebBrowser
                    },
                    PushName = _config.PushName ?? "BlazorClaw",
                    SessionId = -1,
                    ShortConnect = true,
                    ConnectType = Proto.ClientPayload.Types.ConnectType.WifiUnknown,
                    ConnectReason = Proto.ClientPayload.Types.ConnectReason.UserActivated
                };

                var payloadBytes = clientPayload.ToByteArray();
                var encryptedPayload = _noise.Encrypt(payloadBytes);

                // Build ClientFinish
                var clientFinish = new Proto.HandshakeMessage
                {
                    ClientFinish = new Proto.HandshakeMessage.Types.ClientFinish
                    {
                        Static = ByteString.CopyFrom(encryptedNoiseKey),
                        Payload = ByteString.CopyFrom(encryptedPayload)
                    }
                };

                var finishBytes = clientFinish.ToByteArray();
                var framedFinish = EncodeFrame(finishBytes, sendIntro: false);
                
                await SendRawAsync(framedFinish, cancellationToken);
                _logger?.LogDebug("Sent ClientFinish");

                // Finalize noise (switch to transport mode with derived keys)
                _noise.Finish();
                _handshakeComplete = true;

                _logger?.LogInformation("✅ Noise handshake complete");
            }
            catch (Exception ex)
            {
                _logger?.LogError(ex, "Handshake failed");
                throw;
            }
        }

        /// <summary>
        /// Get or generate long-term noise keypair
        /// </summary>
        private (byte[] Public, byte[] Private) GetOrGenerateNoiseKey()
        {
            if (_authState!.NoiseKey != null && _authState.NoiseKey.Length > 0 &&
                _authState.NoiseKeyPublic != null && _authState.NoiseKeyPublic.Length > 0)
            {
                return (_authState.NoiseKeyPublic, _authState.NoiseKey);
            }

            var (pub, priv) = CryptoUtils.GenerateCurve25519Keypair();
            _authState!.NoiseKey = priv;
            _authState.NoiseKeyPublic = pub;
            
            // Save immediately
            _authState.SaveAsync(_config.AuthDir).Wait();
            
            return (pub, priv);
        }

        /// <summary>
        /// Encode frame with 3-byte length prefix (+ optional intro header)
        /// </summary>
        private byte[] EncodeFrame(byte[] data, bool sendIntro)
        {
            using var ms = new MemoryStream();

            // On first frame, prepend intro header (WA magic bytes)
            if (sendIntro && !_sentIntro)
            {
                ms.Write(_noise!.IntroHeader);
                _sentIntro = true;
            }

            // Write 3-byte length (big-endian)
            ms.WriteByte((byte)(data.Length >> 16));
            ms.WriteByte((byte)(data.Length >> 8));
            ms.WriteByte((byte)data.Length);

            // Write data
            ms.Write(data, 0, data.Length);

            return ms.ToArray();
        }

        /// <summary>
        /// Decode frame (extract data from 3-byte length prefix)
        /// </summary>
        private (int Length, byte[] Data) DecodeFrame(byte[] buffer)
        {
            if (buffer.Length < 3)
                throw new InvalidOperationException("Buffer too small for frame header");

            var length = (buffer[0] << 16) | (buffer[1] << 8) | buffer[2];

            if (buffer.Length < length + 3)
                throw new InvalidOperationException($"Buffer too small for frame (expected {length + 3}, got {buffer.Length})");

            var data = new byte[length];
            Array.Copy(buffer, 3, data, 0, length);

            return (length, data);
        }

        /// <summary>
        /// Send raw bytes over WebSocket
        /// </summary>
        private async Task SendRawAsync(byte[] data, CancellationToken cancellationToken)
        {
            if (!_webSocket.State.HasFlag(WebSocketState.Open))
                throw new InvalidOperationException("WebSocket not open");

            _logger?.LogWarning("[SEND] {Length} bytes, Hex: {Hex}", data.Length, BitConverter.ToString(data.Take(64).ToArray()));
            await _webSocket.SendAsync(data, WebSocketMessageType.Binary, true, cancellationToken);
        }

        /// <summary>
        /// Receive complete WebSocket frame (handle fragmentation)
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

            var data = ms.ToArray();
            _logger?.LogWarning("[RECV] {Length} bytes, Type: {Type}, Hex: {Hex}", data.Length, result.MessageType, BitConverter.ToString(data.Take(64).ToArray()));
            return data;
        }

        /// <summary>
        /// Message receive loop (after handshake)
        /// </summary>
        private async Task ReceiveLoopAsync(CancellationToken cancellationToken)
        {
            while (_isConnected && !cancellationToken.IsCancellationRequested)
            {
                try
                {
                    var frameRaw = await ReceiveRawAsync(cancellationToken);

                    // Decode frame
                    var (frameLength, frameData) = DecodeFrame(frameRaw);
                    _logger?.LogDebug("Received frame: {Length} bytes", frameLength);

                    // Decrypt frame (after handshake, all frames are encrypted)
                    if (_handshakeComplete && _noise != null)
                    {
                        var decrypted = _noise.Decrypt(frameData);
                        await HandleIncomingFrameAsync(decrypted, cancellationToken);
                    }
                    else
                    {
                        await HandleIncomingFrameAsync(frameData, cancellationToken);
                    }
                }
                catch (InvalidOperationException ex) when (ex.Message.Contains("WebSocket closed"))
                {
                    _logger?.LogInformation("WebSocket closed");
                    _isConnected = false;
                    OnConnectionUpdate?.Invoke(_config.AccountId, "close");
                    break;
                }
                catch (Exception ex)
                {
                    _logger?.LogError(ex, "Error in receive loop");
                    _isConnected = false;
                    OnConnectionUpdate?.Invoke(_config.AccountId, "close");
                    break;
                }
            }
        }

        /// <summary>
        /// Handle incoming Binary Node (post-handshake messages)
        /// </summary>
        private async Task HandleIncomingFrameAsync(byte[] data, CancellationToken cancellationToken)
        {
            try
            {
                // Decode binary node
                var node = BinaryNodeCodec.Decode(data);
                _logger?.LogDebug("Received node: {Tag}", node.Tag);

                // Handle different node types
                switch (node.Tag)
                {
                    case "iq":
                        await HandleIqNodeAsync(node, cancellationToken);
                        break;
                    case "notification":
                        await HandleNotificationNodeAsync(node, cancellationToken);
                        break;
                    default:
                        _logger?.LogDebug("Unhandled node type: {Tag}", node.Tag);
                        break;
                }
            }
            catch (Exception ex)
            {
                _logger?.LogError(ex, "Failed to handle incoming frame");
            }
        }

        private async Task HandleIqNodeAsync(BinaryNode node, CancellationToken cancellationToken)
        {
            // IQ nodes contain queries/responses
            _logger?.LogDebug("IQ node: {@Attrs}", node.Attrs);
            await Task.CompletedTask;
        }

        private async Task HandleNotificationNodeAsync(BinaryNode node, CancellationToken cancellationToken)
        {
            // Notification nodes contain events (QR code, etc.)
            if (node.Attrs.TryGetValue("type", out var type) && type == "pair-device")
            {
                // QR Code event!
                await HandlePairDeviceNodeAsync(node, cancellationToken);
            }
            await Task.CompletedTask;
        }

        private async Task HandlePairDeviceNodeAsync(BinaryNode node, CancellationToken cancellationToken)
        {
            // Extract QR code data from node
            _logger?.LogInformation("Received pair-device notification (QR Code)");
            
            // Build QR code string: [ref],[noiseKeyB64],[identityKeyB64],[advB64]
            var qrData = "placeholder-qr-data"; // TODO: Extract from node
            
            OnQRCode?.Invoke(qrData, _config.AccountId, _config.PushName ?? "BlazorClaw");
            
            await Task.CompletedTask;
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
            _isConnected = false;
            _webSocket?.Dispose();
            GC.SuppressFinalize(this);
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
    public string AuthDir { get; set; } = "./whatsapp_auth";
    public string? PhoneNumber { get; set; }
    public string? PushName { get; set; }
}
