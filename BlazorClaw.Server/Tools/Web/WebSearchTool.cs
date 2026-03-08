using System.ComponentModel;
using System.ComponentModel.DataAnnotations;
using BlazorClaw.Core.Tools;
using BlazorClaw.Core.Web;

namespace BlazorClaw.Server.Tools.Web;

public class WebSearchTool : BaseTool<WebSearchTool.Params>
{
    private readonly IWebSearchProvider _webSearchProvider;

    public WebSearchTool(IWebSearchProvider webSearchProvider)
    {
        _webSearchProvider = webSearchProvider;
    }

    public override string Name => "web_search";
    public override string Description => "Sucht im Web nach Informationen.";

    public class Params
    {
        [Required, Description("Suchbegriff")]
        public string Query { get; set; } = string.Empty;

        [Description("Anzahl der Ergebnisse (Standard: 5)")]
        public int Count { get; set; } = 5;
    }

    protected override async Task<string> ExecuteInternalAsync(Params parameters, ToolContext context)
    {
        return await _webSearchProvider.SearchAsync(parameters.Query, parameters.Count);
    }
}
