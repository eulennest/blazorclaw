using System.Security.Cryptography;
using System.Text;

namespace BlazorClaw.WhatsApp.Crypto
{
    /// <summary>
    /// Cryptographic utilities for WhatsApp protocol
    /// - AES-GCM: System.Security.Cryptography
    /// - Curve25519: curve25519-dotnet
    /// </summary>
    public static class CryptoUtils
    {
        private const int NONCE_LENGTH = 12;       // 96-bit for GCM
        private const int TAG_LENGTH = 16;         // 128-bit auth tag
        private const int KEY_LENGTH = 32;         // 256-bit keys

        /// <summary>
        /// AES-256-GCM Encryption
        /// </summary>
        public static byte[] AesGcmEncrypt(byte[] plaintext, byte[] key, byte[] nonce)
        {
            if (key.Length != KEY_LENGTH)
                throw new ArgumentException($"Key must be {KEY_LENGTH} bytes", nameof(key));
            if (nonce.Length != NONCE_LENGTH)
                throw new ArgumentException($"Nonce must be {NONCE_LENGTH} bytes", nameof(nonce));

            using (var cipher = new AesGcm(key, TAG_LENGTH))
            {
                var ciphertext = new byte[plaintext.Length];
                var tag = new byte[TAG_LENGTH];

                cipher.Encrypt(nonce, plaintext, null, ciphertext, tag);

                // Return ciphertext + tag
                return ciphertext.Concat(tag).ToArray();
            }
        }

        /// <summary>
        /// AES-256-GCM Decryption
        /// </summary>
        public static byte[] AesGcmDecrypt(byte[] ciphertextWithTag, byte[] key, byte[] nonce)
        {
            if (key.Length != KEY_LENGTH)
                throw new ArgumentException($"Key must be {KEY_LENGTH} bytes", nameof(key));
            if (nonce.Length != NONCE_LENGTH)
                throw new ArgumentException($"Nonce must be {NONCE_LENGTH} bytes", nameof(nonce));
            if (ciphertextWithTag.Length < TAG_LENGTH)
                throw new ArgumentException("Ciphertext too short for tag", nameof(ciphertextWithTag));

            // Split ciphertext and tag
            var ciphertext = ciphertextWithTag.Take(ciphertextWithTag.Length - TAG_LENGTH).ToArray();
            var tag = ciphertextWithTag.Skip(ciphertextWithTag.Length - TAG_LENGTH).ToArray();

            using (var cipher = new AesGcm(key, TAG_LENGTH))
            {
                var plaintext = new byte[ciphertext.Length];
                cipher.Decrypt(nonce, ciphertext, null, tag, plaintext);
                return plaintext;
            }
        }

        /// <summary>
        /// HKDF-SHA256 (HMAC-based Key Derivation Function)
        /// Reference: RFC 5869
        /// </summary>
        public static byte[] HkdfSha256(byte[] ikm, byte[] salt, byte[] info, int length)
        {
            // 1. Extract
            using (var prk = new HMACSHA256(salt ?? new byte[32]))
            {
                var extractedKey = prk.ComputeHash(ikm);

                // 2. Expand
                using (var hmac = new HMACSHA256(extractedKey))
                {
                    var output = new byte[length];
                    var t = new byte[0];

                    for (int i = 0; i < (length + 31) / 32; i++)
                    {
                        var input = t.Concat(info).Concat(new byte[] { (byte)(i + 1) }).ToArray();
                        t = hmac.ComputeHash(input);

                        Array.Copy(t, 0, output, i * 32, Math.Min(32, length - i * 32));
                    }

                    return output;
                }
            }
        }

        /// <summary>
        /// HMAC-SHA256
        /// </summary>
        public static byte[] HmacSha256(byte[] key, byte[] data)
        {
            using (var hmac = new HMACSHA256(key))
            {
                return hmac.ComputeHash(data);
            }
        }

        /// <summary>
        /// SHA256 Hash
        /// </summary>
        public static byte[] Sha256(byte[] data)
        {
            using (var sha = SHA256.Create())
            {
                return sha.ComputeHash(data);
            }
        }

        /// <summary>
        /// Derive nonce from counter (for AES-GCM)
        /// </summary>
        public static byte[] DeriveNonce(uint counter)
        {
            var nonce = new byte[NONCE_LENGTH];
            
            // Write counter as big-endian 32-bit integer at offset 4
            var counterBytes = BitConverter.GetBytes(counter);
            if (BitConverter.IsLittleEndian)
                Array.Reverse(counterBytes);
            
            Array.Copy(counterBytes, 0, nonce, 0, 4);
            
            return nonce;
        }

        /// <summary>
        /// Generate random bytes
        /// </summary>
        public static byte[] RandomBytes(int length)
        {
            using (var rng = RandomNumberGenerator.Create())
            {
                var bytes = new byte[length];
                rng.GetBytes(bytes);
                return bytes;
            }
        }

        /// <summary>
        /// Generate Curve25519 keypair (via curve25519-dotnet)
        /// </summary>
        public static (byte[] publicKey, byte[] privateKey) GenerateCurve25519Keypair()
        {
            return Curve25519Utils.GenerateKeyPair();
        }

        /// <summary>
        /// Curve25519 ECDH shared secret (via curve25519-dotnet)
        /// </summary>
        public static byte[] Curve25519SharedSecret(byte[] privateKey, byte[] publicKey)
        {
            return Curve25519Utils.CalculateAgreement(privateKey, publicKey);
        }
    }
}
