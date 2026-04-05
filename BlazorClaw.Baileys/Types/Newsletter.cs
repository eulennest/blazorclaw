namespace Baileys.Types;

// ──────────────────────────────────────────────────────────────────────────────
//  Newsletter paths / query IDs
// ──────────────────────────────────────────────────────────────────────────────

/// <summary>GraphQL XWA path constants for newsletter operations.</summary>
public static class XWaPaths
{
    public const string Create         = "xwa2_newsletter_create";
    public const string Subscribers    = "xwa2_newsletter_subscribers";
    public const string View           = "xwa2_newsletter_view";
    public const string Metadata       = "xwa2_newsletter";
    public const string AdminCount     = "xwa2_newsletter_admin";
    public const string MuteV2         = "xwa2_newsletter_mute_v2";
    public const string UnmuteV2       = "xwa2_newsletter_unmute_v2";
    public const string Follow         = "xwa2_newsletter_follow";
    public const string Unfollow       = "xwa2_newsletter_unfollow";
    public const string ChangeOwner    = "xwa2_newsletter_change_owner";
    public const string Demote         = "xwa2_newsletter_demote";
    public const string DeleteV2       = "xwa2_newsletter_delete_v2";
}

/// <summary>Internal GraphQL query IDs for newsletter operations.</summary>
public static class NewsletterQueryIds
{
    public const string Create        = "8823471724422422";
    public const string UpdateMetadata = "24250201037901610";
    public const string Metadata      = "6563316087068696";
    public const string Subscribers   = "9783111038412085";
    public const string Follow        = "7871414976211147";
    public const string Unfollow      = "7238632346214362";
    public const string Mute          = "29766401636284406";
    public const string Unmute        = "9864994326891137";
    public const string AdminCount    = "7130823597031706";
    public const string ChangeOwner   = "7341777602580933";
    public const string Demote        = "6551828931592903";
    public const string Delete        = "30062808666639665";
}

// ──────────────────────────────────────────────────────────────────────────────
//  Newsletter types
// ──────────────────────────────────────────────────────────────────────────────

/// <summary>Role of the authenticated user in a newsletter.</summary>
public enum NewsletterViewRole
{
    Admin,
    Guest,
    Owner,
    Subscriber
}

/// <summary>Fields that can be updated on a newsletter.</summary>
public sealed class NewsletterUpdate
{
    public string? Name { get; init; }
    public string? Description { get; init; }
    public string? Picture { get; init; }
}

/// <summary>Reaction entry on a newsletter post.</summary>
public sealed class NewsletterReactionEntry
{
    public required string Code { get; init; }
    public int Count { get; init; }
}

/// <summary>Full newsletter metadata.</summary>
public sealed class NewsletterMetadata
{
    public required string Id { get; init; }
    public string? Owner { get; init; }
    public required string Name { get; init; }
    public string? Description { get; init; }
    public string? Invite { get; init; }
    public long? CreationTime { get; init; }
    public int? Subscribers { get; init; }
    public NewsletterPicture? Picture { get; init; }
    public string? Verification { get; init; }
    public IReadOnlyList<NewsletterReactionEntry>? ReactionCodes { get; init; }
    public string? MuteState { get; init; }
}

/// <summary>Picture reference on a newsletter.</summary>
public sealed class NewsletterPicture
{
    public string? Url { get; init; }
    public string? DirectPath { get; init; }
    public string? MediaKey { get; init; }
    public string? Id { get; init; }
}

/// <summary>Response after creating a newsletter.</summary>
public sealed class NewsletterCreateResponse
{
    public required string Id { get; init; }
    public NewsletterState? State { get; init; }
    public NewsletterThreadMetadata? ThreadMetadata { get; init; }
    public NewsletterViewerMetadata? ViewerMetadata { get; init; }
}

public sealed class NewsletterState
{
    public string? Type { get; init; }
}

public sealed class NewsletterThreadMetadata
{
    public string? CreationTime { get; init; }
    public NewsletterTextField? Description { get; init; }
    public string? Handle { get; init; }
    public string? Invite { get; init; }
    public NewsletterTextField? Name { get; init; }
    public NewsletterMediaField? Picture { get; init; }
    public NewsletterMediaField? Preview { get; init; }
    public string? SubscribersCount { get; init; }
    public string? Verification { get; init; }
}

public sealed class NewsletterTextField
{
    public string? Id { get; init; }
    public string? Text { get; init; }
    public string? UpdateTime { get; init; }
}

public sealed class NewsletterMediaField
{
    public string? DirectPath { get; init; }
    public string? Id { get; init; }
    public string? Type { get; init; }
}

public sealed class NewsletterViewerMetadata
{
    public string? Mute { get; init; }
    public NewsletterViewRole Role { get; init; }
}
