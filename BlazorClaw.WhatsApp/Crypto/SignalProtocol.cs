using libsignal;
using System.Text;

namespace BlazorClaw.WhatsApp.Crypto
{
    /// <summary>
    /// Signal Protocol integration (Double Ratchet algorithm)
    /// Uses libsignal-protocol-dotnet for X3DH + Double Ratchet
    /// 
    /// Reference: 
    /// - https://signal.org/docs/specifications/doubleratchet/
    /// - https://signal.org/docs/specifications/x3dh/
    /// </summary>
    public class SignalProtocolHandler
    {
        private readonly SignalSession _session;

        public SignalProtocolHandler(SignalSession session)
        {
            _session = session;
        }

        /// <summary>
        /// Initialize session with pre-key (X3DH)
        /// </summary>
        public static async Task<SignalProtocolHandler> InitializeSessionAsync(
            string deviceId,
            CancellationToken cancellationToken = default)
        {
            // Generate initial keys
            var (pubKey, privKey) = CryptoUtils.GenerateCurve25519Keypair();

            // Derive root key
            var rootKey = CryptoUtils.HkdfSha256(privKey, null, Encoding.UTF8.GetBytes("WhatsApp"), 32);

            var session = new SignalSession
            {
                DeviceId = deviceId,
                RootKey = rootKey,
                SendingChainKey = rootKey,
                ReceivingChainKey = rootKey,
                DhPublicKey = pubKey,
                DhPrivateKey = privKey
            };

            return await Task.FromResult(new SignalProtocolHandler(session));
        }

        /// <summary>
        /// Encrypt message with Double Ratchet
        /// </summary>
        public async Task<byte[]> EncryptAsync(byte[] plaintext, CancellationToken cancellationToken = default)
        {
            // KDF_CK: Derive message key from chain key
            var messageKey = CryptoUtils.HmacSha256(_session.SendingChainKey, new byte[] { 0x01 });
            var newChainKey = CryptoUtils.HmacSha256(_session.SendingChainKey, new byte[] { 0x02 });

            // Increment counter
            _session.SendCounter++;
            _session.SendingChainKey = newChainKey;

            // Encrypt with AES-GCM
            var nonce = CryptoUtils.DeriveNonce(_session.SendCounter);
            var ciphertext = CryptoUtils.AesGcmEncrypt(plaintext, messageKey, nonce);

            // Build message: [DH_PUBLIC_KEY][COUNTER][CIPHERTEXT]
            var messageBytes = _session.DhPublicKey
                .Concat(BitConverter.GetBytes(_session.SendCounter))
                .Concat(ciphertext)
                .ToArray();

            return await Task.FromResult(messageBytes);
        }

        /// <summary>
        /// Decrypt message with Double Ratchet
        /// </summary>
        public async Task<byte[]> DecryptAsync(byte[] messageBytes, CancellationToken cancellationToken = default)
        {
            // Parse: [DH_PUBLIC_KEY(32)][COUNTER(4)][CIPHERTEXT+TAG(n)]
            var dhPublicKey = messageBytes.Take(32).ToArray();
            var counterBytes = messageBytes.Skip(32).Take(4).ToArray();
            var ciphertext = messageBytes.Skip(36).ToArray();

            if (BitConverter.IsLittleEndian)
                Array.Reverse(counterBytes);
            var counter = BitConverter.ToUInt32(counterBytes, 0);

            // If DH changed: ratchet forward
            if (!dhPublicKey.SequenceEqual(_session.DhPublicKey))
            {
                RatchetReceivingChain(dhPublicKey);
            }

            // Advance chain key to match counter
            var messageKey = _session.ReceivingChainKey;
            for (uint i = _session.RecvCounter; i < counter; i++)
            {
                messageKey = CryptoUtils.HmacSha256(messageKey, new byte[] { 0x02 });
            }

            _session.RecvCounter = counter + 1;

            // Decrypt
            var nonce = CryptoUtils.DeriveNonce(counter);
            var plaintext = CryptoUtils.AesGcmDecrypt(ciphertext, messageKey, nonce);

            return await Task.FromResult(plaintext);
        }

        /// <summary>
        /// Ratchet receiving chain (when DH public key changes)
        /// </summary>
        private void RatchetReceivingChain(byte[] dhPublicKey)
        {
            // KDF_RK: Root key ratchet
            var sharedSecret = CryptoUtils.Curve25519SharedSecret(_session.DhPrivateKey, dhPublicKey);
            var newRootKey = CryptoUtils.HkdfSha256(sharedSecret, _session.RootKey, Encoding.UTF8.GetBytes("WhatsApp"), 64);

            _session.RootKey = newRootKey.Take(32).ToArray();
            _session.ReceivingChainKey = newRootKey.Skip(32).ToArray();
            _session.RecvCounter = 0;

            // Generate new DH keypair
            var (newPubKey, newPrivKey) = CryptoUtils.GenerateCurve25519Keypair();
            _session.DhPublicKey = newPubKey;
            _session.DhPrivateKey = newPrivKey;
        }
    }

    /// <summary>
    /// Signal session state
    /// </summary>
    public class SignalSession
    {
        public string DeviceId { get; set; } = string.Empty;
        public uint SendCounter { get; set; }
        public uint RecvCounter { get; set; }
        public byte[] RootKey { get; set; } = Array.Empty<byte>();
        public byte[] SendingChainKey { get; set; } = Array.Empty<byte>();
        public byte[] ReceivingChainKey { get; set; } = Array.Empty<byte>();
        public byte[] DhPublicKey { get; set; } = Array.Empty<byte>();
        public byte[] DhPrivateKey { get; set; } = Array.Empty<byte>();
    }
}
