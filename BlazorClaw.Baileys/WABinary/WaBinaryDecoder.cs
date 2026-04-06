using Baileys.Types;
using Baileys.Utils;
using System.IO.Compression;
using System.Text;

namespace Baileys.WABinary;

/// <summary>
/// Decodes WhatsApp binary nodes from the wire format, mirroring
/// <c>WABinary/decode.ts</c>.
/// </summary>
public static class WaBinaryDecoder
{
    // ──────────────────────────────────────────────────────────
    //  Public entry points
    // ──────────────────────────────────────────────────────────

    /// <summary>
    /// Decompresses (if necessary) and decodes a raw binary-node buffer received
    /// from the WhatsApp WebSocket.
    /// </summary>
    public static async Task<BinaryNode> DecodeBinaryNodeAsync(byte[] buffer)
    {
        var decompressed = await DecompressIfRequiredAsync(buffer).ConfigureAwait(false);
        var indexRef = new IndexRef();
        return Decode(decompressed, indexRef);
    }

    /// <summary>Synchronous decode (no decompression).</summary>
    public static BinaryNode DecodeDecompressedBinaryNode(byte[] buffer)
        => Decode(buffer, new IndexRef());

    // ──────────────────────────────────────────────────────────
    //  Decompression
    // ──────────────────────────────────────────────────────────

    private static async Task<byte[]> DecompressIfRequiredAsync(byte[] buffer)
    {
        if (buffer is null || buffer.Length < 2)
            throw new ArgumentException(
                "Buffer too short to be a valid binary node frame (must be at least 2 bytes: 1 flag byte + payload).",
                nameof(buffer));

        if ((buffer[0] & 2) != 0)
        {
            // inflate (zlib)
            using var input = new MemoryStream(buffer, 1, buffer.Length - 1);
            using var deflate = new DeflateStream(input, CompressionMode.Decompress);
            using var output = new MemoryStream();
            await deflate.CopyToAsync(output).ConfigureAwait(false);
            return output.ToArray();
        }

        // Skip leading 0x00 byte
        return buffer[1..];
    }

    // ──────────────────────────────────────────────────────────
    //  Core decoder
    // ──────────────────────────────────────────────────────────

    private sealed class IndexRef { public int Index; }

    private static BinaryNode Decode(byte[] buffer, IndexRef idx)
    {
        int ReadByte()
        {
            CheckEos(buffer, idx, 1);
            return buffer[idx.Index++];
        }

        byte[] ReadBytes(int n)
        {
            CheckEos(buffer, idx, n);
            var slice = buffer[idx.Index..(idx.Index + n)];
            idx.Index += n;
            return slice;
        }

        string ReadStringFromChars(int n)
            => Encoding.UTF8.GetString(ReadBytes(n));

        int ReadInt(int n, bool littleEndian = false)
        {
            CheckEos(buffer, idx, n);
            int val = 0;
            for (int i = 0; i < n; i++)
            {
                int shift = littleEndian ? i : (n - 1 - i);
                val |= buffer[idx.Index++] << (shift * 8);
            }
            return val;
        }

        int ReadInt20()
        {
            CheckEos(buffer, idx, 3);
            return ((buffer[idx.Index++] & 15) << 16)
                 + (buffer[idx.Index++] << 8)
                 + buffer[idx.Index++];
        }

        int UnpackHex(int value)
        {
            if (value is >= 0 and < 10) return '0' + value;
            if (value is >= 10 and < 16) return 'A' + value - 10;
            throw new InvalidDataException($"Invalid hex nibble: {value}");
        }

        int UnpackNibble(int value)
        {
            if (value is >= 0 and <= 9) return '0' + value;
            return value switch
            {
                10 => '-',
                11 => '.',
                15 => '\0',
                _ => throw new InvalidDataException($"Invalid nibble: {value}")
            };
        }

        int UnpackByte(int tag, int value)
            => tag == WaBinaryConstants.Tags.Nibble8
                ? UnpackNibble(value)
                : tag == WaBinaryConstants.Tags.Hex8
                    ? UnpackHex(value)
                    : throw new InvalidDataException($"Unknown tag: {tag}");

        string ReadPacked8(int tag)
        {
            int startByte = ReadByte();
            var sb = new StringBuilder();
            for (int i = 0; i < (startByte & 127); i++)
            {
                int cur = ReadByte();
                sb.Append((char)UnpackByte(tag, (cur & 0xF0) >> 4));
                sb.Append((char)UnpackByte(tag, cur & 0x0F));
            }
            if ((startByte >> 7) != 0)
                return sb.ToString()[..^1];
            return sb.ToString();
        }

        bool IsListTag(int tag)
            => tag is WaBinaryConstants.Tags.ListEmpty
                   or WaBinaryConstants.Tags.List8
                   or WaBinaryConstants.Tags.List16;

        int ReadListSize(int tag) => tag switch
        {
            WaBinaryConstants.Tags.ListEmpty => 0,
            WaBinaryConstants.Tags.List8 => ReadByte(),
            WaBinaryConstants.Tags.List16 => ReadInt(2),
            _ => throw new InvalidDataException($"Invalid list tag: {tag}")
        };

        string ReadJidPair()
        {
            string i = ReadString(ReadByte());
            string j = ReadString(ReadByte());
            if (!string.IsNullOrEmpty(j))
                return (i ?? "") + "@" + j;
            throw new InvalidDataException($"Invalid JID pair: {i}, {j}");
        }

        string ReadAdJid()
        {
            int rawDomain = ReadByte();
            int device = ReadByte();
            string user = ReadString(ReadByte());
            var server = (WaJidDomains)rawDomain switch
            {
                WaJidDomains.Lid => JidServer.Lid,
                WaJidDomains.Hosted => JidServer.Hosted,
                WaJidDomains.HostedLid => JidServer.HostedLid,
                _ => JidServer.SWhatsappNet
            };
            return JidUtils.JidEncode(user, server, device);
        }

        string ReadFbJid()
        {
            string user = ReadString(ReadByte());
            int device = ReadInt(2);
            string server = ReadString(ReadByte());
            return $"{user}:{device}@{server}";
        }

        string GetTokenDouble(int dict, int tokenIdx)
        {
            if (dict < 0 || dict >= WaBinaryConstants.DoubleByteTokens.Length)
                throw new InvalidDataException($"Invalid double-token dict: {dict}");
            var d = WaBinaryConstants.DoubleByteTokens[dict];
            if (tokenIdx < 0 || tokenIdx >= d.Length)
                throw new InvalidDataException($"Invalid double-token index: {tokenIdx}");
            return d[tokenIdx];
        }

        string ReadString(int tag)
        {
            if (tag >= 1 && tag < WaBinaryConstants.SingleByteTokens.Length)
                return WaBinaryConstants.SingleByteTokens[tag] ?? "";

            return tag switch
            {
                WaBinaryConstants.Tags.Dictionary0 => GetTokenDouble(0, ReadByte()),
                WaBinaryConstants.Tags.Dictionary1 => GetTokenDouble(1, ReadByte()),
                WaBinaryConstants.Tags.Dictionary2 => GetTokenDouble(2, ReadByte()),
                WaBinaryConstants.Tags.Dictionary3 => GetTokenDouble(3, ReadByte()),
                WaBinaryConstants.Tags.ListEmpty => "",
                WaBinaryConstants.Tags.Binary8 => ReadStringFromChars(ReadByte()),
                WaBinaryConstants.Tags.Binary20 => ReadStringFromChars(ReadInt20()),
                WaBinaryConstants.Tags.Binary32 => ReadStringFromChars(ReadInt(4)),
                WaBinaryConstants.Tags.JidPair => ReadJidPair(),
                WaBinaryConstants.Tags.FbJid => ReadFbJid(),
                WaBinaryConstants.Tags.AdJid => ReadAdJid(),
                WaBinaryConstants.Tags.Nibble8 => ReadPacked8(tag),
                WaBinaryConstants.Tags.Hex8 => ReadPacked8(tag),
                _ => throw new InvalidDataException($"Invalid string tag: {tag}")
            };
        }

        List<BinaryNode> ReadList(int tag)
        {
            int size = ReadListSize(tag);
            var items = new List<BinaryNode>(size);
            for (int i = 0; i < size; i++)
                items.Add(Decode(buffer, idx));
            return items;
        }

        // ── Parse the node ──────────────────────────────────
        int listSize = ReadListSize(ReadByte());
        string header = ReadString(ReadByte());
        if (listSize == 0 || string.IsNullOrEmpty(header))
            throw new InvalidDataException("Invalid binary node: empty list or header");

        var attrs = new Dictionary<string, string>();
        BinaryNodeContent? content = null;

        int attrLen = (listSize - 1) >> 1;
        for (int i = 0; i < attrLen; i++)
        {
            string key = ReadString(ReadByte());
            string val = ReadString(ReadByte());
            attrs[key] = val;
        }

        if (listSize % 2 == 0)
        {
            int tag = ReadByte();
            if (IsListTag(tag))
            {
                content = new BinaryNodeList(ReadList(tag));
            }
            else
            {
                content = tag switch
                {
                    WaBinaryConstants.Tags.Binary8 => new BinaryNodeBytes(ReadBytes(ReadByte())),
                    WaBinaryConstants.Tags.Binary20 => new BinaryNodeBytes(ReadBytes(ReadInt20())),
                    WaBinaryConstants.Tags.Binary32 => new BinaryNodeBytes(ReadBytes(ReadInt(4))),
                    _ => new BinaryNodeString(ReadString(tag))
                };
            }
        }

        return new BinaryNode { Tag = header, Attrs = attrs, Content = content };
    }

    private static void CheckEos(byte[] buffer, IndexRef idx, int needed)
    {
        if (idx.Index + needed > buffer.Length)
            throw new EndOfStreamException("End of binary-node stream reached unexpectedly.");
    }
}
