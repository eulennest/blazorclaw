using Baileys.Session;
using Baileys.Types;
using Baileys.Utils;
using Baileys.Options;
using Baileys.Socket;
using Microsoft.Extensions.Options;
using Baileys.Defaults;
using Baileys.Extensions;

namespace Baileys;

/// <summary>
/// High-level client for interacting with WhatsApp Web.
/// This class orchestrates the session initiation, authentication, and event emission.
/// </summary>
public class BaileysClient : IAsyncDisposable
{
    private readonly IAuthStateProvider _authStateProvider;
    private readonly IBaileysEventEmitter _ev;
    private readonly BaileysOptions _options;
    private readonly ILogger _logger;

    private BaileysSocket? _socket;

    public BaileysClient(
        IAuthStateProvider authStateProvider,
        IBaileysEventEmitter ev,
        IOptions<BaileysOptions> options,
        ILogger logger)
    {
        _authStateProvider = authStateProvider;
        _ev = ev;
        _options = options.Value;
        _logger = logger.Child(new Dictionary<string, object> { ["class"] = "client" });

        // Subscribe to connection updates to automatically log the QR code if configured
        _ev.On<ConnectionUpdateEvent>("connection.update", OnConnectionUpdate);
    }

    /// <summary>
    /// Returns the event emitter for this client.
    /// </summary>
    public IBaileysEventEmitter Ev => _ev;

    /// <summary>
    /// Initiates a connection to WhatsApp.
    /// </summary>
    public virtual async Task ConnectAsync(CancellationToken cancellationToken = default)
    {
        var authState = await _authStateProvider.LoadAuthStateAsync(cancellationToken: cancellationToken).ConfigureAwait(false);
        
        _socket = new BaileysSocket(authState, _ev, _logger);
        
        await _socket.ConnectAsync(BaileysDefaults.WaWebSocketUrl, cancellationToken).ConfigureAwait(false);
    }

    protected virtual void OnConnectionUpdate(ConnectionUpdateEvent update)
    {
        if (_options.PrintQrInTerminal && update.Qr is string qr)
        {
            QrUtils.LogQr(qr, _logger);
        }

        if (update.Connection == WaConnectionState.Open)
        {
            _logger.Info("✅ Connected to WhatsApp!");
        }
        else if (update.Connection == WaConnectionState.Close)
        {
            _logger.Warn("❌ Disconnected from WhatsApp.");
        }
    }

    public virtual async ValueTask DisposeAsync()
    {
        if (_socket != null)
        {
            await _socket.DisposeAsync().ConfigureAwait(false);
        }
    }
}
