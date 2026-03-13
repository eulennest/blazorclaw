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
        public string GetMediaFolder() => Path.Combine(GetBaseFolder(), "uploads");

        public async Task<Tuple<Stream, string>?> GetMediaFile(string fileName)
        {
            if (!Guid.TryParse(Path.GetFileNameWithoutExtension(fileName), out _)) return null;
            var file = Path.Combine(GetMediaFolder(), fileName);
            if (!System.IO.File.Exists(file)) return null;
            return Tuple.Create((Stream)File.OpenRead(file), GetContentType(file));
        }

        public static string GetContentType(string filename)
        {
            var rext = Path.GetExtension(filename).ToLowerInvariant();
            if (rext == ".png") return "image/png";
            if (rext == ".jpg" || rext == ".jpeg") return "image/jpeg";
            if (rext == ".txt") return "text/plain";
            return "application/octet-stream";
        }

        public static string GetExtension(string contentType)
        {
            if (contentType.Contains("image/png")) return ".png";
            else if (contentType.Contains("image/jpeg")) return ".jpg";
            else if (contentType.Contains("image/jpg")) return ".jpg";
            else if (contentType.Contains("text/")) return ".txt";
            return ".dat";
        }

        public async Task<string?> SaveMediaFileAsync(string data)
        {
            if (data.StartsWith("data:"))
            {
                // Split the string to escape the real data

                var b64 = data.Split(",".ToCharArray(), 2);
                var ext = GetExtension(b64[0]);
                var filename = Path.Combine(GetMediaFolder(), $"{Guid.NewGuid()}{ext}");
                // Convert the base 64 String to byte array
                byte[] byteArray = Convert.FromBase64String(b64[1]);
                await File.WriteAllBytesAsync(filename, byteArray);
                return filename;
            }
            if (data.StartsWith("http://") || data.StartsWith("htts://"))
            {
                var uri = new Uri(data);
                var ext = Path.GetExtension(uri.AbsolutePath);
                if (string.IsNullOrWhiteSpace(ext)) ext = ".dat";
                var filename = Path.Combine(GetMediaFolder(), $"{Guid.NewGuid()}{ext}");

                using var strm = await httpClient.GetStreamAsync(uri);
                using (var fStrm = File.OpenWrite(filename))
                {
                    await strm.CopyToAsync(fStrm);
                }
                return filename;
            }

            return null;
        }
    }
}
