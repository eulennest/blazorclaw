using System.Security.Cryptography;
using System.Text;

namespace BlazorClaw.Core.Utils
{
    // CryptoExtensions.cs
    public static class CryptoExtensions
    {
        private static readonly HashAlgorithmName AlgoSHA256 = HashAlgorithmName.SHA256;

        private static byte[] BuildHashBytes(HashAlgorithmName algo, string password, string salt, Encoding encoding)
        {
            byte[] passwordBytes = encoding.GetBytes(password);
            byte[] saltBytes = encoding.GetBytes(salt);

            return Rfc2898DeriveBytes.Pbkdf2(passwordBytes, saltBytes, 100000, algo, 32);
        }

        public static Task EncryptAsync(this Stream source, Stream destination, string password, string salt)
        {
            return EncryptAsync(source, destination, Aes.Create(),
                BuildHashBytes(AlgoSHA256, password, salt, Encoding.UTF8));
        }

        public static Task DecryptAsync(this Stream source, Stream destination, string password, string salt)
        {
            return DecryptAsync(source, destination, Aes.Create(),
                BuildHashBytes(AlgoSHA256, password, salt, Encoding.UTF8));
        }

        public static async Task EncryptAsync(this Stream source, Stream destination, SymmetricAlgorithm cryptAlgo, byte[] key)
        {
            cryptAlgo.Key = key;
            byte[] iv = cryptAlgo.IV;
            destination.Write(iv, 0, iv.Length);

            using (var cryptoStream = new CryptoStream(
                destination,
                cryptAlgo.CreateEncryptor(),
                CryptoStreamMode.Write))
            {
                await source.CopyToAsync(cryptoStream).ConfigureAwait(false);
                /*
                var buffer = new byte[8192];
                int read;
                while ((read = source.Read(buffer, 0, buffer.Length)) > 0)
                {
                    destination.Write(buffer, 0, read);
                    hashAlgo.TransformBlock(buffer, 0, read, buffer, 0);
                }
                hashAlgo.TransformFinalBlock(buffer, 0, 0);

                var hash = hashAlgo.Hash;
                destination.Write(hash, 0, hash.Length);
                */
            }
        }

        public static async Task DecryptAsync(this Stream source, Stream destination, SymmetricAlgorithm cryptAlgo, byte[] key)
        {
            byte[] iv = new byte[cryptAlgo.IV.Length];
            var numBytesToRead = cryptAlgo.IV.Length;
            int numBytesRead = 0;
            while (numBytesToRead > 0)
            {
                int n = await source.ReadAsync(iv, numBytesRead, numBytesToRead).ConfigureAwait(false);
                if (n == 0) break;

                numBytesRead += n;
                numBytesToRead -= n;
            }
            cryptAlgo.Key = key;
            cryptAlgo.IV = iv;
            using (var cryptoStream = new CryptoStream(
                source,
                cryptAlgo.CreateDecryptor(),
                CryptoStreamMode.Read))
            {
                await cryptoStream.CopyToAsync(destination).ConfigureAwait(false);
            }
        }
    }
}
