namespace Baileys.Types;

/// <summary>Call update type values.</summary>
public enum WaCallUpdateType
{
    Offer,
    Ringing,
    Timeout,
    Reject,
    Accept,
    Terminate
}

/// <summary>
/// Represents an incoming or outgoing WhatsApp call event,
/// mirroring the TypeScript <c>Types/Call.ts</c> interface.
/// </summary>
public sealed class WaCallEvent
{
    /// <summary>The JID of the chat in which the call appeared.</summary>
    public required string ChatId { get; init; }

    /// <summary>JID of the caller.</summary>
    public required string From { get; init; }

    /// <summary>Phone-number JID of the caller (if available).</summary>
    public string? CallerPn { get; init; }

    /// <summary>Whether this is a group call.</summary>
    public bool IsGroup { get; init; }

    /// <summary>Group JID if this is a group call.</summary>
    public string? GroupJid { get; init; }

    /// <summary>Unique call identifier.</summary>
    public required string Id { get; init; }

    /// <summary>UTC time the call event was received.</summary>
    public DateTimeOffset Date { get; init; }

    /// <summary>Whether this is a video call.</summary>
    public bool IsVideo { get; init; }

    /// <summary>Current call status.</summary>
    public WaCallUpdateType Status { get; init; }

    /// <summary>Whether the event was delivered while the device was offline.</summary>
    public bool Offline { get; init; }

    /// <summary>Round-trip latency to the caller, in milliseconds (if known).</summary>
    public int? LatencyMs { get; init; }
}
