using Baileys.Types;
using Baileys.Utils;
using System.Text;

namespace Baileys.WABinary;

/// <summary>
/// Encodes <see cref="BinaryNode"/> trees into the WhatsApp binary wire format,
/// mirroring <c>WABinary/encode.ts</c>.
/// </summary>
public static class WaBinaryEncoder
{
    // Build the reverse lookup table once at startup.
    private static readonly Dictionary<string, (int? Dict, int Index)> TokenMap = BuildTokenMap();

    private static Dictionary<string, (int? Dict, int Index)> BuildTokenMap()
    {
        var map = new Dictionary<string, (int?, int)>(StringComparer.Ordinal);
        for (int i = 0; i < WaBinaryConstants.SingleByteTokens.Length; i++)
        {
            var token = WaBinaryConstants.SingleByteTokens[i];
            if (!string.IsNullOrEmpty(token))
                map[token] = (null, i);
        }
        for (int d = 0; d < WaBinaryConstants.DoubleByteTokens.Length; d++)
        {
            var dict = WaBinaryConstants.DoubleByteTokens[d];
            for (int j = 0; j < dict.Length; j++)
            {
                if (!string.IsNullOrEmpty(dict[j]))
                    map[dict[j]] = (d, j);
            }
        }
        return map;
    }

    // ──────────────────────────────────────────────────────────
    //  Public API
    // ──────────────────────────────────────────────────────────

    /// <summary>
    /// Encodes a <see cref="BinaryNode"/> tree into the WhatsApp binary wire
    /// format, with a leading 0x00 (no-compression) byte.
    /// </summary>
    public static byte[] EncodeBinaryNode(BinaryNode node)
    {
        var buffer = new List<byte> { 0 };   // leading 0x00 = uncompressed
        EncodeNode(node, buffer);
        return [.. buffer];
    }

    // ──────────────────────────────────────────────────────────
    //  Internals
    // ──────────────────────────────────────────────────────────

    private static void EncodeNode(BinaryNode node, List<byte> buf)
    {
        if (string.IsNullOrEmpty(node.Tag))
            throw new ArgumentException("BinaryNode tag cannot be null or empty.");

        var validAttrs = node.Attrs
            .Where(kv => kv.Value is not null)
            .ToList();

        int listSize = 1                                             // tag
            + (2 * validAttrs.Count)                                // key + value pairs
            + (node.Content is not null ? 1 : 0);                  // content token

        WriteListStart(listSize, buf);
        WriteString(node.Tag, buf);

        foreach (var (key, val) in validAttrs)
        {
            WriteString(key, buf);
            WriteString(val, buf);
        }

        switch (node.Content)
        {
            case null:
                break;

            case BinaryNodeString s:
                WriteString(s.Value, buf);
                break;

            case BinaryNodeBytes b:
                WriteByteLength(b.Data.Length, buf);
                buf.AddRange(b.Data);
                break;

            case BinaryNodeList list:
                WriteListStart(list.Children.Count, buf);
                foreach (var child in list.Children)
                    EncodeNode(child, buf);
                break;

            default:
                throw new ArgumentException($"Unsupported BinaryNode content type: {node.Content.GetType()}");
        }
    }

    // ── String writing ───────────────────────────────────────

    private static void WriteString(string? str, List<byte> buf)
    {
        if (str is null)
        {
            buf.Add(WaBinaryConstants.Tags.ListEmpty);
            return;
        }

        if (str == "")
        {
            WriteStringRaw(str, buf);
            return;
        }

        if (TokenMap.TryGetValue(str, out var tok))
        {
            if (tok.Dict.HasValue)
                buf.Add((byte)(WaBinaryConstants.Tags.Dictionary0 + tok.Dict.Value));
            buf.Add((byte)tok.Index);
            return;
        }

        if (IsNibble(str)) { WritePackedBytes(str, 'n', buf); return; }
        if (IsHex(str)) { WritePackedBytes(str, 'h', buf); return; }

        var decoded = JidUtils.JidDecode(str);
        if (decoded is not null)
        {
            WriteJid(decoded, buf);
            return;
        }

        WriteStringRaw(str, buf);
    }

    private static void WriteStringRaw(string str, List<byte> buf)
    {
        var bytes = Encoding.UTF8.GetBytes(str);
        WriteByteLength(bytes.Length, buf);
        buf.AddRange(bytes);
    }

    private static void WriteJid(FullJid jid, List<byte> buf)
    {
        if (jid.Device.HasValue)
        {
            buf.Add(WaBinaryConstants.Tags.AdJid);
            buf.Add((byte)(jid.DomainType ?? 0));
            buf.Add((byte)(jid.Device ?? 0));
            WriteString(jid.User, buf);
        }
        else
        {
            buf.Add(WaBinaryConstants.Tags.JidPair);
            if (jid.User.Length > 0)
                WriteString(jid.User, buf);
            else
                buf.Add(WaBinaryConstants.Tags.ListEmpty);
            WriteString(JidUtils.ServerToString(jid.Server), buf);
        }
    }

    // ── Byte-length prefix ───────────────────────────────────

    private static void WriteByteLength(int length, List<byte> buf)
    {
        if (length >= (1 << 20))
        {
            buf.Add(WaBinaryConstants.Tags.Binary32);
            PushInt(length, 4, buf);
        }
        else if (length >= 256)
        {
            buf.Add(WaBinaryConstants.Tags.Binary20);
            PushInt20(length, buf);
        }
        else
        {
            buf.Add(WaBinaryConstants.Tags.Binary8);
            buf.Add((byte)length);
        }
    }

    private static void WriteListStart(int size, List<byte> buf)
    {
        if (size == 0)
            buf.Add(WaBinaryConstants.Tags.ListEmpty);
        else if (size < 256)
        {
            buf.Add(WaBinaryConstants.Tags.List8);
            buf.Add((byte)size);
        }
        else
        {
            buf.Add(WaBinaryConstants.Tags.List16);
            buf.Add((byte)((size >> 8) & 0xFF));
            buf.Add((byte)(size & 0xFF));
        }
    }

    // ── Packed (nibble / hex) ────────────────────────────────

    private static void WritePackedBytes(string str, char type, List<byte> buf)
    {
        if (str.Length > WaBinaryConstants.Tags.PackedMax)
            throw new ArgumentException("String too long to pack.");

        buf.Add((byte)(type == 'n' ? WaBinaryConstants.Tags.Nibble8 : WaBinaryConstants.Tags.Hex8));

        int rounded = (str.Length + 1) / 2;
        if (str.Length % 2 != 0) rounded |= 128;
        buf.Add((byte)rounded);

        Func<char, int> pack = type == 'n' ? PackNibble : PackHex;

        int pairs = str.Length / 2;
        for (int i = 0; i < pairs; i++)
            buf.Add((byte)((pack(str[2 * i]) << 4) | pack(str[2 * i + 1])));

        if (str.Length % 2 != 0)
            buf.Add((byte)((pack(str[^1]) << 4) | pack('\0')));
    }

    private static int PackNibble(char c) => c switch
    {
        '-' => 10,
        '.' => 11,
        '\0' => 15,
        _ => c >= '0' && c <= '9' ? c - '0' : throw new ArgumentException($"Invalid nibble char: {c}")
    };

    private static int PackHex(char c)
    {
        if (c >= '0' && c <= '9') return c - '0';
        if (c >= 'A' && c <= 'F') return 10 + c - 'A';
        if (c >= 'a' && c <= 'f') return 10 + c - 'a';
        if (c == '\0') return 15;
        throw new ArgumentException($"Invalid hex char: {c}");
    }

    private static bool IsNibble(string str)
    {
        if (str.Length > WaBinaryConstants.Tags.PackedMax) return false;
        foreach (var c in str)
            if (!((c >= '0' && c <= '9') || c == '-' || c == '.'))
                return false;
        return true;
    }

    private static bool IsHex(string str)
    {
        if (str.Length > WaBinaryConstants.Tags.PackedMax) return false;
        foreach (var c in str)
            if (!((c >= '0' && c <= '9') || (c >= 'A' && c <= 'F')))
                return false;
        return true;
    }

    // ── Int helpers ──────────────────────────────────────────

    private static void PushInt(int value, int n, List<byte> buf)
    {
        for (int i = n - 1; i >= 0; i--)
            buf.Add((byte)((value >> (i * 8)) & 0xFF));
    }

    private static void PushInt20(int value, List<byte> buf)
    {
        buf.Add((byte)((value >> 16) & 0x0F));
        buf.Add((byte)((value >> 8) & 0xFF));
        buf.Add((byte)(value & 0xFF));
    }
}
