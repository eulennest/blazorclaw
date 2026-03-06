using System.ComponentModel;
using System.ComponentModel.DataAnnotations;
using BlazorClaw.Core.Tools;

namespace BlazorClaw.Server.Tools.Web;

public class WebFetchParams
{
    [Description("URL zum Abrufen")]
    [Required]
    public string Url { get; set; } = string.Empty;
}

public class WebFetchTool : BaseTool<WebFetchParams>
{
    public override string Name => "web_fetch";
    public override string Description => "Abrufen einer Webseite als Markdown";

    protected override Task<string> ExecuteInternalAsync(WebFetchParams p, ToolContext context)
    {
        // TODO: HttpContent Extraction
        return Task.FromResult($"Inhalt von {p.Url} abgerufen.");
    }
}
