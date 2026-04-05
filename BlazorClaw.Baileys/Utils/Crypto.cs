using System.Security.Cryptography;

namespace Baileys.Utils;

/// <summary>
/// Cryptographic helpers that mirror the TypeScript <c>Utils/crypto.ts</c> module.
/// All AES keys must be 32 bytes (AES-256); IVs must be 16 bytes for CBC/CTR and 12
/// bytes for GCM.
/// </summary>
public static class Crypto
{
    private const int GcmTagLengthBytes = 16;

    // ──────────────────────────────────────────────────────────
    //  AES-256-GCM
    // ──────────────────────────────────────────────────────────

    /// <summary>
    /// Encrypts <paramref name="plaintext"/> with AES-256-GCM and returns
    /// <c>ciphertext || tag</c> (tag appended).
    /// </summary>
    public static byte[] AesEncryptGcm(
        ReadOnlySpan<byte> plaintext,
        ReadOnlySpan<byte> key,
        ReadOnlySpan<byte> iv,
        ReadOnlySpan<byte> additionalData)
    {
        using var aesGcm = new AesGcm(key, GcmTagLengthBytes);
        var ciphertext = new byte[plaintext.Length];
        var tag = new byte[GcmTagLengthBytes];
        aesGcm.Encrypt(iv, plaintext, ciphertext, tag, additionalData);

        var result = new byte[ciphertext.Length + tag.Length];
        ciphertext.CopyTo(result, 0);
        tag.CopyTo(result, ciphertext.Length);
        return result;
    }

    /// <summary>
    /// Decrypts AES-256-GCM ciphertext where the 16-byte auth tag is appended
    /// at the end of <paramref name="ciphertextWithTag"/>.
    /// </summary>
    public static byte[] AesDecryptGcm(
        ReadOnlySpan<byte> ciphertextWithTag,
        ReadOnlySpan<byte> key,
        ReadOnlySpan<byte> iv,
        ReadOnlySpan<byte> additionalData)
    {
        if (ciphertextWithTag.Length < GcmTagLengthBytes)
            throw new ArgumentException("Ciphertext is too short to contain the GCM auth tag.", nameof(ciphertextWithTag));

        var ciphertext = ciphertextWithTag[..^GcmTagLengthBytes];
        var tag = ciphertextWithTag[^GcmTagLengthBytes..];

        using var aesGcm = new AesGcm(key, GcmTagLengthBytes);
        var plaintext = new byte[ciphertext.Length];
        aesGcm.Decrypt(iv, ciphertext, tag, plaintext, additionalData);
        return plaintext;
    }

    // ──────────────────────────────────────────────────────────
    //  AES-256-CTR
    // ──────────────────────────────────────────────────────────

    /// <summary>Encrypts <paramref name="plaintext"/> with AES-256-CTR.</summary>
    public static byte[] AesEncryptCtr(
        ReadOnlySpan<byte> plaintext,
        ReadOnlySpan<byte> key,
        ReadOnlySpan<byte> iv)
        => ApplyAesCtr(plaintext, key, iv);

    /// <summary>Decrypts <paramref name="ciphertext"/> with AES-256-CTR.</summary>
    public static byte[] AesDecryptCtr(
        ReadOnlySpan<byte> ciphertext,
        ReadOnlySpan<byte> key,
        ReadOnlySpan<byte> iv)
        => ApplyAesCtr(ciphertext, key, iv);

    // ──────────────────────────────────────────────────────────
    //  AES-256-CBC
    // ──────────────────────────────────────────────────────────

    /// <summary>
    /// Encrypts with AES-256-CBC and prepends a random 16-byte IV to the output
    /// (mirrors the TypeScript <c>aesEncrypt</c> helper).
    /// </summary>
    public static byte[] AesEncrypt(ReadOnlySpan<byte> plaintext, ReadOnlySpan<byte> key)
    {
        var iv = RandomNumberGenerator.GetBytes(16);
        var ciphertext = AesEncryptWithIv(plaintext, key, iv);
        var result = new byte[iv.Length + ciphertext.Length];
        iv.CopyTo(result, 0);
        ciphertext.CopyTo(result, iv.Length);
        return result;
    }

    /// <summary>
    /// Decrypts AES-256-CBC where the first 16 bytes of <paramref name="buffer"/>
    /// are the IV (mirrors the TypeScript <c>aesDecrypt</c> helper).
    /// </summary>
    public static byte[] AesDecrypt(ReadOnlySpan<byte> buffer, ReadOnlySpan<byte> key)
    {
        if (buffer.Length < 16)
            throw new ArgumentException("Buffer too short to contain a 16-byte IV.", nameof(buffer));

        return AesDecryptWithIv(buffer[16..], key, buffer[..16]);
    }

    /// <summary>Encrypts with AES-256-CBC using the supplied IV (no IV prefix).</summary>
    public static byte[] AesEncryptWithIv(
        ReadOnlySpan<byte> plaintext,
        ReadOnlySpan<byte> key,
        ReadOnlySpan<byte> iv)
    {
        using var aes = Aes.Create();
        aes.KeySize = 256;
        aes.Mode = CipherMode.CBC;
        aes.Padding = PaddingMode.PKCS7;
        aes.Key = key.ToArray();
        aes.IV = iv.ToArray();
        using var encryptor = aes.CreateEncryptor();
        return TransformFull(encryptor, plaintext.ToArray());
    }

    /// <summary>Decrypts with AES-256-CBC using the supplied IV.</summary>
    public static byte[] AesDecryptWithIv(
        ReadOnlySpan<byte> ciphertext,
        ReadOnlySpan<byte> key,
        ReadOnlySpan<byte> iv)
    {
        using var aes = Aes.Create();
        aes.KeySize = 256;
        aes.Mode = CipherMode.CBC;
        aes.Padding = PaddingMode.PKCS7;
        aes.Key = key.ToArray();
        aes.IV = iv.ToArray();
        using var decryptor = aes.CreateDecryptor();
        return TransformFull(decryptor, ciphertext.ToArray());
    }

    // ──────────────────────────────────────────────────────────
    //  HMAC-SHA-256 / SHA-512
    // ──────────────────────────────────────────────────────────

    /// <summary>Computes HMAC-SHA-256 over <paramref name="data"/> with <paramref name="key"/>.</summary>
    public static byte[] HmacSha256(ReadOnlySpan<byte> data, ReadOnlySpan<byte> key)
    {
        var result = new byte[HMACSHA256.HashSizeInBytes];
        HMACSHA256.TryHashData(key, data, result, out _);
        return result;
    }

    /// <summary>Computes HMAC-SHA-512 over <paramref name="data"/> with <paramref name="key"/>.</summary>
    public static byte[] HmacSha512(ReadOnlySpan<byte> data, ReadOnlySpan<byte> key)
    {
        var result = new byte[HMACSHA512.HashSizeInBytes];
        HMACSHA512.TryHashData(key, data, result, out _);
        return result;
    }

    // ──────────────────────────────────────────────────────────
    //  SHA-256 / MD5
    // ──────────────────────────────────────────────────────────

    /// <summary>Computes a SHA-256 hash of <paramref name="data"/>.</summary>
    public static byte[] Sha256(ReadOnlySpan<byte> data)
    {
        var result = new byte[SHA256.HashSizeInBytes];
        SHA256.TryHashData(data, result, out _);
        return result;
    }

    /// <summary>Computes an MD5 hash of <paramref name="data"/>.</summary>
    public static byte[] Md5(ReadOnlySpan<byte> data)
    {
        var result = new byte[MD5.HashSizeInBytes];
        MD5.TryHashData(data, result, out _);
        return result;
    }

    // ──────────────────────────────────────────────────────────
    //  HKDF (RFC 5869)
    // ──────────────────────────────────────────────────────────

    /// <summary>
    /// Derives key material using HKDF-SHA-256 (RFC 5869).
    /// </summary>
    /// <param name="inputKeyMaterial">The IKM (e.g. a shared secret).</param>
    /// <param name="outputLength">Number of output bytes.</param>
    /// <param name="salt">Optional salt; defaults to a zero-filled array when null.</param>
    /// <param name="info">Optional context / info bytes.</param>
    public static byte[] Hkdf(
        ReadOnlySpan<byte> inputKeyMaterial,
        int outputLength,
        ReadOnlySpan<byte> salt = default,
        ReadOnlySpan<byte> info = default)
    {
        var output = new byte[outputLength];
        HKDF.DeriveKey(HashAlgorithmName.SHA256, inputKeyMaterial, output, salt, info);
        return output;
    }

    // ──────────────────────────────────────────────────────────
    //  PBKDF2 (for pairing-code key derivation)
    // ──────────────────────────────────────────────────────────

    /// <summary>
    /// Derives a 32-byte key from <paramref name="pairingCode"/> and <paramref name="salt"/>
    /// using PBKDF2-SHA-256 with 131 072 iterations (mirrors <c>derivePairingCodeKey</c>).
    /// </summary>
    public static byte[] DerivePairingCodeKey(string pairingCode, ReadOnlySpan<byte> salt)
    {
        var passwordBytes = System.Text.Encoding.UTF8.GetBytes(pairingCode);
        return Rfc2898DeriveBytes.Pbkdf2(
            passwordBytes,
            salt.ToArray(),
            iterations: 2 << 16,      // 131 072
            hashAlgorithm: HashAlgorithmName.SHA256,
            outputLength: 32);
    }

    // ──────────────────────────────────────────────────────────
    //  Random helpers
    // ──────────────────────────────────────────────────────────

    /// <summary>Returns <paramref name="count"/> cryptographically secure random bytes.</summary>
    public static byte[] RandomBytes(int count)
        => RandomNumberGenerator.GetBytes(count);

    /// <summary>
    /// Generates a random 15-bit registration ID (0–16383), matching the
    /// TypeScript <c>generateRegistrationId</c> function.
    /// </summary>
    public static int GenerateRegistrationId()
        => (int)(BitConverter.ToUInt16(RandomBytes(2)) & 0x3FFF);

    // ──────────────────────────────────────────────────────────
    //  Internals
    // ──────────────────────────────────────────────────────────

    private static byte[] ApplyAesCtr(
        ReadOnlySpan<byte> input,
        ReadOnlySpan<byte> key,
        ReadOnlySpan<byte> iv)
    {
        // AES-CTR: encrypt each counter block with AES-ECB to produce key-stream bytes,
        // then XOR with the input.  A single Aes instance is created once for the entire
        // call and reused across all blocks to avoid the per-block allocation overhead.
        var output = new byte[input.Length];
        var counter = iv.ToArray();          // 16-byte counter block
        var ksBlock = new byte[16];

        using var aes = Aes.Create();
        aes.KeySize = 256;
        aes.Key = key.ToArray();

        for (int offset = 0; offset < input.Length; offset += 16)
        {
            // TryEncryptEcb encrypts a single block without padding in one call,
            // reusing the same cipher state.
            aes.TryEncryptEcb(counter, ksBlock, PaddingMode.None, out _);

            int blockLen = Math.Min(16, input.Length - offset);
            for (int i = 0; i < blockLen; i++)
                output[offset + i] = (byte)(input[offset + i] ^ ksBlock[i]);

            // Increment counter (big-endian) for the next block.
            IncrementCounter(counter);
        }

        return output;
    }

    private static void IncrementCounter(byte[] counter)
    {
        for (int i = counter.Length - 1; i >= 0; i--)
        {
            if (++counter[i] != 0) break;
        }
    }

    private static byte[] TransformFull(ICryptoTransform transform, byte[] data)
    {
        using var ms = new System.IO.MemoryStream();
        using var cs = new CryptoStream(ms, transform, CryptoStreamMode.Write);
        cs.Write(data, 0, data.Length);
        cs.FlushFinalBlock();
        return ms.ToArray();
    }
}
