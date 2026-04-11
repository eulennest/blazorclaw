using BlazorClaw.Core.Utils;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.Configuration;

namespace BlazorClaw.Core.Services
{
    public class PathHelper(IWebHostEnvironment env, HttpClient httpClient, IHttpContextAccessor contextAccessor, IConfiguration conf)
    {
        private Uri? baseUrl;
        public Uri GetBaseUrl()
        {
            baseUrl ??= conf.GetValue<Uri>("Web:BaseUrl");
            if (baseUrl == null && contextAccessor.HttpContext != null)
            {
                var ub = new UriBuilder
                {
                    Scheme = contextAccessor.HttpContext.Request.Scheme,
                    Host = contextAccessor.HttpContext.Request.Host.Host,
                    Port = contextAccessor.HttpContext.Request.Host.Port ?? -1
                };
                baseUrl = ub.Uri;
            }
            return baseUrl ?? new Uri("http://localhost");
        }
        public Uri GetUrl(string relativePath)
        {
            return new Uri(GetBaseUrl(), relativePath);
        }

        public Uri GetMediaUrl(string mediaFile)
        {
            mediaFile = Path.GetFileName(mediaFile);
            return new Uri(GetBaseUrl(), $"/uploads/{mediaFile}");
        }

        public string GetBaseFolder() => env.ContentRootPath;
        public string GetMediaFolder()
        {
            var path = Path.Combine(GetBaseFolder(), "uploads");
            Directory.CreateDirectory(path);
            return path;
        }

        public async Task<Tuple<Stream, string>?> GetMediaFileAsync(string fileName)
        {
            if (!Guid.TryParse(Path.GetFileNameWithoutExtension(fileName), out _)) return null;
            var file = Path.Combine(GetMediaFolder(), Path.GetFileName(fileName));
            if (!System.IO.File.Exists(file)) return null;
            return Tuple.Create((Stream)File.OpenRead(file), GetContentType(file));
        }

        public static string GetContentType(string? filename)
        {
            if (!string.IsNullOrWhiteSpace(filename))
            {
                var rext = Path.GetExtension(filename).ToLowerInvariant();
                if (rext == ".png") return "image/png";
                if (rext == ".jpg" || rext == ".jpeg") return "image/jpeg";
                if (rext == ".txt") return "text/plain";
                if (rext == ".ogg") return "audio/opus";
            }
            return "application/octet-stream";
        }

        public static string GetExtension(string contentType)
        {
            if (contentType.Contains("image/png")) return ".png";
            else if (contentType.Contains("image/jpeg")) return ".jpg";
            else if (contentType.Contains("image/jpg")) return ".jpg";
            else if (contentType.Contains("text/")) return ".txt";
            else if (contentType.Contains("audio/opus")) return ".ogg";
            else if (contentType.Contains("audio/ogg")) return ".ogg";
            else if (contentType.Contains("audio/mpeg")) return ".mp3";
            return ".dat";
        }

        public async Task<string?> SaveMediaFileAsync(string data)
        {
            try
            {


                if (data.StartsWith("data:"))
                {
                    // Split the string to escape the real data

                    var b64 = data.Split(",".ToCharArray(), 2);
                    var mime = b64[0];
                    // Convert the base 64 String to byte array
                    byte[] byteArray = Convert.FromBase64String(b64[1]);
                    if (string.IsNullOrEmpty(mime) || mime.Contains("stream") || mime.Contains("base64"))
                    {
                        mime = Mimetype.DetectMimeType(byteArray);
                    }
                    var ext = Mimetype.GetExtensionFromMimeType(mime) ?? ".dat";
                    var filename = Path.Combine(GetMediaFolder(), $"{Guid.NewGuid()}{ext}");
                    await File.WriteAllBytesAsync(filename, byteArray);
                    return filename;
                }
                if (data.StartsWith("http://") || data.StartsWith("https://"))
                {
                    var uri = new Uri(data);
                    if (uri.Host == GetBaseUrl().Host) return data;

                    var ext = Path.GetExtension(uri.AbsolutePath);
                    if (string.IsNullOrWhiteSpace(ext)) ext = "";
                    var filename = Path.Combine(GetMediaFolder(), $"{Guid.NewGuid()}{ext}");

                    using var strm = await httpClient.GetStreamAsync(uri);
                    using (var fStrm = File.OpenWrite(filename))
                    {
                        await strm.CopyToAsync(fStrm);
                    }
                    if (ext == "")
                    {
                        using var fStrm = File.OpenRead(filename);
                        var buff = new byte[1024];
                        fStrm.Read(buff, 0, buff.Length);
                        var mime = Mimetype.DetectMimeType(buff);
                        var newExt = Mimetype.GetExtensionFromMimeType(mime);
                        if (!string.IsNullOrWhiteSpace(newExt) && newExt != ext)
                        {
                            var newFilename = filename + newExt;
                            File.Move(filename, newFilename);
                            filename = newFilename;
                        }
                    }
                    return filename;
                }
                if (data.StartsWith("file://"))
                {
                    var uri = new Uri(data);
                    if (uri.IsFile) return uri.LocalPath;
                }
            }
            catch
            {
            }
            return null;
        }

        public async Task<string?> SaveMediaFileAsync(Tuple<Stream, string>? tuple)
        {
            if (tuple == null) return null;
            var ext = GetExtension(tuple.Item2);
            var filename = Path.Combine(GetMediaFolder(), $"{Guid.NewGuid()}{ext}");

            using var s = tuple.Item1;
            using var fs = File.OpenWrite(filename);
            await s.CopyToAsync(fs);
            return filename;
        }
    }
}
