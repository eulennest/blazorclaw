using System.ComponentModel;
using System.ComponentModel.DataAnnotations;
using BlazorClaw.Core.Tools;

namespace BlazorClaw.Server.Tools.Web;

public class WebFetchParams
{
    [Description("URL zum Abrufen")]
    [Required]
    public string Url { get; set; } = string.Empty;

    [Description("Abruf-Modus: Auto (Standard), Source, oder Markdown")]
    public FetchMode? Mode { get; set; } = FetchMode.Auto;
}

public enum FetchMode
{
    Auto,
    Source,
    Markdown
}

public class WebFetchTool(HttpClient client) : BaseTool<WebFetchParams>
{
    public override string Name => "web_fetch";
    public override string Description => "Abrufen einer Webseite";

    protected override async Task<string> ExecuteInternalAsync(WebFetchParams p, ToolContext context)
    {
        var mode = p.Mode ?? FetchMode.Auto;

        using var resp = await client.GetAsync(p.Url);
        resp.EnsureSuccessStatusCode();

        if(mode == FetchMode.Auto)
        {
            var contentType = resp.Content.Headers.ContentType?.MediaType;
            if (contentType != null && contentType.Contains("text/html"))
                mode = FetchMode.Source;
            else
                mode = FetchMode.Markdown;
        }

        if (p.Mode == FetchMode.Source)
            return await resp.Content.ReadAsStringAsync();
        var converter = new ReverseMarkdown.Converter();
        converter.Config.UnknownTags = ReverseMarkdown.Config.UnknownTagsOption.Bypass;
        converter.Config.RemoveComments = true;
        converter.Config.SmartHrefHandling = true;
        return converter.Convert(await resp.Content.ReadAsStringAsync());
    }
}
