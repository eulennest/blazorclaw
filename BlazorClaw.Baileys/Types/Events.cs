namespace Baileys.Types;

// ──────────────────────────────────────────────────────────────────────────────
//  Baileys event map — mirrors BaileysEventMap in Types/Events.ts
// ──────────────────────────────────────────────────────────────────────────────

// These classes represent the payload for each event that the Baileys client emits.
// They are plain data carriers; actual event subscription is handled by the
// IBaileysEventEmitter interface (see Utils/EventEmitter.cs).

/// <summary>Payload for <c>connection.update</c>.</summary>
public sealed class ConnectionUpdateEventArgs : EventArgs
{
    public WaConnectionState? Connection { get; init; }
    public LastDisconnectInfo? LastDisconnect { get; init; }
    public bool? IsNewLogin { get; init; }
    public string? Qr { get; init; }
    public bool? ReceivedPendingNotifications { get; init; }
    public bool? IsOnline { get; init; }
}

/// <summary>Payload for <c>messaging-history.set</c> (history sync).</summary>
public sealed class MessagingHistorySetEvent
{
    public IReadOnlyList<Chat> Chats { get; init; } = [];
    public IReadOnlyList<Contact> Contacts { get; init; } = [];
    public IReadOnlyList<MinimalMessage> Messages { get; init; } = [];
    public IReadOnlyList<LidMapping>? LidPnMappings { get; init; }
    public bool? IsLatest { get; init; }
    public int? Progress { get; init; }
    public int? SyncType { get; init; }
    public string? PeerDataRequestSessionId { get; init; }
}

/// <summary>Payload for <c>presence.update</c>.</summary>
public sealed class PresenceUpdateEventArgs : EventArgs
{
    public required string Id { get; init; }
    public required Dictionary<string, PresenceData> Presences { get; init; }
}

/// <summary>Payload for <c>messages.delete</c>.</summary>
public sealed class MessagesDeleteEventArgs : EventArgs
{
    /// <summary>Specific message keys to delete, or null when <see cref="All"/> is set.</summary>
    public IReadOnlyList<WaMessageKey>? Keys { get; init; }

    /// <summary>JID to delete all messages from (when set, <see cref="Keys"/> is null).</summary>
    public string? Jid { get; init; }

    /// <summary>Whether to delete all messages in the chat.</summary>
    public bool All { get; init; }
}

/// <summary>A single message update entry.</summary>
public sealed class WaMessageUpdateEventArgs : EventArgs
{
    public required WaMessageKey Key { get; init; }
    public required MessageUpdatePayload Update { get; init; }
}

/// <summary>Partial payload carried in a message update.</summary>
public sealed class MessageUpdatePayload
{
    public int? Status { get; init; }
    public long? MessageTimestamp { get; init; }
}

/// <summary>Payload for a media update on a message.</summary>
public sealed class MessageMediaUpdateEventArgs : EventArgs
{
    public required WaMessageKey Key { get; init; }
    public MessageMediaPayload? Media { get; init; }
    public Exception? Error { get; init; }
}

/// <summary>Raw media cipher payload.</summary>
public sealed class MessageMediaPayload
{
    public required byte[] Ciphertext { get; init; }
    public required byte[] Iv { get; init; }
}

/// <summary>Payload for <c>messages.upsert</c>.</summary>
public sealed class MessagesUpsertEventArgs : EventArgs
{
    public IReadOnlyList<MinimalMessage> Messages { get; init; } = [];
    public MessageUpsertType Type { get; init; }
    public string? RequestId { get; init; }
}

/// <summary>A reaction on a message.</summary>
public sealed class MessageReactionEventArgs : EventArgs
{
    public required WaMessageKey Key { get; init; }
    public string? ReactionText { get; init; }
    public long? Timestamp { get; init; }
}

/// <summary>Payload for <c>group-participants.update</c>.</summary>
public sealed class GroupParticipantsUpdateEventArgs : EventArgs
{
    public required string Id { get; init; }
    public required string Author { get; init; }
    public string? AuthorPn { get; init; }
    public IReadOnlyList<GroupParticipant> Participants { get; init; } = [];
    public ParticipantAction Action { get; init; }
}

/// <summary>Payload for <c>group.join-request</c>.</summary>
public sealed class GroupJoinRequestEventArgs : EventArgs
{
    public required string Id { get; init; }
    public required string Author { get; init; }
    public string? AuthorPn { get; init; }
    public required string Participant { get; init; }
    public string? ParticipantPn { get; init; }
    public RequestJoinAction Action { get; init; }
    public RequestJoinMethod? Method { get; init; }
}

/// <summary>Payload for <c>group.member-tag.update</c>.</summary>
public sealed class GroupMemberTagUpdateEventArgs : EventArgs
{
    public required string GroupId { get; init; }
    public required string Participant { get; init; }
    public string? ParticipantAlt { get; init; }
    public required string LabelValue { get; init; }
    public long? MessageTimestamp { get; init; }
}

/// <summary>Payload for <c>blocklist.set</c> / <c>blocklist.update</c>.</summary>
public sealed class BlocklistUpdateEventArgs : EventArgs
{
    public IReadOnlyList<string> Blocklist { get; init; } = [];
    /// <summary>"add" or "remove" — null for the full <c>blocklist.set</c> event.</summary>
    public string? Type { get; init; }
}

/// <summary>Payload for a label association event.</summary>
public sealed class LabelAssociationEventArgs : EventArgs
{
    public required object Association { get; init; }
    public required string Type { get; init; }
}

/// <summary>Newsletter reaction event payload.</summary>
public sealed class NewsletterReactionEventArgs : EventArgs
{
    public required string Id { get; init; }
    public required string ServerId { get; init; }
    public string? ReactionCode { get; init; }
    public int? Count { get; init; }
    public bool Removed { get; init; }
}

/// <summary>Newsletter view event payload.</summary>
public sealed class NewsletterViewEventArgs : EventArgs
{
    public required string Id { get; init; }
    public required string ServerId { get; init; }
    public int Count { get; init; }
}

/// <summary>Newsletter-participants update event payload.</summary>
public sealed class NewsletterParticipantsUpdateEventArgs : EventArgs
{
    public required string Id { get; init; }
    public required string Author { get; init; }
    public required string User { get; init; }
    public required string NewRole { get; init; }
    public required string Action { get; init; }
}

/// <summary>Chat lock event payload.</summary>
public sealed class ChatLockEventArgs : EventArgs
{
    public required string Id { get; init; }
    public bool Locked { get; init; }
}

/// <summary>LID mapping update event payload.</summary>
public sealed class LidMappingUpdateEventArgs : EventArgs
{
    public required string PhoneNumber { get; init; }
    public required string Lid { get; init; }
}
