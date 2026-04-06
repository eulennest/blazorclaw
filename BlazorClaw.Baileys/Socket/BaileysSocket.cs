using Baileys.Crypto;
using Baileys.Defaults;
using Baileys.Types;
using Baileys.Utils;
using Baileys.WABinary;
using Google.Protobuf;
using Microsoft.Extensions.Logging;
using org.whispersystems.curve25519.csharp;
using Proto;
using System.Net.WebSockets;
using System.Security.Cryptography;
using System.Text;
using static Org.BouncyCastle.Math.EC.ECCurve;
using static System.Runtime.InteropServices.JavaScript.JSType;
using ILogger = Baileys.Utils.ILogger;

namespace Baileys.Socket;

/// <summary>
/// Low-level WebSocket client for Baileys that handles the Noise handshake
/// and binary node encoding/decoding.
/// </summary>
public sealed class BaileysSocket : IAsyncDisposable
{
    private readonly ClientWebSocket _ws = new();
    private readonly AuthenticationState _creds;
    private readonly SignalCreds _signalKeys;
    private readonly KeyPair _emperalKey;
    private readonly NoiseHandler _noise;
    private readonly ILogger _logger;
    private readonly IBaileysEventEmitter _ev;
    private readonly CancellationTokenSource _cts = new();

    private bool _handshakeComplete;
    private Task? _receiveTask;

    public BaileysSocket(AuthenticationState creds, IBaileysEventEmitter ev, ILogger logger)
    {
        _creds = creds;
        _signalKeys = new SignalCreds(creds.Creds.SignedIdentityKey, creds.Creds.SignedPreKey, creds.Creds.RegistrationId);
        _emperalKey = Curve25519Utils.GenerateKeyPair();
        _noise = new NoiseHandler(_emperalKey, logger);
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
        await _ws.SendAsync(header, WebSocketMessageType.Binary, true, cancellationToken).ConfigureAwait(false);
        _logger.Debug("Sent intro header.");
    }

    private async Task SendClientHelloAsync(CancellationToken cancellationToken)
    {
        // Build ClientHello protobuf
        var clientHello = new global::Proto.HandshakeMessage
        {
            ClientHello = new global::Proto.HandshakeMessage.Types.ClientHello
            {
                Ephemeral = ByteString.CopyFrom(_emperalKey.Public)
            }
        };

        var helloBytes = clientHello.ToByteArray();
        await SendRawAsync(helloBytes, cancellationToken).ConfigureAwait(false);
        _logger.Debug($"Sent ClientHello ({helloBytes.Length} bytes)");
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
        await SendRawAsync(encrypted, cancellationToken).ConfigureAwait(false);
    }
    public Task SendRawAsync(byte[] data, CancellationToken cancellationToken = default)
    {
        data = EncodeFrame(data);
        var hex = BitConverter.ToString(data).Replace("-", string.Empty);
        _logger.Trace($"[SEND] ({data.Length} bytes) {hex}");
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
                    await HandleMessageAsync(buffer[..result.Count][3..]).ConfigureAwait(false);
                }
            }
        }
        catch (OperationCanceledException) { }
        catch (Exception ex)
        {
            _logger.Exception(ex);
            _ev.Emit("connection.update", new ConnectionUpdateEvent
            {
                Connection = WaConnectionState.Close,
                LastDisconnect = new LastDisconnectInfo { Error = ex, Date = DateTimeOffset.UtcNow }
            });
        }
    }

    private async Task HandleMessageAsync(byte[] data, CancellationToken cancellationToken = default)
    {
        if (!_handshakeComplete)
        {
            var key = _noise.ProcessHandshake(Proto.HandshakeMessage.Parser.ParseFrom(data));

            Proto.ClientPayload? cpNode;
            if (!_creds.Creds.Registered)
            {
                cpNode = GenerateRegistrationNode(_signalKeys);

                _logger.Info("not logged in, attempting registration...");
            }
            else
            {
                cpNode = GenerateLoginNode(null);

                _logger.Info("logging in...");
            }

            var payloadEnc = _noise.Encrypt(cpNode.ToByteArray());

            // Build ClientHello protobuf
            var finish = new global::Proto.HandshakeMessage
            {
                ClientFinish = new()
                {
                    Static = ByteString.CopyFrom(key),
                    Payload = ByteString.CopyFrom(payloadEnc)
                }
            };

            // Encode and frame
            var frame = EncodeFrame(finish.ToByteArray());
            await SendRawAsync(frame, cancellationToken).ConfigureAwait(false);




            // First message from server is the handshake response
            //            _noise.Decrypt(data); // This updates the internal state

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



    public static ClientPayload GenerateRegistrationNode(SignalCreds credentials)
    {
        // Hash app version as MD5
        var versionString = string.Join(".", BaileysDefaults.BaileysVersion);
        byte[] appVersionBuf;

        using (var md5 = MD5.Create())
        {
            appVersionBuf = md5.ComputeHash(Encoding.UTF8.GetBytes(versionString));
        }

        var companion = new DeviceProps
        {
            Os = "win",
            PlatformType = DeviceProps.Types.PlatformType.Chrome,
            //           RequireFullSync = config.SyncFullHistory,
            HistorySyncConfig = new()
            {
                StorageQuotaMb = 10240,
                InlineInitialPayloadInE2EeMsg = true,
                RecentSyncDaysLimit = 0, // or null equivalent
                SupportCallLogHistory = false,
                SupportBotUserAgentChatHistory = true,
                SupportCagReactionsAndPolls = true,
                SupportBizHostedMsg = true,
                SupportRecentSyncChunkMessageCountTuning = true,
                SupportHostedGroupMsg = true,
                SupportFbidBotChatHistory = true,
                SupportAddOnHistorySyncMigration = false,
                SupportMessageAssociation = true,
                SupportGroupHistory = false,
                OnDemandReady = false,
                SupportGuestChat = false
            },
            Version = new()
            {
                Primary = 10,
                Secondary = 15,
                Tertiary = 7
            }
        };

        var companionProto = companion.ToByteString();

        var waversion = BaileysDefaults.BaileysWaVersion;
        var registerPayload = new ClientPayload
        {
            ConnectType = ClientPayload.Types.ConnectType.WifiUnknown,
            ConnectReason = ClientPayload.Types.ConnectReason.UserActivated,
            UserAgent = new()
            {
                AppVersion = new()
                {
                    Primary = (uint)waversion.Major,
                    Secondary = (uint)waversion.Minor,
                    Tertiary = (uint)waversion.Patch
                },
                Platform = ClientPayload.Types.UserAgent.Types.Platform.Web,
                ReleaseChannel = ClientPayload.Types.UserAgent.Types.ReleaseChannel.Release,
                OsVersion = "0.1",
                Device = "Desktop",
                OsBuildNumber = "0.1",
                LocaleLanguageIso6391 = "en",
                Mnc = "000",
                Mcc = "000",
                LocaleCountryIso31661Alpha2 = "de"
            },
            WebInfo = new()
            {
                WebSubPlatform = ClientPayload.Types.WebInfo.Types.WebSubPlatform.WebBrowser
            },
            Passive = false,
            Pull = false,
            DevicePairingData = new()
            {
                BuildHash = ByteString.CopyFrom(appVersionBuf),
                DeviceProps = companionProto,
                ERegid = ByteString.CopyFrom(Generics.EncodeBigEndian(credentials.RegistrationId)),
                EKeytype = ByteString.CopyFrom(BaileysDefaults.KeyBundleType),
                EIdent = ByteString.CopyFrom(credentials.SignedIdentityKey.Public),
                ESkeyId = ByteString.CopyFrom(Generics.EncodeBigEndian(credentials.SignedPreKey.KeyId, 3)),
                ESkeyVal = ByteString.CopyFrom(credentials.SignedPreKey.KeyPair.Public),
                ESkeySig = ByteString.CopyFrom(credentials.SignedPreKey.Signature)
            }
        };

        return registerPayload;
    }
    public static ClientPayload GenerateLoginNode(SignalCreds credentials)
    {
        var jid = JidUtils.JidDecode("");
        var waversion = BaileysDefaults.BaileysWaVersion;
        var registerPayload = new ClientPayload
        {
            ConnectType = ClientPayload.Types.ConnectType.WifiUnknown,
            ConnectReason = ClientPayload.Types.ConnectReason.UserActivated,
            UserAgent = new()
            {
                AppVersion = new()
                {
                    Primary = (uint)waversion.Major,
                    Secondary = (uint)waversion.Minor,
                    Tertiary = (uint)waversion.Patch
                },
                Platform = ClientPayload.Types.UserAgent.Types.Platform.Web,
                ReleaseChannel = ClientPayload.Types.UserAgent.Types.ReleaseChannel.Release,
                OsVersion = "0.1",
                Device = "Desktop",
                OsBuildNumber = "0.1",
                LocaleLanguageIso6391 = "en",
                Mnc = "000",
                Mcc = "000",
                LocaleCountryIso31661Alpha2 = "de"
            },
            WebInfo = new()
            {
                WebSubPlatform = ClientPayload.Types.WebInfo.Types.WebSubPlatform.WebBrowser
            },
            Passive = true,
            Pull = true,
            LidDbMigrated = false,
            Username = ulong.Parse(jid.User),
            Device = (uint)jid.Device.GetValueOrDefault()
        };

        return registerPayload;


    }

}
