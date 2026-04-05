using System.Text.RegularExpressions;
using Baileys.Types;

namespace Baileys.Utils;

/// <summary>
/// JID (Jabber-ID) encode/decode utilities that mirror
/// <c>WABinary/jid-utils.ts</c>.
/// </summary>
public static partial class JidUtils
{
    // ──────────────────────────────────────────────────────────
    //  Server string ↔ enum helpers
    // ──────────────────────────────────────────────────────────

    /// <summary>Returns the canonical string for a <see cref="JidServer"/> value.</summary>
    public static string ServerToString(JidServer server) => server switch
    {
        JidServer.ContactUs       => "c.us",
        JidServer.GroupUs         => "g.us",
        JidServer.Broadcast       => "broadcast",
        JidServer.SWhatsappNet    => "s.whatsapp.net",
        JidServer.Call            => "call",
        JidServer.Lid             => "lid",
        JidServer.Newsletter      => "newsletter",
        JidServer.Bot             => "bot",
        JidServer.Hosted          => "hosted",
        JidServer.HostedLid       => "hosted.lid",
        _                         => throw new ArgumentOutOfRangeException(nameof(server))
    };

    /// <summary>Parses a raw server string into a <see cref="JidServer"/>.</summary>
    public static JidServer ServerFromString(string server) => server switch
    {
        "c.us"           => JidServer.ContactUs,
        "g.us"           => JidServer.GroupUs,
        "broadcast"      => JidServer.Broadcast,
        "s.whatsapp.net" => JidServer.SWhatsappNet,
        "call"           => JidServer.Call,
        "lid"            => JidServer.Lid,
        "newsletter"     => JidServer.Newsletter,
        "bot"            => JidServer.Bot,
        "hosted"         => JidServer.Hosted,
        "hosted.lid"     => JidServer.HostedLid,
        _                => JidServer.ContactUs  // fallback
    };

    // ──────────────────────────────────────────────────────────
    //  Encode
    // ──────────────────────────────────────────────────────────

    /// <summary>
    /// Encodes a JID from its components, e.g. ("123", JidServer.ContactUs)
    /// → "123@c.us".
    /// </summary>
    public static string JidEncode(
        string? user,
        JidServer server,
        int? device = null,
        int? agent = null)
    {
        var sb = new System.Text.StringBuilder();
        sb.Append(user ?? string.Empty);
        if (agent.HasValue) sb.Append('_').Append(agent.Value);
        if (device.HasValue) sb.Append(':').Append(device.Value);
        sb.Append('@').Append(ServerToString(server));
        return sb.ToString();
    }

    // ──────────────────────────────────────────────────────────
    //  Decode
    // ──────────────────────────────────────────────────────────

    /// <summary>
    /// Decodes a full JID string into a <see cref="FullJid"/>, or returns
    /// <c>null</c> when the string is not a valid JID.
    /// </summary>
    public static FullJid? JidDecode(string? jid)
    {
        if (string.IsNullOrEmpty(jid)) return null;

        int atIdx = jid.IndexOf('@');
        if (atIdx < 0) return null;

        var serverStr = jid[(atIdx + 1)..];
        var userCombined = jid[..atIdx];

        // Split on ':' for device
        var colonIdx = userCombined.IndexOf(':');
        string userAgent = colonIdx >= 0 ? userCombined[..colonIdx] : userCombined;
        int? device = null;
        if (colonIdx >= 0)
        {
            if (!int.TryParse(userCombined[(colonIdx + 1)..], out int deviceVal))
                return null;
            device = deviceVal;
        }

        // Split on '_' for agent / domain type
        var underIdx = userAgent.IndexOf('_');
        string user = underIdx >= 0 ? userAgent[..underIdx] : userAgent;
        int? domainType = null;
        if (underIdx >= 0)
        {
            if (!int.TryParse(userAgent[(underIdx + 1)..], out int domainVal))
                return null;
            domainType = domainVal;
        }

        var server = serverStr switch
        {
            "lid"        => JidServer.Lid,
            "hosted"     => JidServer.Hosted,
            "hosted.lid" => JidServer.HostedLid,
            _            => ServerFromString(serverStr)
        };

        // Infer domainType from server when not encoded in the user part
        if (!domainType.HasValue)
        {
            domainType = server switch
            {
                JidServer.Lid       => (int)WaJidDomains.Lid,
                JidServer.Hosted    => (int)WaJidDomains.Hosted,
                JidServer.HostedLid => (int)WaJidDomains.HostedLid,
                _                   => (int)WaJidDomains.WhatsApp
            };
        }

        return new FullJid(user, server, device, domainType);
    }

    // ──────────────────────────────────────────────────────────
    //  Normalisation
    // ──────────────────────────────────────────────────────────

    /// <summary>
    /// Normalises a user JID: converts the server from <c>c.us</c> to
    /// <c>s.whatsapp.net</c> if necessary.
    /// </summary>
    public static string JidNormalizedUser(string? jid)
    {
        var decoded = JidDecode(jid);
        if (decoded is null) return string.Empty;
        var server = decoded.Server == JidServer.ContactUs ? JidServer.SWhatsappNet : decoded.Server;
        return JidEncode(decoded.User, server);
    }

    // ──────────────────────────────────────────────────────────
    //  Predicate helpers
    // ──────────────────────────────────────────────────────────

    /// <summary>Returns <c>true</c> when both JIDs refer to the same user.</summary>
    public static bool AreJidsSameUser(string? jid1, string? jid2)
        => JidDecode(jid1)?.User == JidDecode(jid2)?.User;

    /// <summary>Returns <c>true</c> when <paramref name="jid"/> is a Meta AI JID.</summary>
    public static bool IsJidMetaAi(string? jid) => jid?.EndsWith("@bot") == true;

    /// <summary>Returns <c>true</c> when <paramref name="jid"/> is on s.whatsapp.net.</summary>
    public static bool IsPnUser(string? jid) => jid?.EndsWith("@s.whatsapp.net") == true;

    /// <summary>Returns <c>true</c> when <paramref name="jid"/> is a LID.</summary>
    public static bool IsLidUser(string? jid) => jid?.EndsWith("@lid") == true;

    /// <summary>Returns <c>true</c> when <paramref name="jid"/> is a broadcast address.</summary>
    public static bool IsJidBroadcast(string? jid) => jid?.EndsWith("@broadcast") == true;

    /// <summary>Returns <c>true</c> when <paramref name="jid"/> is a group JID.</summary>
    public static bool IsJidGroup(string? jid) => jid?.EndsWith("@g.us") == true;

    /// <summary>Returns <c>true</c> when <paramref name="jid"/> is the status broadcast.</summary>
    public static bool IsJidStatusBroadcast(string? jid) => jid == "status@broadcast";

    /// <summary>Returns <c>true</c> when <paramref name="jid"/> is a newsletter.</summary>
    public static bool IsJidNewsletter(string? jid) => jid?.EndsWith("@newsletter") == true;

    /// <summary>Returns <c>true</c> when <paramref name="jid"/> is a hosted PN JID.</summary>
    public static bool IsHostedPnUser(string? jid) => jid?.EndsWith("@hosted") == true;

    /// <summary>Returns <c>true</c> when <paramref name="jid"/> is a hosted LID JID.</summary>
    public static bool IsHostedLidUser(string? jid) => jid?.EndsWith("@hosted.lid") == true;

    [GeneratedRegex(@"^1313555\d{4}$|^131655500\d{2}$")]
    private static partial Regex BotRegex();

    /// <summary>
    /// Returns <c>true</c> when <paramref name="jid"/> is a known WhatsApp bot JID.
    /// </summary>
    public static bool IsJidBot(string? jid)
    {
        if (jid is null) return false;
        var atIdx = jid.IndexOf('@');
        if (atIdx < 0 || !jid.EndsWith("@c.us")) return false;
        return BotRegex().IsMatch(jid[..atIdx]);
    }

    // ──────────────────────────────────────────────────────────
    //  Transfer-device helper
    // ──────────────────────────────────────────────────────────

    /// <summary>
    /// Copies the device part from <paramref name="fromJid"/> to
    /// <paramref name="toJid"/>.
    /// </summary>
    public static string TransferDevice(string fromJid, string toJid)
    {
        var from = JidDecode(fromJid);
        var to = JidDecode(toJid);
        if (to is null) throw new ArgumentException("Invalid JID", nameof(toJid));
        return JidEncode(to.User, to.Server, from?.Device ?? 0);
    }
}
