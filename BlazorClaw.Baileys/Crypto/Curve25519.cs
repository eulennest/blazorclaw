using Baileys.Types;
using Curve25519Impl = org.whispersystems.curve25519.Curve25519;

namespace Baileys.Crypto
{
    /// <summary>
    /// Curve25519 Elliptic Curve Diffie-Hellman wrapper
    /// Uses curve25519-dotnet NuGet (managed C# implementation)
    /// </summary>
    public static class Curve25519Utils
    {
        private static readonly Curve25519Impl _instance = Curve25519Impl.getInstance(Curve25519Impl.BEST);

        /// <summary>
        /// Generate a random Curve25519 keypair
        /// </summary>
        public static KeyPair GenerateKeyPair()
        {
            var keyPair = _instance.generateKeyPair();
            return new(keyPair.getPublicKey(), keyPair.getPrivateKey());
        }

        /// <summary>
        /// Compute public key from private key
        /// </summary>
        public static byte[] ComputePublicKey(byte[] privateKey)
        {
            return _instance.generatePublicKey(privateKey);
        }

        /// <summary>
        /// Perform Curve25519 ECDH - compute shared secret
        /// </summary>
        public static byte[] CalculateAgreement(byte[] privateKey, byte[] publicKey)
        {
            return _instance.calculateAgreement(publicKey, privateKey);
        }

        /// <summary>
        /// Sign a message with Curve25519
        /// </summary>
        public static byte[] CalculateSignature(byte[] privateKey, byte[] message)
        {
            return _instance.calculateSignature(privateKey, message);
        }

        /// <summary>
        /// Verify a Curve25519 signature
        /// </summary>
        public static bool VerifySignature(byte[] publicKey, byte[] message, byte[] signature)
        {
            return _instance.verifySignature(publicKey, message, signature);
        }
    }
}
