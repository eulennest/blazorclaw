namespace BlazorClaw.Core.Utils
{
    public static class Mimetype
    {
        public const string PNG = "image/png";
        public const string JPEG = "image/jpeg";
        public const string GIF = "image/gif";
        public const string PDF = "application/pdf";

        private static Dictionary<string, string>? mimetypes = null;
        private static Dictionary<string, string>? mimetypesExt = null;

        private static readonly object syncObj = new();
        private static Dictionary<string, string> ReadMimetypes()
        {
            lock (syncObj)
            {
                if (mimetypes != null) return mimetypes;
                mimetypes = [];
                mimetypesExt = [];

                using var stream = new MemoryStream(Resources.mimetypes);
                using var reader = new StreamReader(stream);
                string? line = null;
                while ((line = reader.ReadLine()) != null)
                {
                    string[] cols = line.Trim().Split(['\t', ' '], StringSplitOptions.RemoveEmptyEntries);
                    for (int i = 1; i < cols.Length; i++)
                    {
                        mimetypes.Add(cols[i], cols[0]);
                        if (!mimetypesExt.ContainsKey(cols[0]))
                        {
                            mimetypesExt.Add(cols[0], cols[i]);
                        }
                    }
                }
            }
            return mimetypes;
        }
        public static string? GetMimeTypeFromExtension(string filename)
        {
            var ext = Path.GetExtension(filename).ToLowerInvariant();
            var dict = ReadMimetypes();
            if (dict.TryGetValue(ext, out var mime)) return mime;
            return null;
        }

        public static string? GetExtensionFromMimeType(string mimetype)
        {
            if (mimetypesExt == null) ReadMimetypes();
            if (mimetypesExt?.TryGetValue(mimetype, out var ext) ?? false) return ext;
            return null;
        }

        // byte[] is implicitly convertible to ReadOnlySpan<byte>
        private static bool FirstBytesCompare(byte[] a1, byte[] a2)
        {
            if (a1.Length > a2.Length) return false;
            return a1.SequenceEqual(a2.Take(a1.Length));
        }

        private static IEnumerable<MimeTypeInfo> GetMimeSignatures()
        {
            yield return new MimeTypeInfo(PDF, "%PDF-"u8.ToArray());
            yield return new MimeTypeInfo("image/x-ms-bmp", 'B', 'M');
            yield return new MimeTypeInfo(PNG, 0x89, 0x50, 0x4e, 0x47, 0x0d, 0x0a, 0x1a, 0x0a);
            yield return new MimeTypeInfo(GIF, 'G', 'I', 'F');
            yield return new MimeTypeInfo(JPEG, 0xff, 0xd8, 0xff);
            yield return new MimeTypeInfo("image/psd", '8', 'B', 'P', 'S');
            yield return new MimeTypeInfo("image/iff", 'F', 'O', 'R', 'M');
            yield return new MimeTypeInfo("image/webp", 'R', 'I', 'F', 'F');
            yield return new MimeTypeInfo("image/vnd.microsoft.icon", 0x00, 0x00, 0x01, 0x00);
            yield return new MimeTypeInfo("image/tiff", (byte)'I', (byte)'I', 0x2A, 0x00);
            yield return new MimeTypeInfo("image/tiff", (byte)'M', (byte)'M', 0x00, 0x2A);
        }

        private class MimeTypeInfo
        {
            public MimeTypeInfo(string mime, params char[] sig)
            {
                MimeType = mime;
                Signature = [.. sig.Select(c => (byte)c)];
            }
            public MimeTypeInfo(string mime, params byte[] sig)
            {
                MimeType = mime;
                Signature = sig;
            }
            public byte[] Signature;
            public string MimeType;
        }

        public static string? DetectMimeType(this byte[] data)
        {

            foreach (var item in GetMimeSignatures())
            {
                if (FirstBytesCompare(item.Signature, data)) return item.MimeType;
            }
            return null;
        }
        public static string? DetectMimeType(this Stream strm)
        {

            var list = GetMimeSignatures().ToList();
            var size = list.Max(o => o.Signature.Length);
            var data = new byte[size];
            strm.Read(data, 0, size);
            strm.Seek(0, SeekOrigin.Begin);

            foreach (var item in list)
            {
                if (FirstBytesCompare(item.Signature, data)) return item.MimeType;
            }
            return null;

        }
    }
}
