namespace Baileys.Types;

// ──────────────────────────────────────────────────────────────────────────────
//  Signal key store — mirrors TypeScript Types/Auth.ts SignalKeyStore /
//  SignalDataTypeMap / SignalDataSet
// ──────────────────────────────────────────────────────────────────────────────

/// <summary>
/// String constants for Signal-protocol data types, mirroring the keys of the
/// TypeScript <c>SignalDataTypeMap</c>.
/// </summary>
public static class SignalDataTypes
{
    /// <summary>Individual pre-key pairs used for X3DH key agreement.</summary>
    public const string PreKey = "pre-key";

    /// <summary>Per-JID session state for the Signal double-ratchet.</summary>
    public const string Session = "session";

    /// <summary>Group sender-key data.</summary>
    public const string SenderKey = "sender-key";

    /// <summary>In-memory map tracking which JIDs have received a sender-key distribution.</summary>
    public const string SenderKeyMemory = "sender-key-memory";

    /// <summary>App-state sync encryption keys.</summary>
    public const string AppStateSyncKey = "app-state-sync-key";

    /// <summary>LT-hash state for app-state sync patches.</summary>
    public const string AppStateSyncVersion = "app-state-sync-version";

    /// <summary>LID ↔ phone-number mapping entries.</summary>
    public const string LidMapping = "lid-mapping";

    /// <summary>Per-JID list of known device IDs.</summary>
    public const string DeviceList = "device-list";

    /// <summary>TC-token data used in device authentication.</summary>
    public const string TcToken = "tctoken";

    /// <summary>Raw identity keys for contacts / own devices.</summary>
    public const string IdentityKey = "identity-key";
}

/// <summary>
/// Storage for Signal-protocol cryptographic keys.
/// Mirrors the TypeScript <c>SignalKeyStore</c> interface from <c>Types/Auth.ts</c>.
/// </summary>
/// <remarks>
/// Values are stored as raw JSON bytes so that the store remains type-agnostic
/// (different key types carry different payloads).  Higher-level helpers
/// (<see cref="SignalKeyStoreExtensions"/>) provide typed accessors built on top
/// of this raw interface.
/// </remarks>
public interface ISignalKeyStore
{
    /// <summary>
    /// Retrieves stored values for the given Signal-data <paramref name="type"/>
    /// and the specified <paramref name="ids"/>.
    /// </summary>
    /// <param name="type">
    /// Signal data type name — use one of the constants on
    /// <see cref="SignalDataTypes"/> (e.g. <c>"pre-key"</c>, <c>"session"</c>).
    /// </param>
    /// <param name="ids">IDs to look up.</param>
    /// <param name="cancellationToken">Optional cancellation token.</param>
    /// <returns>
    /// A dictionary mapping each requested id to its raw JSON bytes, or
    /// <see langword="null"/> when the entry is absent.
    /// </returns>
    Task<IReadOnlyDictionary<string, byte[]?>> GetAsync(
        string type,
        IReadOnlyList<string> ids,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Stores or removes values for the given Signal-data <paramref name="type"/>.
    /// A <see langword="null"/> value for a given id removes that entry.
    /// </summary>
    /// <param name="type">Signal data type name.</param>
    /// <param name="values">Map of id → raw JSON bytes (null to delete).</param>
    /// <param name="cancellationToken">Optional cancellation token.</param>
    Task SetAsync(
        string type,
        IReadOnlyDictionary<string, byte[]?> values,
        CancellationToken cancellationToken = default);

    /// <summary>Removes all stored keys of every type.</summary>
    Task ClearAsync(CancellationToken cancellationToken = default);
}

/// <summary>
/// An <see cref="ISignalKeyStore"/> that additionally supports atomic
/// transactions, mirroring the TypeScript
/// <c>SignalKeyStoreWithTransaction</c> interface.
/// </summary>
public interface ISignalKeyStoreWithTransaction : ISignalKeyStore
{
    /// <summary>
    /// Returns <see langword="true"/> if the current async execution context
    /// is running inside an active transaction.
    /// </summary>
    bool IsInTransaction { get; }

    /// <summary>
    /// Executes <paramref name="action"/> inside a transaction.  Changes made
    /// through the store during the transaction are buffered and committed
    /// atomically when <paramref name="action"/> completes successfully.
    /// On failure the changes are discarded.
    /// </summary>
    /// <param name="action">The work to perform within the transaction.</param>
    /// <param name="key">A logical key identifying this transaction (for logging).</param>
    Task<T> TransactionAsync<T>(Func<Task<T>> action, string key = "");
}
