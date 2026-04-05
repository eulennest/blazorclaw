namespace Baileys.Types;

// ──────────────────────────────────────────────────────────────────────────────
//  Chat
// ──────────────────────────────────────────────────────────────────────────────

/// <summary>
/// Represents a WhatsApp chat (conversation), mirroring
/// the TypeScript <c>Types/Chat.ts</c> Chat type.
/// </summary>
public sealed class Chat
{
    /// <summary>Chat JID.</summary>
    public required string Id { get; init; }

    /// <summary>Unread message count.</summary>
    public int? UnreadCount { get; init; }

    /// <summary>Whether the chat is archived.</summary>
    public bool? Archive { get; init; }

    /// <summary>Whether the chat is pinned.</summary>
    public bool? Pinned { get; init; }

    /// <summary>Unix timestamp (ms) of the mute expiry — null means unmuted.</summary>
    public long? Mute { get; init; }

    /// <summary>Whether messages in this chat disappear after the ephemeral duration.</summary>
    public int? EphemeralExpiration { get; init; }

    /// <summary>Unix timestamp (seconds) when disappearing messages were last configured.</summary>
    public long? EphemeralSettingTimestamp { get; init; }

    /// <summary>Whether this chat is marked as unread.</summary>
    public bool? MarkedAsUnread { get; init; }

    /// <summary>Unix timestamp (seconds) when the last message was received.</summary>
    public long? LastMessageRecvTimestamp { get; init; }

    /// <summary>Chat display name (contact / group name).</summary>
    public string? Name { get; init; }

    /// <summary>Whether this chat is read-only (newsletter, etc.).</summary>
    public bool? ReadOnly { get; init; }

    /// <summary>Whether the chat is locked.</summary>
    public bool? Locked { get; init; }
}

/// <summary>
/// Partial update to a chat — mirrors the TypeScript <c>ChatUpdate</c> type.
/// </summary>
public sealed class ChatUpdate
{
    public string? Id { get; init; }
    public int? UnreadCount { get; init; }
    public bool? Archive { get; init; }
    public bool? Pinned { get; init; }
    public long? Mute { get; init; }
    public int? EphemeralExpiration { get; init; }
    public long? EphemeralSettingTimestamp { get; init; }
    public bool? MarkedAsUnread { get; init; }
    public long? LastMessageRecvTimestamp { get; init; }
    public string? Name { get; init; }
    public bool? ReadOnly { get; init; }
    public bool? Locked { get; init; }
}

// ──────────────────────────────────────────────────────────────────────────────
//  Privacy values
// ──────────────────────────────────────────────────────────────────────────────

/// <summary>Combined chat modification payload.</summary>
public abstract class ChatModification { }

public sealed class ArchiveChatModification : ChatModification
{
    public required bool Archive { get; init; }
    public required IReadOnlyList<MinimalMessage> LastMessages { get; init; }
}

public sealed class PinChatModification : ChatModification
{
    public required bool Pin { get; init; }
}

public sealed class MuteChatModification : ChatModification
{
    /// <summary>Duration in ms to mute for, or null to unmute.</summary>
    public long? Mute { get; init; }
}

public sealed class MarkReadChatModification : ChatModification
{
    public required bool MarkRead { get; init; }
    public required IReadOnlyList<MinimalMessage> LastMessages { get; init; }
}

public sealed class DeleteChatModification : ChatModification
{
    public required IReadOnlyList<MinimalMessage> LastMessages { get; init; }
}

public sealed class ClearChatModification : ChatModification
{
    public required bool Clear { get; init; }
    public required IReadOnlyList<MinimalMessage> LastMessages { get; init; }
}

public sealed class AddLabelChatModification : ChatModification
{
    public required LabelActionBody Label { get; init; }
}

public sealed class AddChatLabelModification : ChatModification
{
    public required ChatLabelAssociationActionBody LabelAssoc { get; init; }
}

public sealed class RemoveChatLabelModification : ChatModification
{
    public required ChatLabelAssociationActionBody LabelAssoc { get; init; }
}
