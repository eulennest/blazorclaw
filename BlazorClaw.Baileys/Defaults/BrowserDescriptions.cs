namespace Baileys.Defaults;

/// <summary>
/// Browser description presets that mirror the TypeScript
/// <c>Utils/browser-utils.ts</c> Browsers map.
///
/// Each browser description is a triplet of [platform, browser, version].
/// These are sent to WhatsApp servers to identify the connecting client.
/// </summary>
public static class Browsers
{
    // ──────────────────────────────────────────────────────────
    //  Predefined browser descriptions
    // ──────────────────────────────────────────────────────────

    /// <summary>Returns an Ubuntu browser description with the given browser name.</summary>
    public static string[] Ubuntu(string browser = "Chrome")
        => ["Ubuntu", browser, "22.04.4"];

    /// <summary>Returns a macOS browser description with the given browser name.</summary>
    public static string[] MacOs(string browser = "Chrome")
        => ["Mac OS", browser, "14.4.1"];

    /// <summary>Returns a Baileys custom browser description.</summary>
    public static string[] Baileys(string browser = "Desktop")
        => ["Baileys", browser, "6.5.0"];

    /// <summary>Returns a Windows browser description with the given browser name.</summary>
    public static string[] Windows(string browser = "Chrome")
        => ["Windows", browser, "10.0.22631"];

    /// <summary>
    /// Returns a browser description based on the current OS.
    /// Falls back to Ubuntu when the OS is not recognized.
    /// </summary>
    public static string[] Appropriate(string browser = "Chrome")
    {
        var platform = System.Runtime.InteropServices.RuntimeInformation.IsOSPlatform(
                           System.Runtime.InteropServices.OSPlatform.OSX)
            ? "Mac OS"
            : System.Runtime.InteropServices.RuntimeInformation.IsOSPlatform(
                  System.Runtime.InteropServices.OSPlatform.Windows)
                ? "Windows"
                : "Ubuntu";

        return [platform, browser, System.Environment.OSVersion.Version.ToString()];
    }

    // ──────────────────────────────────────────────────────────
    //  HKDF key mapping — media type → encryption key label
    // ──────────────────────────────────────────────────────────

    /// <summary>
    /// Maps each media type string to the HKDF info label used when
    /// deriving the media encryption key.
    /// </summary>
    public static readonly IReadOnlyDictionary<string, string> MediaHkdfKeyMapping
        = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase)
        {
            ["audio"]                 = "Audio",
            ["document"]              = "Document",
            ["gif"]                   = "Video",
            ["image"]                 = "Image",
            ["ppic"]                  = "",
            ["product"]               = "Image",
            ["ptt"]                   = "Audio",
            ["sticker"]               = "Image",
            ["video"]                 = "Video",
            ["thumbnail-document"]    = "Document Thumbnail",
            ["thumbnail-image"]       = "Image Thumbnail",
            ["thumbnail-video"]       = "Video Thumbnail",
            ["thumbnail-link"]        = "Link Thumbnail",
            ["md-msg-hist"]           = "History",
            ["md-app-state"]          = "App State",
            ["product-catalog-image"] = "",
            ["payment-bg-image"]      = "Payment Background",
            ["ptv"]                   = "Video",
            ["biz-cover-photo"]       = "Image"
        };

    /// <summary>
    /// Maps each media type string to its WhatsApp upload/download server path segment.
    /// </summary>
    public static readonly IReadOnlyDictionary<string, string> MediaPathMap
        = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase)
        {
            ["image"]                 = "/mms/image",
            ["video"]                 = "/mms/video",
            ["document"]              = "/mms/document",
            ["audio"]                 = "/mms/audio",
            ["sticker"]               = "/mms/image",
            ["thumbnail-link"]        = "/mms/image",
            ["product-catalog-image"] = "/product/image",
            ["md-app-state"]          = "",
            ["md-msg-hist"]           = "/mms/md-app-state",
            ["biz-cover-photo"]       = "/pps/biz-cover-photo"
        };
}
