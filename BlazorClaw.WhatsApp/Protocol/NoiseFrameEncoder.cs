namespace BlazorClaw.WhatsApp.Protocol
{
    /// <summary>
    /// Noise Protocol Frame Encoder/Decoder
    /// WhatsApp uses 3-byte length prefix + optional intro header
    /// Reference: Baileys noise-handler.ts
    /// </summary>
    public static class NoiseFrameEncoder
    {
        // WhatsApp Noise Header: "WA" + Protocol Version + Dict Version
        private static readonly byte[] NOISE_WA_HEADER = new byte[] { 0x57, 0x41, 0x06, 0x03 };
        
        private static bool _sentIntro = false;

        /// <summary>
        /// Encode frame for sending (adds intro header on first call)
        /// </summary>
        public static byte[] EncodeFrame(byte[] data, bool isHandshake = false)
        {
            using var ms = new MemoryStream();

            // On first frame, prepend NOISE_WA_HEADER
            if (!_sentIntro && isHandshake)
            {
                ms.Write(NOISE_WA_HEADER, 0, NOISE_WA_HEADER.Length);
                _sentIntro = true;
            }

            // Write 3-byte length (big-endian)
            ms.WriteByte((byte)(data.Length >> 16));
            ms.WriteByte((byte)(data.Length >> 8));
            ms.WriteByte((byte)data.Length);

            // Write data
            ms.Write(data, 0, data.Length);

            return ms.ToArray();
        }

        /// <summary>
        /// Decode incoming frame (extract data from 3-byte length prefix)
        /// </summary>
        public static (int length, byte[] data) DecodeFrame(byte[] buffer)
        {
            if (buffer.Length < 3)
                throw new InvalidOperationException("Buffer too small for frame header");

            // Read 3-byte length (big-endian)
            var length = (buffer[0] << 16) | (buffer[1] << 8) | buffer[2];

            if (buffer.Length < length + 3)
                throw new InvalidOperationException($"Buffer too small for frame (expected {length + 3}, got {buffer.Length})");

            var data = new byte[length];
            Array.Copy(buffer, 3, data, 0, length);

            return (length, data);
        }

        /// <summary>
        /// Reset intro flag (for testing or new connection)
        /// </summary>
        public static void ResetIntro()
        {
            _sentIntro = false;
        }
    }
}
