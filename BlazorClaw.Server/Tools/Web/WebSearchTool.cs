using System.ComponentModel;
using System.ComponentModel.DataAnnotations;
using BlazorClaw.Core.Tools;
using BlazorClaw.Core.Web;

namespace BlazorClaw.Server.Tools.Web;

public class WebSearchTool(IWebSearchProvider webSearchProvider) : BaseTool<WebSearchTool.Params>
{
    public override string Name => "web_search";
    public override string Description => "Sucht im Web nach Informationen.";

    public class Params
    {
        [Required, Description("Suchbegriff")]
        public string Query { get; set; } = string.Empty;

        [Description("Anzahl der Ergebnisse (Standard: 10)")]
        public int? Count { get; set; } = 10;
    }

    protected override async Task<string> ExecuteInternalAsync(Params parameters, ToolContext context)
    {
        return await webSearchProvider.SearchAsync(parameters.Query, parameters.Count ?? 10);
    }
}
