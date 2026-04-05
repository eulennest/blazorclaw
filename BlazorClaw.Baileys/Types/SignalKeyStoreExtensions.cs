using System.Text.Json;
using System.Text.Json.Serialization;

namespace Baileys.Types;

/// <summary>
/// Typed extension methods for <see cref="ISignalKeyStore"/> that provide
/// convenient, type-safe access to each Signal data category defined in
/// <see cref="SignalDataTypes"/>.
/// </summary>
/// <remarks>
/// Each method serialises/deserialises values to/from JSON bytes — the same
/// format used by the TypeScript <c>useMultiFileAuthState</c> file store and
/// the in-memory store.
/// </remarks>
public static class SignalKeyStoreExtensions
{
    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
        DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull
    };

    // ─────────────────────────────────────────────────────────────────────────
    //  pre-key
    // ─────────────────────────────────────────────────────────────────────────

    /// <summary>Gets pre-keys by their numeric IDs.</summary>
    public static async Task<IReadOnlyDictionary<string, KeyPair?>> GetPreKeysAsync(
        this ISignalKeyStore store,
        IReadOnlyList<string> ids,
        CancellationToken cancellationToken = default)
    {
        var raw = await store.GetAsync(SignalDataTypes.PreKey, ids, cancellationToken).ConfigureAwait(false);
        return raw.ToDictionary(
            kv => kv.Key,
            kv => kv.Value is null ? (KeyPair?)null : Deserialize<KeyPairDto>(kv.Value)?.ToKeyPair());
    }

    /// <summary>Stores pre-keys.  A <see langword="null"/> value removes the entry.</summary>
    public static Task SetPreKeysAsync(
        this ISignalKeyStore store,
        IReadOnlyDictionary<string, KeyPair?> values,
        CancellationToken cancellationToken = default)
    {
        var raw = values.ToDictionary(
            kv => kv.Key,
            kv => kv.Value is null ? (byte[]?)null : Serialize(KeyPairDto.From(kv.Value)));
        return store.SetAsync(SignalDataTypes.PreKey, raw, cancellationToken);
    }

    // ─────────────────────────────────────────────────────────────────────────
    //  session
    // ─────────────────────────────────────────────────────────────────────────

    /// <summary>Gets raw Signal session bytes by JID-based IDs.</summary>
    public static Task<IReadOnlyDictionary<string, byte[]?>> GetSessionsAsync(
        this ISignalKeyStore store,
        IReadOnlyList<string> ids,
        CancellationToken cancellationToken = default)
        => store.GetAsync(SignalDataTypes.Session, ids, cancellationToken);

    /// <summary>Stores raw Signal session bytes.  A <see langword="null"/> value removes the entry.</summary>
    public static Task SetSessionsAsync(
        this ISignalKeyStore store,
        IReadOnlyDictionary<string, byte[]?> values,
        CancellationToken cancellationToken = default)
        => store.SetAsync(SignalDataTypes.Session, values, cancellationToken);

    // ─────────────────────────────────────────────────────────────────────────
    //  sender-key
    // ─────────────────────────────────────────────────────────────────────────

    /// <summary>Gets group sender-key bytes.</summary>
    public static Task<IReadOnlyDictionary<string, byte[]?>> GetSenderKeysAsync(
        this ISignalKeyStore store,
        IReadOnlyList<string> ids,
        CancellationToken cancellationToken = default)
        => store.GetAsync(SignalDataTypes.SenderKey, ids, cancellationToken);

    /// <summary>Stores group sender-key bytes.  A <see langword="null"/> value removes the entry.</summary>
    public static Task SetSenderKeysAsync(
        this ISignalKeyStore store,
        IReadOnlyDictionary<string, byte[]?> values,
        CancellationToken cancellationToken = default)
        => store.SetAsync(SignalDataTypes.SenderKey, values, cancellationToken);

    // ─────────────────────────────────────────────────────────────────────────
    //  sender-key-memory
    // ─────────────────────────────────────────────────────────────────────────

    /// <summary>Gets sender-key distribution memory maps by group+JID IDs.</summary>
    public static async Task<IReadOnlyDictionary<string, Dictionary<string, bool>?>> GetSenderKeyMemoriesAsync(
        this ISignalKeyStore store,
        IReadOnlyList<string> ids,
        CancellationToken cancellationToken = default)
    {
        var raw = await store.GetAsync(SignalDataTypes.SenderKeyMemory, ids, cancellationToken).ConfigureAwait(false);
        return raw.ToDictionary(
            kv => kv.Key,
            kv => kv.Value is null ? (Dictionary<string, bool>?)null
                                   : Deserialize<Dictionary<string, bool>>(kv.Value));
    }

    /// <summary>Stores sender-key distribution memory maps.  A <see langword="null"/> value removes the entry.</summary>
    public static Task SetSenderKeyMemoriesAsync(
        this ISignalKeyStore store,
        IReadOnlyDictionary<string, Dictionary<string, bool>?> values,
        CancellationToken cancellationToken = default)
    {
        var raw = values.ToDictionary(
            kv => kv.Key,
            kv => kv.Value is null ? (byte[]?)null : Serialize(kv.Value));
        return store.SetAsync(SignalDataTypes.SenderKeyMemory, raw, cancellationToken);
    }

    // ─────────────────────────────────────────────────────────────────────────
    //  app-state-sync-key
    // ─────────────────────────────────────────────────────────────────────────

    /// <summary>Gets raw app-state sync key bytes.</summary>
    public static Task<IReadOnlyDictionary<string, byte[]?>> GetAppStateSyncKeysAsync(
        this ISignalKeyStore store,
        IReadOnlyList<string> ids,
        CancellationToken cancellationToken = default)
        => store.GetAsync(SignalDataTypes.AppStateSyncKey, ids, cancellationToken);

    /// <summary>Stores raw app-state sync key bytes.  A <see langword="null"/> value removes the entry.</summary>
    public static Task SetAppStateSyncKeysAsync(
        this ISignalKeyStore store,
        IReadOnlyDictionary<string, byte[]?> values,
        CancellationToken cancellationToken = default)
        => store.SetAsync(SignalDataTypes.AppStateSyncKey, values, cancellationToken);

    // ─────────────────────────────────────────────────────────────────────────
    //  app-state-sync-version
    // ─────────────────────────────────────────────────────────────────────────

    /// <summary>Gets LT-hash state objects used for app-state sync verification.</summary>
    public static async Task<IReadOnlyDictionary<string, LtHashState?>> GetAppStateSyncVersionsAsync(
        this ISignalKeyStore store,
        IReadOnlyList<string> ids,
        CancellationToken cancellationToken = default)
    {
        var raw = await store.GetAsync(SignalDataTypes.AppStateSyncVersion, ids, cancellationToken).ConfigureAwait(false);
        return raw.ToDictionary(
            kv => kv.Key,
            kv => kv.Value is null ? (LtHashState?)null : Deserialize<LtHashState>(kv.Value));
    }

    /// <summary>Stores LT-hash state objects.  A <see langword="null"/> value removes the entry.</summary>
    public static Task SetAppStateSyncVersionsAsync(
        this ISignalKeyStore store,
        IReadOnlyDictionary<string, LtHashState?> values,
        CancellationToken cancellationToken = default)
    {
        var raw = values.ToDictionary(
            kv => kv.Key,
            kv => kv.Value is null ? (byte[]?)null : Serialize(kv.Value));
        return store.SetAsync(SignalDataTypes.AppStateSyncVersion, raw, cancellationToken);
    }

    // ─────────────────────────────────────────────────────────────────────────
    //  lid-mapping
    // ─────────────────────────────────────────────────────────────────────────

    /// <summary>Gets LID-to-phone-number mapping strings.</summary>
    public static async Task<IReadOnlyDictionary<string, string?>> GetLidMappingsAsync(
        this ISignalKeyStore store,
        IReadOnlyList<string> ids,
        CancellationToken cancellationToken = default)
    {
        var raw = await store.GetAsync(SignalDataTypes.LidMapping, ids, cancellationToken).ConfigureAwait(false);
        return raw.ToDictionary(
            kv => kv.Key,
            kv => kv.Value is null ? (string?)null : System.Text.Encoding.UTF8.GetString(kv.Value));
    }

    /// <summary>Stores LID-to-phone-number mapping strings.  A <see langword="null"/> value removes the entry.</summary>
    public static Task SetLidMappingsAsync(
        this ISignalKeyStore store,
        IReadOnlyDictionary<string, string?> values,
        CancellationToken cancellationToken = default)
    {
        var raw = values.ToDictionary(
            kv => kv.Key,
            kv => kv.Value is null ? (byte[]?)null : System.Text.Encoding.UTF8.GetBytes(kv.Value));
        return store.SetAsync(SignalDataTypes.LidMapping, raw, cancellationToken);
    }

    // ─────────────────────────────────────────────────────────────────────────
    //  device-list
    // ─────────────────────────────────────────────────────────────────────────

    /// <summary>Gets the list of known device IDs for each JID.</summary>
    public static async Task<IReadOnlyDictionary<string, IReadOnlyList<string>?>> GetDeviceListsAsync(
        this ISignalKeyStore store,
        IReadOnlyList<string> ids,
        CancellationToken cancellationToken = default)
    {
        var raw = await store.GetAsync(SignalDataTypes.DeviceList, ids, cancellationToken).ConfigureAwait(false);
        return raw.ToDictionary(
            kv => kv.Key,
            kv => kv.Value is null
                ? (IReadOnlyList<string>?)null
                : (IReadOnlyList<string>?)Deserialize<List<string>>(kv.Value));
    }

    /// <summary>Stores device-ID lists.  A <see langword="null"/> value removes the entry.</summary>
    public static Task SetDeviceListsAsync(
        this ISignalKeyStore store,
        IReadOnlyDictionary<string, IReadOnlyList<string>?> values,
        CancellationToken cancellationToken = default)
    {
        var raw = values.ToDictionary(
            kv => kv.Key,
            kv => kv.Value is null ? (byte[]?)null : Serialize(kv.Value));
        return store.SetAsync(SignalDataTypes.DeviceList, raw, cancellationToken);
    }

    // ─────────────────────────────────────────────────────────────────────────
    //  tctoken
    // ─────────────────────────────────────────────────────────────────────────

    /// <summary>Gets TC-token entries.</summary>
    public static async Task<IReadOnlyDictionary<string, TcToken?>> GetTcTokensAsync(
        this ISignalKeyStore store,
        IReadOnlyList<string> ids,
        CancellationToken cancellationToken = default)
    {
        var raw = await store.GetAsync(SignalDataTypes.TcToken, ids, cancellationToken).ConfigureAwait(false);
        return raw.ToDictionary(
            kv => kv.Key,
            kv => kv.Value is null ? (TcToken?)null : Deserialize<TcToken>(kv.Value));
    }

    /// <summary>Stores TC-token entries.  A <see langword="null"/> value removes the entry.</summary>
    public static Task SetTcTokensAsync(
        this ISignalKeyStore store,
        IReadOnlyDictionary<string, TcToken?> values,
        CancellationToken cancellationToken = default)
    {
        var raw = values.ToDictionary(
            kv => kv.Key,
            kv => kv.Value is null ? (byte[]?)null : Serialize(kv.Value));
        return store.SetAsync(SignalDataTypes.TcToken, raw, cancellationToken);
    }

    // ─────────────────────────────────────────────────────────────────────────
    //  identity-key
    // ─────────────────────────────────────────────────────────────────────────

    /// <summary>Gets raw identity-key bytes by JID.</summary>
    public static Task<IReadOnlyDictionary<string, byte[]?>> GetIdentityKeysAsync(
        this ISignalKeyStore store,
        IReadOnlyList<string> ids,
        CancellationToken cancellationToken = default)
        => store.GetAsync(SignalDataTypes.IdentityKey, ids, cancellationToken);

    /// <summary>Stores raw identity-key bytes.  A <see langword="null"/> value removes the entry.</summary>
    public static Task SetIdentityKeysAsync(
        this ISignalKeyStore store,
        IReadOnlyDictionary<string, byte[]?> values,
        CancellationToken cancellationToken = default)
        => store.SetAsync(SignalDataTypes.IdentityKey, values, cancellationToken);

    // ─────────────────────────────────────────────────────────────────────────
    //  Private serialisation helpers
    // ─────────────────────────────────────────────────────────────────────────

    private static byte[] Serialize<T>(T value)
        => JsonSerializer.SerializeToUtf8Bytes(value, JsonOptions);

    private static T? Deserialize<T>(byte[] bytes)
        => JsonSerializer.Deserialize<T>(bytes, JsonOptions);

    // ─────────────────────────────────────────────────────────────────────────
    //  DTO for KeyPair
    // ─────────────────────────────────────────────────────────────────────────

    private sealed class KeyPairDto
    {
        public string? Public { get; set; }
        public string? Private { get; set; }

        public KeyPair? ToKeyPair()
        {
            if (Public is null || Private is null)
                return null;
            return new KeyPair(
                Public:  Convert.FromBase64String(Public),
                Private: Convert.FromBase64String(Private));
        }

        public static KeyPairDto From(KeyPair kp) => new()
        {
            Public  = Convert.ToBase64String(kp.Public),
            Private = Convert.ToBase64String(kp.Private)
        };
    }
}

/// <summary>TC-token data used in device authentication.</summary>
public sealed class TcToken
{
    /// <summary>Raw token bytes (Base64-encoded in JSON).</summary>
    [JsonConverter(typeof(Base64ByteArrayConverter))]
    public required byte[] Token { get; init; }

    /// <summary>Optional timestamp string.</summary>
    public string? Timestamp { get; init; }
}

/// <summary>JSON converter that serialises <c>byte[]</c> as a Base64 string.</summary>
internal sealed class Base64ByteArrayConverter : JsonConverter<byte[]>
{
    public override byte[] Read(ref Utf8JsonReader reader, Type typeToConvert, JsonSerializerOptions options)
        => Convert.FromBase64String(reader.GetString() ?? string.Empty);

    public override void Write(Utf8JsonWriter writer, byte[] value, JsonSerializerOptions options)
        => writer.WriteStringValue(Convert.ToBase64String(value));
}
