namespace Baileys.Defaults;

/// <summary>
/// Protocol-level constants and default configuration values that mirror
/// the TypeScript <c>Defaults/index.ts</c> module.
/// </summary>
public static class BaileysDefaults
{
    /// <summary>Current Baileys/WhatsApp web version triplet [major, minor, patch].</summary>
    public static readonly int[] BaileysVersion = [2, 3000, 1033846690];

    // ──────────────────────────────────────────────────────────
    //  HTTP / WebSocket URLs
    // ──────────────────────────────────────────────────────────

    public const string WaWebSocketUrl  = "wss://web.whatsapp.com/ws/chat";
    public const string DefaultOrigin   = "https://web.whatsapp.com";
    public const string CallVideoPrefix = "https://call.whatsapp.com/video/";
    public const string CallAudioPrefix = "https://call.whatsapp.com/voice/";

    // ──────────────────────────────────────────────────────────
    //  Protocol prefixes / tag strings
    // ──────────────────────────────────────────────────────────

    public const string DefCallbackPrefix  = "CB:";
    public const string DefTagPrefix       = "TAG:";
    public const string PhoneConnectionCb  = "CB:Pong";

    // ──────────────────────────────────────────────────────────
    //  Signature prefixes (6-byte protocol markers)
    // ──────────────────────────────────────────────────────────

    public static readonly byte[] WaAdvAccountSigPrefix       = [6, 0];
    public static readonly byte[] WaAdvDeviceSigPrefix        = [6, 1];
    public static readonly byte[] WaAdvHostedAccountSigPrefix = [6, 5];
    public static readonly byte[] WaAdvHostedDeviceSigPrefix  = [6, 6];

    // ──────────────────────────────────────────────────────────
    //  Timing defaults
    // ──────────────────────────────────────────────────────────

    /// <summary>Default ephemeral message duration: 7 days in seconds.</summary>
    public const int WaDefaultEphemeral = 7 * 24 * 60 * 60;

    /// <summary>Status messages older than 24 hours are considered expired.</summary>
    public const int StatusExpirySeconds = 24 * 60 * 60;

    /// <summary>WA Web enforces a 14-day maximum age for placeholder resend requests.</summary>
    public const int PlaceholderMaxAgeSeconds = 14 * 24 * 60 * 60;

    // ──────────────────────────────────────────────────────────
    //  Noise protocol
    // ──────────────────────────────────────────────────────────

    public const string NoiseMode = "Noise_XX_25519_AESGCM_SHA256\0\0\0\0";
    public const int DictVersion = 3;

    /// <summary>Key-bundle type prefix byte.</summary>
    public static readonly byte[] KeyBundleType = [5];

    /// <summary>WA noise header bytes: "WA" + 0x06 + DICT_VERSION.</summary>
    public static readonly byte[] NoiseWaHeader = [(byte)'W', (byte)'A', 6, DictVersion];

    // ──────────────────────────────────────────────────────────
    //  Cert details
    // ──────────────────────────────────────────────────────────

    public const int WaCertSerial = 0;
    public const string WaCertIssuer = "WhatsAppLongTerm1";
    public static readonly byte[] WaCertPublicKey =
        Convert.FromHexString("142375574d0a587166aae71ebe516437c4a28b73e3695c6ce1f7f9545da8ee6b");

    // ──────────────────────────────────────────────────────────
    //  Pre-key management
    // ──────────────────────────────────────────────────────────

    public const int MinPreKeyCount    = 5;
    public const int InitialPreKeyCount = 812;

    // ──────────────────────────────────────────────────────────
    //  Upload / download timeouts
    // ──────────────────────────────────────────────────────────

    public const int UploadTimeoutMs      = 30_000;
    public const int MinUploadIntervalMs  = 5_000;

    // ──────────────────────────────────────────────────────────
    //  HTTP status codes that indicate unauthorized / session expired
    // ──────────────────────────────────────────────────────────

    public static readonly IReadOnlySet<int> UnauthorizedCodes =
        new HashSet<int> { 401, 403, 419 };

    // ──────────────────────────────────────────────────────────
    //  Default cache TTLs (in seconds)
    // ──────────────────────────────────────────────────────────

    public const int CacheTtlSignalStore = 5 * 60;
    public const int CacheTtlMsgRetry    = 60 * 60;
    public const int CacheTtlCallOffer   = 5 * 60;
    public const int CacheTtlUserDevices = 5 * 60;

    // ──────────────────────────────────────────────────────────
    //  Timespan helpers
    // ──────────────────────────────────────────────────────────

    public static readonly TimeSpan TimeMinute = TimeSpan.FromMinutes(1);
    public static readonly TimeSpan TimeHour   = TimeSpan.FromHours(1);
    public static readonly TimeSpan TimeDay    = TimeSpan.FromDays(1);
    public static readonly TimeSpan TimeWeek   = TimeSpan.FromDays(7);

    // ──────────────────────────────────────────────────────────
    //  Connection config defaults
    // ──────────────────────────────────────────────────────────

    public const int ConnectTimeoutMs         = 20_000;
    public const int KeepAliveIntervalMs      = 30_000;
    public const int DefaultQueryTimeoutMs    = 60_000;
    public const int RetryRequestDelayMs      = 250;
    public const int MaxMsgRetryCount         = 5;
    public const int LinkPreviewThumbnailWidth = 192;
    public const int MaxCommitRetries         = 10;
    public const int DelayBetweenTriesMs      = 3_000;
    public const string DefaultCountryCode    = "US";
}
