namespace Baileys.Types;

/// <summary>
/// A WhatsApp contact, mirroring the TypeScript <c>Types/Contact.ts</c> interface.
/// </summary>
public sealed class Contact
{
    /// <summary>Contact ID — either in LID or JID format (preferred).</summary>
    public required string Id { get; init; }

    /// <summary>Contact ID in LID format (@lid).</summary>
    public string? Lid { get; init; }

    /// <summary>Contact ID in PN format (@s.whatsapp.net).</summary>
    public string? PhoneNumber { get; init; }

    /// <summary>Name as saved in the authenticated account's address book.</summary>
    public string? Name { get; init; }

    /// <summary>Display name set by the contact themselves on WhatsApp.</summary>
    public string? Notify { get; init; }

    /// <summary>Verified business name, if applicable.</summary>
    public string? VerifiedName { get; init; }

    /// <summary>
    /// Profile picture URL: <c>null</c> = default picture, <c>"changed"</c> = changed,
    /// any other string = URL to the picture.
    /// </summary>
    public string? ImgUrl { get; init; }

    /// <summary>Contact status message.</summary>
    public string? Status { get; init; }
}
