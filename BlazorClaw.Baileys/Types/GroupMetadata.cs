namespace Baileys.Types;

/// <summary>
/// A participant in a WhatsApp group, extending <see cref="Contact"/> with
/// admin information.
/// </summary>
public sealed class GroupParticipant
{
    /// <summary>Contact ID — either in LID or JID format (preferred).</summary>
    public required string Id { get; init; }

    /// <summary>Contact ID in LID format (@lid).</summary>
    public string? Lid { get; init; }

    /// <summary>Contact ID in PN format (@s.whatsapp.net).</summary>
    public string? PhoneNumber { get; init; }

    /// <summary>Name as saved in the authenticated account's address book.</summary>
    public string? Name { get; init; }

    /// <summary>Display name set by the contact themselves.</summary>
    public string? Notify { get; init; }

    /// <summary>Whether this participant is a group admin or super-admin.</summary>
    public bool IsAdmin { get; init; }

    /// <summary>Whether this participant is a group super-admin.</summary>
    public bool IsSuperAdmin { get; init; }

    /// <summary>Admin role string: "admin", "superadmin", or null.</summary>
    public string? Admin { get; init; }
}

/// <summary>Actions that can be applied to a group participant.</summary>
public enum ParticipantAction
{
    Add,
    Remove,
    Promote,
    Demote,
    Modify
}

/// <summary>Actions for join requests.</summary>
public enum RequestJoinAction
{
    Created,
    Revoked,
    Rejected
}

/// <summary>Method by which someone joined a group.</summary>
public enum RequestJoinMethod
{
    InviteLink,
    LinkedGroupJoin,
    NonAdminAdd
}

/// <summary>
/// Full metadata for a WhatsApp group, mirroring
/// the TypeScript <c>Types/GroupMetadata.ts</c> interface.
/// </summary>
public sealed class GroupMetadata
{
    /// <summary>Group JID.</summary>
    public required string Id { get; init; }

    /// <summary>Group display name (notification name).</summary>
    public string? Notify { get; init; }

    /// <summary>Whether the group uses LID or PN addressing mode.</summary>
    public string? AddressingMode { get; init; }

    /// <summary>JID of the group owner.</summary>
    public string? Owner { get; init; }

    /// <summary>PN of the group owner.</summary>
    public string? OwnerPn { get; init; }

    /// <summary>Country code of the group owner.</summary>
    public string? OwnerCountryCode { get; init; }

    /// <summary>Group subject (display name).</summary>
    public required string Subject { get; init; }

    /// <summary>JID of the contact who last changed the subject.</summary>
    public string? SubjectOwner { get; init; }

    /// <summary>PN of the contact who last changed the subject.</summary>
    public string? SubjectOwnerPn { get; init; }

    /// <summary>Unix timestamp (seconds) when the subject was last changed.</summary>
    public long? SubjectTime { get; init; }

    /// <summary>Unix timestamp (seconds) when the group was created.</summary>
    public long? Creation { get; init; }

    /// <summary>Group description text.</summary>
    public string? Desc { get; init; }

    /// <summary>JID of the contact who set the description.</summary>
    public string? DescOwner { get; init; }

    /// <summary>PN of the contact who set the description.</summary>
    public string? DescOwnerPn { get; init; }

    /// <summary>Internal description ID.</summary>
    public string? DescId { get; init; }

    /// <summary>Unix timestamp (seconds) when the description was set.</summary>
    public long? DescTime { get; init; }

    /// <summary>If in a community, the JID of the parent community.</summary>
    public string? LinkedParent { get; init; }

    /// <summary>Only admins can change group settings when true.</summary>
    public bool Restrict { get; init; }

    /// <summary>Only admins can send messages when true.</summary>
    public bool Announce { get; init; }

    /// <summary>Members can add participants when true.</summary>
    public bool MemberAddMode { get; init; }

    /// <summary>Approval is required to join when true.</summary>
    public bool JoinApprovalMode { get; init; }

    /// <summary>Whether this is a community group.</summary>
    public bool IsCommunity { get; init; }

    /// <summary>Whether this is the announcement channel of a community.</summary>
    public bool IsCommunityAnnounce { get; init; }

    /// <summary>Current participant count.</summary>
    public int? Size { get; init; }

    /// <summary>List of current group participants.</summary>
    public IReadOnlyList<GroupParticipant> Participants { get; init; } = [];

    /// <summary>Ephemeral message duration in seconds (if set).</summary>
    public int? EphemeralDuration { get; init; }

    /// <summary>Group invite code (part of the invite link).</summary>
    public string? InviteCode { get; init; }

    /// <summary>JID of the person who triggered the last group event.</summary>
    public string? Author { get; init; }

    /// <summary>PN of the person who triggered the last group event.</summary>
    public string? AuthorPn { get; init; }
}

/// <summary>Response from a group create request.</summary>
public sealed class WaGroupCreateResponse
{
    public int Status { get; init; }
    public string? Gid { get; init; }
    public IReadOnlyList<Dictionary<string, object>>? Participants { get; init; }
}

/// <summary>Response from a group modification request (add/remove participants).</summary>
public sealed class GroupModificationResponse
{
    public int Status { get; init; }
    public Dictionary<string, object>? Participants { get; init; }
}
