using System.Text;

namespace Baileys.Utils;

/// <summary>
/// Generates and renders WhatsApp QR codes in the terminal so the user can
/// scan them with the WhatsApp mobile app during the connection pairing flow.
/// </summary>
/// <remarks>
/// Implements QR code generation from scratch (ISO/IEC 18004) using byte-mode
/// encoding with L error correction, supporting versions 1–10 (up to ~271
/// UTF-8 bytes), which covers all WhatsApp QR payloads.
/// </remarks>
public static class QrUtils
{
    // ─────────────────────────────────────────────────────────────────────────
    //  Public API
    // ─────────────────────────────────────────────────────────────────────────

    /// <summary>
    /// Logs a "scan QR code" message at <c>info</c> level and writes the QR
    /// code as ASCII art to <see cref="Console.Out"/> so the user can scan it
    /// with the WhatsApp mobile app.
    /// </summary>
    /// <param name="qrData">
    /// Raw QR data string from the <c>connection.update</c> event's
    /// <c>Qr</c> property.
    /// </param>
    /// <param name="logger">Logger to write the informational message to.</param>
    public static void LogQr(string qrData, ILogger logger)
    {
        logger.Info("Scan the QR code below with WhatsApp on your phone to connect:");
        PrintToConsole(qrData);
    }

    /// <summary>
    /// Generates the QR code for <paramref name="qrData"/> and writes it as
    /// ASCII art (using Unicode block characters) to <see cref="Console.Out"/>.
    /// </summary>
    public static void PrintToConsole(string qrData)
    {
        var matrix = Generate(qrData);
        Console.Write(RenderToAscii(matrix));
    }

    /// <summary>
    /// Generates a QR code matrix for <paramref name="text"/> using byte-mode
    /// encoding and L error correction (versions 1–10, up to ~271 bytes).
    /// </summary>
    /// <returns>
    /// A square boolean array indexed as <c>[row, col]</c>.
    /// <see langword="true"/> represents a dark module;
    /// <see langword="false"/> represents a light module.
    /// </returns>
    /// <exception cref="ArgumentException">
    /// Thrown when <paramref name="text"/> exceeds the maximum supported
    /// capacity of 271 UTF-8 bytes (version 10, L error correction).
    /// </exception>
    public static bool[,] Generate(string text)
    {
        var data    = Encoding.UTF8.GetBytes(text);
        int version = ChooseVersion(data.Length);
        if (version < 0)
            throw new ArgumentException(
                $"QR data is too long ({data.Length} bytes); the maximum supported " +
                "is 271 UTF-8 bytes (version 10, L error correction).",
                nameof(text));

        int size     = QrSize(version);
        var matrix   = new bool[size, size];
        var reserved = new bool[size, size];

        PlaceFinderPatterns(matrix, reserved, size);
        PlaceTimingPatterns(matrix, reserved, size);
        PlaceAlignmentPatterns(matrix, reserved, version, size);
        PlaceDarkModule(matrix, reserved, size);
        ReserveFormatInfoArea(reserved, size);

        var codewords = BuildCodewords(data, version);
        PlaceDataBits(matrix, reserved, codewords, size);

        int mask = ChooseBestMask(matrix, reserved, size);
        ApplyMask(matrix, reserved, mask, size);
        WriteFormatInfo(matrix, mask, size);

        return matrix;
    }

    /// <summary>
    /// Renders a QR code matrix as an ASCII-art string using Unicode block
    /// characters: <c>██</c> for dark modules and two spaces for light modules.
    /// A 4-module quiet zone is added on all sides as required by the spec.
    /// </summary>
    public static string RenderToAscii(bool[,] matrix)
    {
        int size      = matrix.GetLength(0);
        const int border = 4;
        int lineWidth = (size + border * 2) * 2;
        var blank     = new string(' ', lineWidth);
        var sb        = new StringBuilder();

        for (int i = 0; i < border; i++) sb.AppendLine(blank);

        for (int r = 0; r < size; r++)
        {
            sb.Append(' ', border * 2);
            for (int c = 0; c < size; c++)
                sb.Append(matrix[r, c] ? "██" : "  ");
            sb.Append(' ', border * 2);
            sb.AppendLine();
        }

        for (int i = 0; i < border; i++) sb.AppendLine(blank);

        return sb.ToString();
    }

    // ─────────────────────────────────────────────────────────────────────────
    //  Version selection
    // ─────────────────────────────────────────────────────────────────────────

    // Maximum byte-mode data capacity for L error correction, versions 1–10.
    private static readonly int[] ByteCapacityL = [17, 32, 53, 78, 106, 134, 154, 192, 230, 271];

    private static int ChooseVersion(int byteCount)
    {
        for (int i = 0; i < ByteCapacityL.Length; i++)
            if (byteCount <= ByteCapacityL[i])
                return i + 1;
        return -1;
    }

    private static int QrSize(int version) => 4 * version + 17;

    // ─────────────────────────────────────────────────────────────────────────
    //  Codeword generation
    // ─────────────────────────────────────────────────────────────────────────

    // EC parameters for each version 1–10 with L error correction:
    // (ecPerBlock, numBlocks1, dataCwPerBlock1, numBlocks2, dataCwPerBlock2)
    private static readonly (int ec, int nb1, int dc1, int nb2, int dc2)[] EcParamsL =
    [
        (7,  1,  19, 0,  0),   // V1
        (10, 1,  34, 0,  0),   // V2
        (15, 1,  55, 0,  0),   // V3
        (20, 1,  80, 0,  0),   // V4
        (26, 1, 108, 0,  0),   // V5
        (18, 2,  68, 0,  0),   // V6
        (20, 2,  78, 0,  0),   // V7
        (24, 2,  97, 0,  0),   // V8
        (30, 2, 116, 0,  0),   // V9
        (18, 2,  68, 2, 69),   // V10
    ];

    private static byte[] BuildCodewords(byte[] data, int version)
    {
        var (ec, nb1, dc1, nb2, dc2) = EcParamsL[version - 1];
        int totalDataCw = nb1 * dc1 + nb2 * dc2;
        int maxBits     = totalDataCw * 8;

        // ── Encode data into a flat bit array ────────────────────────────────
        var bits = new List<int>(maxBits);

        // Byte-mode indicator: 0b0100
        bits.AddRange([0, 1, 0, 0]);

        // Character count indicator: 8 bits for versions 1–9, 16 bits for 10+
        int ccBits = version <= 9 ? 8 : 16;
        for (int i = ccBits - 1; i >= 0; i--)
            bits.Add((data.Length >> i) & 1);

        // Data bytes (MSB first)
        foreach (byte b in data)
            for (int i = 7; i >= 0; i--)
                bits.Add((b >> i) & 1);

        // Terminator: up to 4 zero bits
        for (int i = 0; i < 4 && bits.Count < maxBits; i++)
            bits.Add(0);

        // Pad to byte boundary
        while (bits.Count % 8 != 0) bits.Add(0);

        // Pad codewords with alternating 0xEC / 0x11
        for (int pad = 0; bits.Count < maxBits; pad++)
        {
            byte padByte = (pad & 1) == 0 ? (byte)0xEC : (byte)0x11;
            for (int i = 7; i >= 0; i--)
                bits.Add((padByte >> i) & 1);
        }

        // ── Convert to codeword bytes ────────────────────────────────────────
        var dataCw = new byte[totalDataCw];
        for (int i = 0; i < totalDataCw; i++)
        {
            int v = 0;
            for (int j = 0; j < 8; j++) v = (v << 1) | bits[i * 8 + j];
            dataCw[i] = (byte)v;
        }

        // ── Split into blocks and compute EC codewords ───────────────────────
        int offset = 0;
        var blocks = new List<(byte[] data, byte[] ecCw)>(nb1 + nb2);

        for (int b = 0; b < nb1; b++)
        {
            var blockData = dataCw[offset..(offset + dc1)];
            blocks.Add((blockData, ReedSolomon(blockData, ec)));
            offset += dc1;
        }
        for (int b = 0; b < nb2; b++)
        {
            var blockData = dataCw[offset..(offset + dc2)];
            blocks.Add((blockData, ReedSolomon(blockData, ec)));
            offset += dc2;
        }

        // ── Interleave data then EC codewords ────────────────────────────────
        int maxDc  = dc2 > dc1 ? dc2 : dc1;
        var result = new List<byte>(totalDataCw + (nb1 + nb2) * ec);

        for (int i = 0; i < maxDc; i++)
            foreach (var (d, _) in blocks)
                if (i < d.Length) result.Add(d[i]);

        for (int i = 0; i < ec; i++)
            foreach (var (_, e) in blocks)
                result.Add(e[i]);

        return [.. result];
    }

    // ─────────────────────────────────────────────────────────────────────────
    //  GF(256) arithmetic (primitive polynomial 0x11D)
    // ─────────────────────────────────────────────────────────────────────────

    private static readonly byte[] GfExp = new byte[512];
    private static readonly byte[] GfLog = new byte[256];

    static QrUtils()
    {
        int x = 1;
        for (int i = 0; i < 255; i++)
        {
            GfExp[i] = (byte)x;
            GfLog[x] = (byte)i;
            x <<= 1;
            if (x >= 256) x ^= 0x11D; // reduce modulo x^8+x^4+x^3+x^2+1
        }
        // Extend to avoid modulo in GfMul (max index = 254+254 = 508 < 512).
        for (int i = 255; i < 512; i++)
            GfExp[i] = GfExp[i - 255];
    }

    private static byte GfMul(byte a, byte b)
        => (a == 0 || b == 0) ? (byte)0 : GfExp[GfLog[a] + GfLog[b]];

    // ─────────────────────────────────────────────────────────────────────────
    //  Reed-Solomon error correction
    // ─────────────────────────────────────────────────────────────────────────

    private static byte[] ReedSolomon(byte[] data, int ecCount)
    {
        // Build generator polynomial g(x) = ∏(x + α^i) for i = 0..ecCount-1.
        // PolyMul stores coefficients in ascending degree order.
        var gen = new byte[] { 1 };
        for (int i = 0; i < ecCount; i++)
            gen = PolyMul(gen, [GfExp[i], 1]);

        // The LFSR division algorithm needs the generator in descending degree
        // order (MSB-first), excluding the monic leading coefficient gen[ecCount].
        var divisor = gen[0..ecCount];
        Array.Reverse(divisor);

        // Polynomial long division — returns the ecCount-byte remainder.
        var rem = new byte[ecCount];
        foreach (byte b in data)
        {
            byte coef = (byte)(b ^ rem[0]);
            Array.Copy(rem, 1, rem, 0, ecCount - 1);
            rem[ecCount - 1] = 0;
            for (int j = 0; j < ecCount; j++)
                rem[j] ^= GfMul(coef, divisor[j]);
        }
        return rem;
    }

    private static byte[] PolyMul(byte[] a, byte[] b)
    {
        var result = new byte[a.Length + b.Length - 1];
        for (int i = 0; i < a.Length; i++)
            for (int j = 0; j < b.Length; j++)
                result[i + j] ^= GfMul(a[i], b[j]);
        return result;
    }

    // ─────────────────────────────────────────────────────────────────────────
    //  QR matrix construction
    // ─────────────────────────────────────────────────────────────────────────

    private static void PlaceFinderPatterns(bool[,] m, bool[,] res, int size)
    {
        // Place one 7×7 finder pattern plus its 1-module-wide light separator.
        void PlaceFinder(int row, int col)
        {
            for (int dr = -1; dr <= 7; dr++)
            for (int dc = -1; dc <= 7; dc++)
            {
                int r = row + dr, c = col + dc;
                if ((uint)r >= (uint)size || (uint)c >= (uint)size) continue;
                res[r, c] = true;
                if ((uint)dr <= 6u && (uint)dc <= 6u)
                    // Outer ring (dr/dc == 0 or 6) or centre 3×3 are dark.
                    m[r, c] = dr == 0 || dr == 6 || dc == 0 || dc == 6
                           || (dr >= 2 && dr <= 4 && dc >= 2 && dc <= 4);
                else
                    m[r, c] = false; // separator is always light
            }
        }

        PlaceFinder(0,        0);         // top-left
        PlaceFinder(0,        size - 7);  // top-right
        PlaceFinder(size - 7, 0);         // bottom-left
    }

    private static void PlaceTimingPatterns(bool[,] m, bool[,] res, int size)
    {
        for (int i = 8; i < size - 8; i++)
        {
            bool dark = (i & 1) == 0;
            m[6, i] = m[i, 6] = dark;
            res[6, i] = res[i, 6] = true;
        }
    }

    private static void PlaceAlignmentPatterns(bool[,] m, bool[,] res, int version, int size)
    {
        if (version < 2) return;

        int[] pos = AlignPos[version - 1];
        foreach (int ar in pos)
        foreach (int ac in pos)
        {
            // Skip positions whose centre would overlap a finder pattern area.
            if (ar <= 8 && ac <= 8)        continue; // top-left
            if (ar <= 8 && ac >= size - 9) continue; // top-right
            if (ar >= size - 9 && ac <= 8) continue; // bottom-left

            // Place 5×5 alignment pattern centred at (ar, ac).
            for (int dr = -2; dr <= 2; dr++)
            for (int dc = -2; dc <= 2; dc++)
            {
                bool dark = dr == -2 || dr == 2 || dc == -2 || dc == 2
                         || (dr == 0 && dc == 0);
                m[ar + dr, ac + dc]   = dark;
                res[ar + dr, ac + dc] = true;
            }
        }
    }

    private static void PlaceDarkModule(bool[,] m, bool[,] res, int size)
    {
        m[size - 8, 8]   = true;
        res[size - 8, 8] = true;
    }

    private static void ReserveFormatInfoArea(bool[,] res, int size)
    {
        // Near top-left finder: row 8 cols 0–8, and col 8 rows 0–8
        for (int i = 0; i <= 8; i++) { res[8, i] = true; res[i, 8] = true; }
        // Near top-right finder: row 8, cols size-8..size-1
        for (int i = size - 8; i < size; i++) res[8, i] = true;
        // Near bottom-left finder: col 8, rows size-8..size-1
        for (int i = size - 8; i < size; i++) res[i, 8] = true;
    }

    // ─────────────────────────────────────────────────────────────────────────
    //  Data bit placement (zigzag traversal)
    // ─────────────────────────────────────────────────────────────────────────

    private static void PlaceDataBits(bool[,] m, bool[,] res, byte[] codewords, int size)
    {
        int totalBits = codewords.Length * 8;
        var bits      = new bool[totalBits];
        for (int i = 0; i < codewords.Length; i++)
            for (int j = 0; j < 8; j++)
                bits[i * 8 + j] = ((codewords[i] >> (7 - j)) & 1) == 1;

        int  bitIdx = 0;
        bool upward = true;

        // Traverse two columns at a time from right to left.
        for (int right = size - 1; right >= 1; right -= 2)
        {
            if (right == 6) right--; // skip the vertical timing-pattern column

            for (int vert = 0; vert < size; vert++)
            {
                int row = upward ? size - 1 - vert : vert;
                for (int dx = 0; dx <= 1; dx++)
                {
                    int col = right - dx;
                    if (!res[row, col])
                        m[row, col] = bitIdx < totalBits && bits[bitIdx++];
                }
            }
            upward = !upward;
        }
    }

    // ─────────────────────────────────────────────────────────────────────────
    //  Masking
    // ─────────────────────────────────────────────────────────────────────────

    private static int ChooseBestMask(bool[,] m, bool[,] res, int size)
    {
        int  bestMask  = 0;
        int  bestScore = int.MaxValue;
        var  temp      = new bool[size, size];

        for (int mask = 0; mask < 8; mask++)
        {
            Array.Copy(m, temp, m.Length);
            for (int r = 0; r < size; r++)
            for (int c = 0; c < size; c++)
                if (!res[r, c] && MaskCondition(mask, r, c))
                    temp[r, c] = !temp[r, c];

            int score = PenaltyScore(temp, size);
            if (score < bestScore) { bestScore = score; bestMask = mask; }
        }
        return bestMask;
    }

    private static void ApplyMask(bool[,] m, bool[,] res, int mask, int size)
    {
        for (int r = 0; r < size; r++)
        for (int c = 0; c < size; c++)
            if (!res[r, c] && MaskCondition(mask, r, c))
                m[r, c] = !m[r, c];
    }

    private static bool MaskCondition(int mask, int r, int c) => mask switch
    {
        0 => (r + c) % 2 == 0,
        1 => r % 2 == 0,
        2 => c % 3 == 0,
        3 => (r + c) % 3 == 0,
        4 => (r / 2 + c / 3) % 2 == 0,
        5 => (r * c) % 2 + (r * c) % 3 == 0,
        6 => ((r * c) % 2 + (r * c) % 3) % 2 == 0,
        7 => ((r + c) % 2 + (r * c) % 3) % 2 == 0,
        _ => false
    };

    private static int PenaltyScore(bool[,] m, int size)
    {
        int score = 0;

        // Rule 1: 5+ consecutive same-colour modules per row/column.
        for (int i = 0; i < size; i++)
        {
            score += RunPenalty(i, size, isCol: false, m);
            score += RunPenalty(i, size, isCol: true,  m);
        }

        // Rule 2: 2×2 blocks of same colour (+3 each).
        for (int r = 0; r < size - 1; r++)
        for (int c = 0; c < size - 1; c++)
            if (m[r, c] == m[r, c + 1] && m[r, c] == m[r + 1, c] && m[r, c] == m[r + 1, c + 1])
                score += 3;

        // Rule 3: 1:1:3:1:1 finder-like patterns (+40 each).
        bool[] fwdPat = [true, false, true, true, true, false, true, false, false, false, false];
        bool[] revPat = [false, false, false, false, true, false, true, true, true, false, true];
        for (int i = 0; i < size; i++)
        {
            score += FinderPatternCount(i, size, isCol: false, m, fwdPat) * 40;
            score += FinderPatternCount(i, size, isCol: false, m, revPat) * 40;
            score += FinderPatternCount(i, size, isCol: true,  m, fwdPat) * 40;
            score += FinderPatternCount(i, size, isCol: true,  m, revPat) * 40;
        }

        // Rule 4: dark-module proportion penalty.
        int dark = 0;
        for (int r = 0; r < size; r++)
        for (int c = 0; c < size; c++)
            if (m[r, c]) dark++;
        int k = Math.Abs(dark * 20 / (size * size) - 10);
        score += k * 10;

        return score;
    }

    private static int RunPenalty(int idx, int size, bool isCol, bool[,] m)
    {
        int  score = 0, run = 1;
        bool last  = isCol ? m[0, idx] : m[idx, 0];
        for (int i = 1; i < size; i++)
        {
            bool cur = isCol ? m[i, idx] : m[idx, i];
            if (cur == last)
            {
                run++;
                if (run == 5) score += 3;
                else if (run > 5) score++;
            }
            else { run = 1; last = cur; }
        }
        return score;
    }

    private static int FinderPatternCount(int idx, int size, bool isCol, bool[,] m, bool[] pat)
    {
        int count = 0;
        for (int start = 0; start <= size - pat.Length; start++)
        {
            bool match = true;
            for (int k = 0; k < pat.Length && match; k++)
            {
                bool val = isCol ? m[start + k, idx] : m[idx, start + k];
                if (val != pat[k]) match = false;
            }
            if (match) count++;
        }
        return count;
    }

    // ─────────────────────────────────────────────────────────────────────────
    //  Format information (BCH-protected, L error correction)
    // ─────────────────────────────────────────────────────────────────────────

    private static void WriteFormatInfo(bool[,] m, int mask, int size)
    {
        // EC level L = 0b01; combined with mask pattern (3 bits) → 5-bit data word.
        int fmt = ComputeFormatBits((0b01 << 3) | mask);

        bool Bit(int i) => ((fmt >> i) & 1) == 1;

        // First copy near the top-left finder pattern:
        for (int i = 0; i <= 5; i++) m[8, i] = Bit(i); // bits 0–5 → (8, 0–5)
        m[8, 7] = Bit(6);                                // bit  6 → (8,7), timing at (8,6) skipped
        m[8, 8] = Bit(7);                                // bit  7 → (8,8)
        m[7, 8] = Bit(8);                                // bit  8 → (7,8)
        for (int i = 9; i <= 14; i++) m[14 - i, 8] = Bit(i); // bits 9–14 → rows 5..0, col 8

        // Second copy near the top-right and bottom-left finder patterns:
        for (int i = 0;  i <= 7;  i++) m[size - 1 - i, 8] = Bit(i);  // bits 0–7  → col 8
        for (int i = 8;  i <= 14; i++) m[8, size - 15 + i] = Bit(i);  // bits 8–14 → row 8
        m[size - 8, 8] = true; // dark module is always dark (restores bit 7's overwrite)
    }

    private static int ComputeFormatBits(int data)
    {
        // BCH(15,5) error correction; generator polynomial x^10+x^8+x^5+x^4+x^2+x+1 = 0x537.
        int rem = data << 10;
        for (int i = 4; i >= 0; i--)
            if (((rem >> (i + 10)) & 1) != 0)
                rem ^= 0x537 << i;
        return ((data << 10) | (rem & 0x3FF)) ^ 0x5412; // XOR with 101010000010010
    }

    // ─────────────────────────────────────────────────────────────────────────
    //  Alignment pattern centre positions (versions 1–10, from ISO/IEC 18004)
    // ─────────────────────────────────────────────────────────────────────────

    private static readonly int[][] AlignPos =
    [
        [],              // V1:  no alignment patterns
        [6, 18],         // V2
        [6, 22],         // V3
        [6, 26],         // V4
        [6, 30],         // V5
        [6, 34],         // V6
        [6, 22, 38],     // V7
        [6, 24, 42],     // V8
        [6, 26, 46],     // V9
        [6, 28, 50],     // V10
    ];
}
