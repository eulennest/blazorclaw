namespace Baileys.Options;

/// <summary>
/// Configuration options for a Baileys client instance.
/// Bind from <c>appsettings.json</c> via the <c>"Baileys"</c> section, or configure
/// programmatically through <c>AddBaileys(o => …)</c>.
/// </summary>
public sealed class BaileysOptions
{
    /// <summary>The default configuration section name when binding from IConfiguration.</summary>
    public const string SectionName = "Baileys";

    /// <summary>
    /// The WhatsApp phone number (without '+', e.g. <c>"15551234567"</c>) used to
    /// identify this session.  Maps to the concept of a "chat ID" (JID user part).
    /// </summary>
    public string PhoneNumber { get; set; } = string.Empty;

    /// <summary>
    /// Optional override for the default JID server suffix used when encoding JIDs.
    /// Defaults to <c>s.whatsapp.net</c> when <see langword="null"/>.
    /// </summary>
    public string? JidServer { get; set; }

    /// <summary>
    /// Maximum number of pre-keys to generate on first connect.
    /// Defaults to <c>31</c> (matches the Baileys TypeScript default).
    /// </summary>
    public int InitialPreKeyCount { get; set; } = 31;

    /// <summary>
    /// How long (in milliseconds) to wait before retrying a dropped connection.
    /// Defaults to <c>3000</c> ms.
    /// </summary>
    public int RetryRequestDelayMs { get; set; } = 3_000;

    /// <summary>
    /// When <see langword="true"/>, previously archived chats are unarchived when a
    /// new message arrives in them.  Mirrors the TypeScript <c>AccountSettings.unarchiveChats</c>.
    /// </summary>
    public bool UnarchiveChats { get; set; }

    /// <summary>
    /// When <see langword="true"/>, the QR code is automatically printed to the terminal
    /// when it arrives.  Defaults to <see langword="true"/>.
    /// </summary>
    public bool PrintQrInTerminal { get; set; } = true;
}
