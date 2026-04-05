namespace Baileys.Types;

// ──────────────────────────────────────────────────────────────────────────────
//  Connection state
// ──────────────────────────────────────────────────────────────────────────────

/// <summary>WhatsApp WebSocket connection state.</summary>
public enum WaConnectionState
{
    Open,
    Connecting,
    Close
}

/// <summary>
/// Snapshot of the current connection state, mirroring the TypeScript
/// <c>Types/State.ts</c> ConnectionState interface.
/// </summary>
public sealed class ConnectionState
{
    /// <summary>Current connection status.</summary>
    public WaConnectionState Connection { get; init; }

    /// <summary>Details about why the connection was last closed.</summary>
    public LastDisconnectInfo? LastDisconnect { get; init; }

    /// <summary>Whether this is the first time the account is being registered.</summary>
    public bool? IsNewLogin { get; init; }

    /// <summary>Current QR code string (present when waiting for QR scan).</summary>
    public string? Qr { get; init; }

    /// <summary>Whether the device has processed all offline notifications.</summary>
    public bool? ReceivedPendingNotifications { get; init; }

    /// <summary>Whether the client appears online to other devices.</summary>
    public bool? IsOnline { get; init; }
}

/// <summary>Details about the last disconnect event.</summary>
public sealed class LastDisconnectInfo
{
    /// <summary>The error that caused the disconnection.</summary>
    public Exception? Error { get; init; }

    /// <summary>Timestamp of the disconnection.</summary>
    public DateTimeOffset Date { get; init; }
}

// ──────────────────────────────────────────────────────────────────────────────
//  Presence
// ──────────────────────────────────────────────────────────────────────────────

/// <summary>A WhatsApp presence state.</summary>
public enum WaPresence
{
    Unavailable,
    Available,
    Composing,
    Recording,
    Paused
}

/// <summary>Presence data for a contact in a chat.</summary>
public sealed class PresenceData
{
    public WaPresence LastKnownPresence { get; init; }
    public long? LastSeen { get; init; }
}

// ──────────────────────────────────────────────────────────────────────────────
//  Privacy settings
// ──────────────────────────────────────────────────────────────────────────────

/// <summary>Possible values for most WhatsApp privacy settings.</summary>
public enum WaPrivacyValue
{
    All,
    Contacts,
    ContactBlacklist,
    None
}

/// <summary>Who can see the "online" status.</summary>
public enum WaPrivacyOnlineValue
{
    All,
    MatchLastSeen
}

/// <summary>Who can add the authenticated user to groups.</summary>
public enum WaPrivacyGroupAddValue
{
    All,
    Contacts,
    ContactBlacklist
}

/// <summary>Read-receipt privacy.</summary>
public enum WaReadReceiptsValue
{
    All,
    None
}

/// <summary>Who can call the authenticated user.</summary>
public enum WaPrivacyCallValue
{
    All,
    Known
}

/// <summary>Who can send messages to the authenticated user.</summary>
public enum WaPrivacyMessagesValue
{
    All,
    Contacts
}

// ──────────────────────────────────────────────────────────────────────────────
//  App state patch names
// ──────────────────────────────────────────────────────────────────────────────

/// <summary>Named app-state patches WhatsApp sends over the sync channel.</summary>
public enum WaPatchName
{
    CriticalBlock,
    CriticalUnblockLow,
    RegularHigh,
    RegularLow,
    Regular
}

/// <summary>String constants for <see cref="WaPatchName"/>.</summary>
public static class WaPatchNames
{
    public const string CriticalBlock = "critical_block";
    public const string CriticalUnblockLow = "critical_unblock_low";
    public const string RegularHigh = "regular_high";
    public const string RegularLow = "regular_low";
    public const string Regular = "regular";

    public static readonly IReadOnlyList<string> All =
    [
        CriticalBlock, CriticalUnblockLow, RegularHigh, RegularLow, Regular
    ];
}

// ──────────────────────────────────────────────────────────────────────────────
//  Sync state
// ──────────────────────────────────────────────────────────────────────────────

/// <summary>Stages the socket goes through during startup.</summary>
public enum SyncState
{
    /// <summary>Connecting but no pending notifications received yet.</summary>
    Connecting,
    /// <summary>Pending notifications received; buffering events.</summary>
    AwaitingInitialSync,
    /// <summary>Initial app-state sync in progress.</summary>
    Syncing,
    /// <summary>Fully operational; events processed in real time.</summary>
    Online
}
