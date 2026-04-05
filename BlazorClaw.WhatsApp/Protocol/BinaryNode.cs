namespace BlazorClaw.WhatsApp.Protocol
{
    /// <summary>
    /// Binary Node - WhatsApp's custom binary XML format
    /// Used for most messages after handshake
    /// Reference: Baileys WABinary encoder/decoder
    /// </summary>
    public class BinaryNode
    {
        public string Tag { get; set; } = string.Empty;
        public Dictionary<string, string> Attrs { get; set; } = new();
        public object? Content { get; set; } // Can be: string, byte[], List<BinaryNode>

        public BinaryNode() { }

        public BinaryNode(string tag, Dictionary<string, string>? attrs = null, object? content = null)
        {
            Tag = tag;
            Attrs = attrs ?? new();
            Content = content;
        }

        /// <summary>
        /// Get child node by tag
        /// </summary>
        public BinaryNode? GetChild(string tag)
        {
            if (Content is List<BinaryNode> children)
            {
                return children.FirstOrDefault(c => c.Tag == tag);
            }
            return null;
        }

        /// <summary>
        /// Get all children with tag
        /// </summary>
        public List<BinaryNode> GetChildren(string tag)
        {
            if (Content is List<BinaryNode> children)
            {
                return children.Where(c => c.Tag == tag).ToList();
            }
            return new();
        }

        /// <summary>
        /// Get all children
        /// </summary>
        public List<BinaryNode> GetAllChildren()
        {
            if (Content is List<BinaryNode> children)
            {
                return children;
            }
            return new();
        }

        public override string ToString()
        {
            var attrs = string.Join(", ", Attrs.Select(kv => $"{kv.Key}={kv.Value}"));
            var contentPreview = Content switch
            {
                string s => $"\"{s.Substring(0, Math.Min(s.Length, 30))}\"",
                byte[] b => $"<bytes {b.Length}>",
                List<BinaryNode> nodes => $"[{nodes.Count} children]",
                _ => "null"
            };
            return $"<{Tag} {attrs}>{contentPreview}</{Tag}>";
        }
    }

    /// <summary>
    /// Binary Node Encoder/Decoder
    /// Implements WhatsApp's binary XML format
    /// </summary>
    public static class BinaryNodeCodec
    {
        // WhatsApp binary tags (simplified version — full list has 200+ tags)
        private static readonly Dictionary<int, string> _tags = new()
        {
            { 0, "iq" },
            { 1, "pair-device" },
            { 2, "pair-success" },
            { 3, "success" },
            { 4, "stream:error" },
            { 5, "failure" },
            { 6, "notification" },
            { 7, "message" },
            { 8, "presence" },
            { 9, "ack" },
            { 10, "text" },
            { 11, "to" },
            { 12, "from" },
            { 13, "id" },
            { 14, "type" },
            { 15, "xmlns" },
            { 16, "ref" },
            { 17, "qr" },
            { 18, "ephemeral" },
            { 19, "static" },
            { 20, "payload" }
        };

        private static readonly Dictionary<string, int> _tagsReverse = 
            _tags.ToDictionary(kv => kv.Value, kv => kv.Key);

        /// <summary>
        /// Encode BinaryNode to bytes
        /// </summary>
        public static byte[] Encode(BinaryNode node)
        {
            using var ms = new MemoryStream();
            WriteBinaryNode(ms, node);
            return ms.ToArray();
        }

        /// <summary>
        /// Decode bytes to BinaryNode
        /// </summary>
        public static BinaryNode Decode(byte[] data)
        {
            using var ms = new MemoryStream(data);
            return ReadBinaryNode(ms);
        }

        private static void WriteBinaryNode(Stream stream, BinaryNode node)
        {
            // Write tag
            WriteString(stream, node.Tag);

            // Write attributes count
            stream.WriteByte((byte)node.Attrs.Count);

            // Write attributes
            foreach (var (key, value) in node.Attrs)
            {
                WriteString(stream, key);
                WriteString(stream, value);
            }

            // Write content
            if (node.Content == null)
            {
                stream.WriteByte(0x00); // No content
            }
            else if (node.Content is string text)
            {
                stream.WriteByte(0x01); // String content
                WriteString(stream, text);
            }
            else if (node.Content is byte[] bytes)
            {
                stream.WriteByte(0x02); // Binary content
                WriteBytes(stream, bytes);
            }
            else if (node.Content is List<BinaryNode> children)
            {
                stream.WriteByte(0x03); // List content
                stream.WriteByte((byte)children.Count);
                foreach (var child in children)
                {
                    WriteBinaryNode(stream, child);
                }
            }
        }

        private static BinaryNode ReadBinaryNode(Stream stream)
        {
            var tag = ReadString(stream);
            var attrCount = stream.ReadByte();
            var attrs = new Dictionary<string, string>();

            for (int i = 0; i < attrCount; i++)
            {
                var key = ReadString(stream);
                var value = ReadString(stream);
                attrs[key] = value;
            }

            var contentType = stream.ReadByte();
            object? content = contentType switch
            {
                0x00 => null,
                0x01 => ReadString(stream),
                0x02 => ReadBytes(stream),
                0x03 => ReadNodeList(stream),
                _ => null
            };

            return new BinaryNode(tag, attrs, content);
        }

        private static List<BinaryNode> ReadNodeList(Stream stream)
        {
            var count = stream.ReadByte();
            var nodes = new List<BinaryNode>();
            for (int i = 0; i < count; i++)
            {
                nodes.Add(ReadBinaryNode(stream));
            }
            return nodes;
        }

        private static void WriteString(Stream stream, string str)
        {
            var bytes = System.Text.Encoding.UTF8.GetBytes(str);
            WriteBytes(stream, bytes);
        }

        private static string ReadString(Stream stream)
        {
            var bytes = ReadBytes(stream);
            return System.Text.Encoding.UTF8.GetString(bytes);
        }

        private static void WriteBytes(Stream stream, byte[] bytes)
        {
            // Write length (3 bytes, big-endian)
            stream.WriteByte((byte)(bytes.Length >> 16));
            stream.WriteByte((byte)(bytes.Length >> 8));
            stream.WriteByte((byte)bytes.Length);
            stream.Write(bytes, 0, bytes.Length);
        }

        private static byte[] ReadBytes(Stream stream)
        {
            // Read length (3 bytes, big-endian)
            var len = (stream.ReadByte() << 16) | (stream.ReadByte() << 8) | stream.ReadByte();
            var bytes = new byte[len];
            stream.Read(bytes, 0, len);
            return bytes;
        }
    }
}
