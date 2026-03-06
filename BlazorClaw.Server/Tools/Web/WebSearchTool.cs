using System.ComponentModel;
using System.ComponentModel.DataAnnotations;
using BlazorClaw.Core.Tools;

namespace BlazorClaw.Server.Tools.Web;

public class SearchParams
{
    [Description("Suchbegriff")]
    [Required]
    public string Query { get; set; } = string.Empty;
}

public class WebSearchTool : BaseTool<SearchParams>
{
    public override string Name => "web_search";
    public override string Description => "Suche im Web via Brave API";

    protected override Task<string> ExecuteInternalAsync(SearchParams p, ToolContext context)
    {
        // TODO: Brave Search API Integration (oder ähnlich)
        return Task.FromResult($"Suche nach {p.Query} durchgeführt.");
    }
}
