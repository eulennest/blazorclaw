namespace Baileys.Types;

/// <summary>Represents the WhatsApp JID (Jabber ID) server domains.</summary>
public enum JidServer
{
    /// <summary>Regular WhatsApp user server (c.us).</summary>
    ContactUs,
    /// <summary>WhatsApp group server (g.us).</summary>
    GroupUs,
    /// <summary>Broadcast server.</summary>
    Broadcast,
    /// <summary>Signal protocol server (s.whatsapp.net).</summary>
    SWhatsappNet,
    /// <summary>Call server.</summary>
    Call,
    /// <summary>Linked-device ID server.</summary>
    Lid,
    /// <summary>Newsletter server.</summary>
    Newsletter,
    /// <summary>Bot server.</summary>
    Bot,
    /// <summary>Hosted PN server.</summary>
    Hosted,
    /// <summary>Hosted LID server.</summary>
    HostedLid
}

/// <summary>Well-known WhatsApp JID constants.</summary>
public static class WellKnownJids
{
    public const string SWhatsappNet = "@s.whatsapp.net";
    public const string OfficialBizJid = "16505361212@c.us";
    public const string ServerJid = "server@c.us";
    public const string PsaWid = "0@c.us";
    public const string StoriesJid = "status@broadcast";
    public const string MetaAiJid = "13135550002@c.us";
}

/// <summary>Domain type codes used in the binary WA protocol.</summary>
public enum WaJidDomains
{
    WhatsApp = 0,
    Lid = 1,
    Hosted = 128,
    HostedLid = 129
}

/// <summary>A decoded JID including user, device, server, and domain type.</summary>
public sealed record FullJid(string User, JidServer Server, int? Device = null, int? DomainType = null);
