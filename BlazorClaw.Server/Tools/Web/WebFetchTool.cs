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

public class WebFetchTool(HttpClient client) : BaseTool<WebFetchParams>
{
    public override string Name => "web_fetch";
    public override string Description => "Abrufen einer Webseite (Quelltext)";

    protected override Task<string> ExecuteInternalAsync(WebFetchParams p, ToolContext context)
    {
        return client.GetStringAsync(p.Url);
    }
}
