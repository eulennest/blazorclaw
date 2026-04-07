using System.Text;

namespace Baileys.Utils;

/// <summary>
/// Generic utility helpers that mirror the TypeScript <c>Utils/generics.ts</c> module.
/// </summary>
public static class Generics
{
    // ──────────────────────────────────────────────────────────
    //  Big-endian encoding
    // ──────────────────────────────────────────────────────────

    /// <summary>
    /// Encodes <paramref name="value"/> as a big-endian byte array of
    /// <paramref name="byteCount"/> bytes.
    /// </summary>
    public static byte[] EncodeBigEndian(int value, int byteCount = 4)
    {
        var result = new byte[byteCount];
        for (int i = byteCount - 1; i >= 0; i--)
        {
            result[i] = (byte)(value & 0xFF);
            value >>= 8;
        }
        return result;
    }

    /// <summary>
    /// Encodes <paramref name="value"/> as a big-endian byte array of
    /// <paramref name="byteCount"/> bytes.
    /// </summary>
    public static byte[] EncodeBigEndian(long value, int byteCount = 8)
    {
        var result = new byte[byteCount];
        for (int i = byteCount - 1; i >= 0; i--)
        {
            result[i] = (byte)(value & 0xFF);
            value >>= 8;
        }
        return result;
    }
    /// <summary>
    /// Decodes a big-endian byte array into a long integer.
    /// Mirrors the logic of EncodeBigEndian by processing bytes from left to right.
    /// </summary>
    /// <param name="data">The big-endian byte array to decode.</param>
    /// <returns>The resulting long integer value.</returns>
    public static long DecodeBigEndian(byte[] data)
    {
        long result = 0;
        for (int i = 0; i < data.Length; i++)
        {
            result <<= 8;
            result |= (data[i] & 0xFFL);
        }
        return result;
    }
    // ──────────────────────────────────────────────────────────
    //  Padding helpers (used by the noise protocol)
    // ──────────────────────────────────────────────────────────

    /// <summary>
    /// Appends between 1 and 16 bytes of PKCS#7-style padding to
    /// <paramref name="message"/> (pad byte value == pad length).
    /// </summary>
    public static byte[] WriteRandomPadMax16(ReadOnlySpan<byte> message)
    {
        var padByte = Crypto.RandomBytes(1)[0];
        var padLength = (padByte & 0x0F) + 1;
        var result = new byte[message.Length + padLength];
        message.CopyTo(result);
        for (int i = message.Length; i < result.Length; i++)
            result[i] = (byte)padLength;
        return result;
    }

    /// <summary>
    /// Removes the PKCS#7-style padding written by <see cref="WriteRandomPadMax16"/>.
    /// All padding bytes are validated to equal the pad length.
    /// </summary>
    /// <exception cref="InvalidOperationException">When the padding is invalid.</exception>
    public static byte[] UnpadRandomMax16(ReadOnlySpan<byte> padded)
    {
        if (padded.IsEmpty)
            throw new InvalidOperationException("unpadPkcs7 given empty bytes");

        int pad = padded[^1];
        if (pad == 0 || pad > 16 || pad > padded.Length)
            throw new InvalidOperationException($"unpad given {padded.Length} bytes, but pad is {pad}");

        // Validate that every padding byte equals the pad value (full PKCS#7 check).
        for (int i = padded.Length - pad; i < padded.Length; i++)
        {
            if (padded[i] != pad)
                throw new InvalidOperationException(
                    $"Invalid PKCS#7 padding: byte at position {i} is 0x{padded[i]:X2}, expected 0x{pad:X2}");
        }

        return padded[..^pad].ToArray();
    }

    // ──────────────────────────────────────────────────────────
    //  Message-ID generation
    // ──────────────────────────────────────────────────────────

    /// <summary>
    /// Generates a random WhatsApp message ID (hex, "3EB0" prefix, 18 random bytes).
    /// </summary>
    public static string GenerateMessageId()
        => "3EB0" + Convert.ToHexString(Crypto.RandomBytes(18));

    /// <summary>
    /// Generates a deterministic-prefix WhatsApp message ID based on the current
    /// unix timestamp and an optional user JID (mirrors <c>generateMessageIDV2</c>).
    /// </summary>
    public static string GenerateMessageIdV2(string? userId = null)
    {
        var data = new byte[8 + 20 + 16];
        var ts = (ulong)DateTimeOffset.UtcNow.ToUnixTimeSeconds();
        var tsBytes = BitConverter.GetBytes(ts);
        if (BitConverter.IsLittleEndian) Array.Reverse(tsBytes);
        tsBytes.CopyTo(data, 0);

        if (userId is not null)
        {
            var decoded = JidUtils.JidDecode(userId);
            if (decoded?.User is { Length: > 0 } user)
            {
                var userBytes = Encoding.ASCII.GetBytes(user);
                userBytes.CopyTo(data, 8);
                var suffix = Encoding.ASCII.GetBytes("@c.us");
                suffix.CopyTo(data, 8 + userBytes.Length);
            }
        }

        Crypto.RandomBytes(16).CopyTo(data, 28);

        var hash = Crypto.Sha256(data);
        return "3EB0" + Convert.ToHexString(hash)[..18];
    }

    /// <summary>
    /// Generates a random multi-device tag prefix (e.g. "12345.67890-").
    /// </summary>
    public static string GenerateMdTagPrefix()
    {
        var bytes = Crypto.RandomBytes(4);
        var a = BitConverter.ToUInt16(bytes, 0);
        var b = BitConverter.ToUInt16(bytes, 2);
        return $"{a}.{b}-";
    }

    // ──────────────────────────────────────────────────────────
    //  Unix timestamp
    // ──────────────────────────────────────────────────────────

    /// <summary>Returns the current time as a Unix timestamp in seconds.</summary>
    public static long UnixTimestampSeconds(DateTimeOffset? dt = null)
        => (dt ?? DateTimeOffset.UtcNow).ToUnixTimeSeconds();

    // ──────────────────────────────────────────────────────────
    //  Crockford Base-32 encoding
    // ──────────────────────────────────────────────────────────

    private const string CrockfordChars = "123456789ABCDEFGHJKLMNPQRSTVWXYZ";

    /// <summary>
    /// Encodes <paramref name="buffer"/> as a Crockford Base-32 string
    /// (mirrors the TypeScript <c>bytesToCrockford</c> helper).
    /// </summary>
    public static string BytesToCrockford(ReadOnlySpan<byte> buffer)
    {
        int value = 0, bitCount = 0;
        var sb = new StringBuilder();
        foreach (var b in buffer)
        {
            value = (value << 8) | (b & 0xFF);
            bitCount += 8;
            while (bitCount >= 5)
            {
                sb.Append(CrockfordChars[(value >> (bitCount - 5)) & 31]);
                bitCount -= 5;
            }
        }
        if (bitCount > 0)
            sb.Append(CrockfordChars[(value << (5 - bitCount)) & 31]);
        return sb.ToString();
    }

    // ──────────────────────────────────────────────────────────
    //  Participant-hash V2
    // ──────────────────────────────────────────────────────────

    /// <summary>
    /// Generates a participant-list hash for group messages
    /// (mirrors the TypeScript <c>generateParticipantHashV2</c>).
    /// </summary>
    public static string GenerateParticipantHashV2(IEnumerable<string> participants)
    {
        var sorted = participants.OrderBy(p => p, StringComparer.Ordinal).ToList();
        var joined = Encoding.UTF8.GetBytes(string.Concat(sorted));
        var hash = Convert.ToBase64String(Crypto.Sha256(joined));
        return "2:" + hash[..6];
    }

    // ──────────────────────────────────────────────────────────
    //  String helpers
    // ──────────────────────────────────────────────────────────

    /// <summary>Returns <c>true</c> when <paramref name="value"/> is null or empty.</summary>
    public static bool IsNullOrEmpty([System.Diagnostics.CodeAnalysis.NotNullWhen(false)] string? value)
        => string.IsNullOrEmpty(value);

    /// <summary>
    /// Determines whether the given platform string indicates a WhatsApp Business account.
    /// </summary>
    public static bool IsWaBusinessPlatform(string platform)
        => platform is "smbi" or "smba";

}
