namespace Baileys.Types;

// ──────────────────────────────────────────────────────────────────────────────
//  Message addressing mode
// ──────────────────────────────────────────────────────────────────────────────

/// <summary>How a WhatsApp group addresses its members.</summary>
public enum WaMessageAddressingMode
{
    /// <summary>Messages are addressed by phone number (PN).</summary>
    Pn,
    /// <summary>Messages are addressed by linked-device ID (LID).</summary>
    Lid
}

// ──────────────────────────────────────────────────────────────────────────────
//  Message key
// ──────────────────────────────────────────────────────────────────────────────

/// <summary>
/// Identifies a specific WhatsApp message — a trimmed version of the
/// proto.IMessageKey type that does not require protobuf.
/// </summary>
public sealed class WaMessageKey
{
    /// <summary>JID of the chat this message belongs to.</summary>
    public string? RemoteJid { get; init; }

    /// <summary>Alternative remote JID (used in LID/PN transitions).</summary>
    public string? RemoteJidAlt { get; init; }

    /// <summary>Whether the message was sent by the authenticated account.</summary>
    public bool? FromMe { get; init; }

    /// <summary>Unique message ID string.</summary>
    public string? Id { get; init; }

    /// <summary>JID of the participant who sent the message (in groups).</summary>
    public string? Participant { get; init; }

    /// <summary>Alternative participant JID (used in LID/PN transitions).</summary>
    public string? ParticipantAlt { get; init; }

    /// <summary>Server-assigned message ID.</summary>
    public string? ServerId { get; init; }

    /// <summary>Addressing mode for this message.</summary>
    public string? AddressingMode { get; init; }

    /// <summary>Whether the message was sent as view-once.</summary>
    public bool? IsViewOnce { get; init; }
}

// ──────────────────────────────────────────────────────────────────────────────
//  Message receipt / upsert type
// ──────────────────────────────────────────────────────────────────────────────

/// <summary>Reason a <c>messages.upsert</c> event was emitted.</summary>
public enum MessageUpsertType
{
    /// <summary>Message received while the socket was connected.</summary>
    Notify,
    /// <summary>Message loaded from history or app-state sync.</summary>
    Append
}

/// <summary>Type of receipt attached to a message status update.</summary>
public enum MessageReceiptType
{
    Read,
    ReadSelf,
    HistSync,
    PeerMsg,
    Sender,
    Inactive,
    Played
}

// ──────────────────────────────────────────────────────────────────────────────
//  Media types
// ──────────────────────────────────────────────────────────────────────────────

/// <summary>All WhatsApp media types that Baileys supports.</summary>
public enum MediaType
{
    Audio,
    Document,
    Gif,
    Image,
    Ppic,
    Product,
    Ptt,
    Sticker,
    Video,
    ThumbnailDocument,
    ThumbnailImage,
    ThumbnailVideo,
    ThumbnailLink,
    MdMsgHist,
    MdAppState,
    ProductCatalogImage,
    PaymentBgImage,
    Ptv,
    BizCoverPhoto
}

// ──────────────────────────────────────────────────────────────────────────────
//  Media connectivity info
// ──────────────────────────────────────────────────────────────────────────────

/// <summary>A single media upload/download host.</summary>
public sealed class MediaConnHost
{
    public required string Hostname { get; init; }
    public long MaxContentLengthBytes { get; init; }
}

/// <summary>Response from the WhatsApp media-connection endpoint.</summary>
public sealed class MediaConnInfo
{
    public required string Auth { get; init; }
    public int Ttl { get; init; }
    public IReadOnlyList<MediaConnHost> Hosts { get; init; } = [];
    public DateTimeOffset FetchDate { get; init; }
}

// ──────────────────────────────────────────────────────────────────────────────
//  URL info (link preview)
// ──────────────────────────────────────────────────────────────────────────────

/// <summary>Metadata extracted from a URL for a link preview.</summary>
public sealed class WaUrlInfo
{
    public required string CanonicalUrl { get; init; }
    public required string MatchedText { get; init; }
    public required string Title { get; init; }
    public string? Description { get; init; }
    public byte[]? JpegThumbnail { get; init; }
    public string? OriginalThumbnailUrl { get; init; }
}

// ──────────────────────────────────────────────────────────────────────────────
//  Minimal message (used in history processing)
// ──────────────────────────────────────────────────────────────────────────────

/// <summary>Minimal representation of a WhatsApp message (no proto dependency).</summary>
public sealed class MinimalMessage
{
    public required WaMessageKey Key { get; init; }
    public long? MessageTimestamp { get; init; }
}

// ──────────────────────────────────────────────────────────────────────────────
//  User receipt update
// ──────────────────────────────────────────────────────────────────────────────

/// <summary>A single receipt entry for a message.</summary>
public sealed class UserReceipt
{
    public required string UserJid { get; init; }
    public long? ReadTimestamp { get; init; }
    public long? DeliveryTimestamp { get; init; }
    public long? PlayedTimestamp { get; init; }
    public string[]? PendingDeviceJids { get; init; }
    public string[]? DeliveredDeviceJids { get; init; }
}

/// <summary>Update event for a message's per-user receipts.</summary>
public sealed class MessageUserReceiptUpdate
{
    public required WaMessageKey Key { get; init; }
    public required UserReceipt Receipt { get; init; }
}

// ──────────────────────────────────────────────────────────────────────────────
//  Poll message options
// ──────────────────────────────────────────────────────────────────────────────

/// <summary>Options for creating a poll message.</summary>
public sealed class PollMessageOptions
{
    public required string Name { get; init; }
    public int? SelectableCount { get; init; }
    public required IReadOnlyList<string> Values { get; init; }
    /// <summary>32-byte secret to encrypt poll selections.</summary>
    public byte[]? MessageSecret { get; init; }
    public bool ToAnnouncementGroup { get; init; }
}
