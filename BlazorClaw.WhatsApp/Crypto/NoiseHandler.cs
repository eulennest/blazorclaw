using System.Security.Cryptography;

namespace BlazorClaw.WhatsApp.Crypto
{
    /// <summary>
    /// Implements WhatsApp Noise_XX_25519_AESGCM_SHA256 handshake protocol
    /// Ported from Baileys .NET (Darkace01/Baileys/dotnet) for .NET 8
    /// </summary>
    public sealed class NoiseHandler
    {
        private const int IvLength = 12;
        private const string NoiseMode = "Noise_XX_25519_AESGCM_SHA256\0\0\0\0";
        private static readonly byte[] NoiseWaHeader = new byte[] { 0x57, 0x41, 0x06, 0x03 }; // "WA" + version

        private byte[] _hash;
        private byte[] _salt;
        private byte[] _encKey;
        private byte[] _decKey;
        private int _writeCounter;
        private int _readCounter;
        private bool _transportEstablished;

        private readonly byte[] _privateKey;
        private readonly byte[] _publicKey;
        private readonly byte[] _introHeader;

        public NoiseHandler(byte[] privateKey, byte[] publicKey, byte[]? routingInfo = null)
        {
            _privateKey = privateKey;
            _publicKey = publicKey;

            // Initialize hash/salt/encKey/decKey from noise mode
            var modeBytes = System.Text.Encoding.ASCII.GetBytes(NoiseMode);
            _hash = modeBytes.Length == 32 ? modeBytes : SHA256.HashData(modeBytes);
            _salt = [.._hash];
            _encKey = [.._hash];
            _decKey = [.._hash];

            // Build intro header
            if (routingInfo is { Length: > 0 })
            {
                _introHeader = new byte[7 + routingInfo.Length + NoiseWaHeader.Length];
                _introHeader[0] = (byte)'E';
                _introHeader[1] = (byte)'D';
                _introHeader[2] = 0;
                _introHeader[3] = 1;
                _introHeader[4] = (byte)(routingInfo.Length >> 16);
                _introHeader[5] = (byte)((routingInfo.Length >> 8) & 0xFF);
                _introHeader[6] = (byte)(routingInfo.Length & 0xFF);
                routingInfo.CopyTo(_introHeader, 7);
                NoiseWaHeader.CopyTo(_introHeader, 7 + routingInfo.Length);
            }
            else
            {
                _introHeader = [.. NoiseWaHeader];
            }
        }

        public ReadOnlySpan<byte> IntroHeader => _introHeader;
        public byte[] PublicKey => _publicKey;

        /// <summary>
        /// Encrypt plaintext (handshake or transport mode)
        /// </summary>
        public byte[] Encrypt(ReadOnlySpan<byte> plaintext)
        {
            if (!_transportEstablished)
            {
                Authenticate(plaintext);
                var iv = GenerateIv(_writeCounter++);
                var ciphertext = CryptoUtils.AesGcmEncrypt(plaintext.ToArray(), _encKey, iv);
                Authenticate(ciphertext);
                return ciphertext;
            }
            else
            {
                return EncryptTransport(plaintext);
            }
        }

        /// <summary>
        /// Decrypt ciphertext (handshake or transport mode)
        /// </summary>
        public byte[] Decrypt(ReadOnlySpan<byte> ciphertext)
        {
            if (!_transportEstablished)
            {
                Authenticate(ciphertext);
                var iv = GenerateIv(_readCounter++);
                var plaintext = CryptoUtils.AesGcmDecrypt(ciphertext.ToArray(), _decKey, iv);
                Authenticate(plaintext);
                return plaintext;
            }
            else
            {
                return DecryptTransport(ciphertext);
            }
        }

        /// <summary>
        /// Finalize handshake and switch to transport mode
        /// </summary>
        public void Finish()
        {
            var expanded = CryptoUtils.HkdfSha256(Array.Empty<byte>(), 64, _salt, Array.Empty<byte>());
            _encKey = expanded[..32];
            _decKey = expanded[32..];
            _writeCounter = 0;
            _readCounter = 0;
            _transportEstablished = true;
        }

        /// <summary>
        /// Mix data into handshake hash
        /// </summary>
        public void MixHash(ReadOnlySpan<byte> data) => Authenticate(data);

        /// <summary>
        /// Mix data into key chain (HKDF)
        /// </summary>
        public void MixIntoKey(ReadOnlySpan<byte> data)
        {
            var expanded = CryptoUtils.HkdfSha256(data.ToArray(), 64, _salt, Array.Empty<byte>());
            _salt = expanded[..32];
            _encKey = expanded[32..];
            _decKey = expanded[32..];
        }

        private void Authenticate(ReadOnlySpan<byte> data)
        {
            if (_transportEstablished) return;
            var combined = new byte[_hash.Length + data.Length];
            _hash.CopyTo(combined, 0);
            data.CopyTo(combined.AsSpan(_hash.Length));
            _hash = SHA256.HashData(combined);
        }

        private byte[] EncryptTransport(ReadOnlySpan<byte> plaintext)
        {
            var iv = BuildTransportIv(_writeCounter++);
            return CryptoUtils.AesGcmEncrypt(plaintext.ToArray(), _encKey, iv);
        }

        private byte[] DecryptTransport(ReadOnlySpan<byte> ciphertext)
        {
            var iv = BuildTransportIv(_readCounter++);
            return CryptoUtils.AesGcmDecrypt(ciphertext.ToArray(), _decKey, iv);
        }

        private static byte[] BuildTransportIv(int counter)
        {
            var iv = new byte[IvLength];
            iv[8] = (byte)((uint)counter >> 24);
            iv[9] = (byte)((uint)counter >> 16);
            iv[10] = (byte)((uint)counter >> 8);
            iv[11] = (byte)counter;
            return iv;
        }

        private static byte[] GenerateIv(int counter)
        {
            var iv = new byte[IvLength];
            System.Buffers.Binary.BinaryPrimitives.WriteInt32BigEndian(iv.AsSpan(8), counter);
            return iv;
        }
    }
}
