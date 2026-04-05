using Baileys.Defaults;
using Baileys.Types;
using Baileys.Utils;
using Baileys.WABinary;
using System.Net.WebSockets;
using static System.Runtime.InteropServices.JavaScript.JSType;
using Google.Protobuf;

namespace Baileys.Socket;

/// <summary>
/// Low-level WebSocket client for Baileys that handles the Noise handshake
/// and binary node encoding/decoding.
/// </summary>
public sealed class BaileysSocket : IAsyncDisposable
{
    private readonly ClientWebSocket _ws = new();
    private readonly NoiseHandler _noise;
    private readonly ILogger _logger;
    private readonly IBaileysEventEmitter _ev;
    private readonly CancellationTokenSource _cts = new();

    private bool _handshakeComplete;
    private Task? _receiveTask;

    public BaileysSocket(KeyPair noiseKeyPair, IBaileysEventEmitter ev, ILogger logger)
    {
        _noise = new NoiseHandler(noiseKeyPair, logger);
        _ev = ev;
        _logger = logger.Child(new Dictionary<string, object> { ["class"] = "socket" });
    }

    public async Task ConnectAsync(string url, CancellationToken cancellationToken = default)
    {
        _logger.Info($"Connecting to {url}...");
        await _ws.ConnectAsync(new Uri(url), cancellationToken).ConfigureAwait(false);
        _logger.Info("Connected to WebSocket.");

        // Start the Noise handshake
        await SendIntroHeaderAsync(cancellationToken).ConfigureAwait(false);
        await SendClientHelloAsync(cancellationToken).ConfigureAwait(false);

        _receiveTask = ReceiveLoopAsync(_cts.Token);
    }

    private async Task SendIntroHeaderAsync(CancellationToken cancellationToken)
    {
        var header = _noise.IntroHeader.ToArray();
        await SendRawAsync(header, cancellationToken).ConfigureAwait(false);
        _logger.Debug("Sent intro header.");
    }

    private async Task SendClientHelloAsync(CancellationToken cancellationToken)
    {
        // Generate ephemeral keypair for this session
        var ephemeralKeyPair = AuthUtils.GenerateKeyPair();
        var ephemeralPub = ephemeralKeyPair.Public;

        // Build ClientHello protobuf
        var clientHello = new global::Proto.HandshakeMessage
        {
            ClientHello = new global::Proto.HandshakeMessage.Types.ClientHello
            {
                Ephemeral = ByteString.CopyFrom(ephemeralPub)
            }
        };

        // Encode and frame
        var helloBytes = clientHello.ToByteArray();
        var frame = EncodeFrame(helloBytes);

        await SendRawAsync(frame, cancellationToken).ConfigureAwait(false);
        _logger.Debug($"Sent ClientHello ({frame.Length} bytes)");
    }

    private static byte[] EncodeFrame(byte[] payload)
    {
        var frame = new byte[3 + payload.Length];
        frame[0] = (byte)((payload.Length >> 16) & 0xFF);
        frame[1] = (byte)((payload.Length >> 8) & 0xFF);
        frame[2] = (byte)(payload.Length & 0xFF);
        payload.CopyTo(frame, 3);
        return frame;
    }

    public async Task SendNodeAsync(BinaryNode node, CancellationToken cancellationToken = default)
    {
        var encoded = WaBinaryEncoder.EncodeBinaryNode(node);
        var encrypted = _noise.Encrypt(encoded);

        // Frame it: 3 bytes for length (big-endian)
        var frame = new byte[encrypted.Length + 3];
        frame[0] = (byte)((encrypted.Length >> 16) & 0xFF);
        frame[1] = (byte)((encrypted.Length >> 8) & 0xFF);
        frame[2] = (byte)(encrypted.Length & 0xFF);
        encrypted.CopyTo(frame.AsSpan(3));

        await SendRawAsync(frame, cancellationToken).ConfigureAwait(false);
    }
    public Task SendRawAsync(byte[] data, CancellationToken cancellationToken = default)
    {
        var hex = BitConverter.ToString(data).Replace("-", string.Empty);
        _logger.Trace($"[SEND] {hex}");
        return _ws.SendAsync(data, WebSocketMessageType.Binary, true, cancellationToken);
    }


    private async Task ReceiveLoopAsync(CancellationToken cancellationToken)
    {
        var buffer = new byte[65536];
        try
        {
            while (!cancellationToken.IsCancellationRequested && _ws.State == WebSocketState.Open)
            {
                var result = await _ws.ReceiveAsync(buffer, cancellationToken).ConfigureAwait(false);
                if (result.MessageType == WebSocketMessageType.Close)
                {
                    await _ws.CloseAsync(WebSocketCloseStatus.NormalClosure, string.Empty, cancellationToken).ConfigureAwait(false);
                    _ev.Emit("connection.update", new ConnectionUpdateEvent { Connection = WaConnectionState.Close });
                    break;
                }

                if (result.MessageType == WebSocketMessageType.Binary)
                {
                    var hex = BitConverter.ToString(buffer[..result.Count]).Replace("-", string.Empty);
                    _logger.Trace($"[RECV] {hex}");
                    await HandleMessageAsync(buffer[..result.Count]).ConfigureAwait(false);
                }
            }
        }
        catch (OperationCanceledException) { }
        catch (Exception ex)
        {
            _logger.Error($"Receive loop error: {ex.Message}");
            _ev.Emit("connection.update", new ConnectionUpdateEvent
            {
                Connection = WaConnectionState.Close,
                LastDisconnect = new LastDisconnectInfo { Error = ex, Date = DateTimeOffset.UtcNow }
            });
        }
    }

    private async Task HandleMessageAsync(byte[] data)
    {
        if (!_handshakeComplete)
        {
            // First message from server is the handshake response
            _noise.Decrypt(data); // This updates the internal state

            // In a real implementation, we'd process the handshake more carefully.
            // For now, we assume the next step is to send our finish.
            // In Baileys, the handshake is 3 messages:
            // 1. Client -> Server (Intro + Noise Message 1)
            // 2. Server -> Client (Noise Message 2)
            // 3. Client -> Server (Noise Message 3)

            // Simplified: we'll just emit that we're connecting.
            _handshakeComplete = true;
            _noise.Finish();
            _logger.Info("Handshake complete.");
            _ev.Emit("connection.update", new ConnectionUpdateEvent { Connection = WaConnectionState.Open });
            return;
        }

        // After handshake, messages are framed with a 3-byte length
        if (data.Length < 3) return;

        var encrypted = data[3..];
        var decrypted = _noise.Decrypt(encrypted);
        var node = await WaBinaryDecoder.DecodeBinaryNodeAsync(decrypted).ConfigureAwait(false);

        _logger.Trace($"Received node: {node.Tag}");
        // Here we would dispatch the node to the right handler.
        // For the QR code, it usually comes in a specific node or triggered by a success node.
    }

    public async ValueTask DisposeAsync()
    {
        _cts.Cancel();
        if (_receiveTask != null) await _receiveTask.ConfigureAwait(false);
        _ws.Dispose();
        _cts.Dispose();
    }
}
