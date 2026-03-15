using BlazorClaw.Core.Commands;
using BlazorClaw.Core.Memory;
using BlazorClaw.Core.Tools;
using System.ComponentModel;
using System.ComponentModel.DataAnnotations;

namespace BlazorClaw.Server.Tools.Memory;

public class MemorySearchParams
{
    [Description("Suchbegriffe für Memory")]
    [Required]
    public string[] Queries { get; set; } = [];

    [Description("Maximale Anzahl Ergebnisse (Default: 20)")]
    public int? MaxResults { get; set; } = 20;
}

public class MemorySearchTool : BaseTool<MemorySearchParams>
{
    public override string Name => "memory_search";
    public override string Description => "Suche in Dokumenten/Notizen";

    protected override async Task<string> ExecuteInternalAsync(MemorySearchParams p, MessageContext context)
    {
        var maxResults = p.MaxResults ?? 20;
        var provider = context.Provider.GetRequiredService<IMemorySearchProvider>();
        var ret = await provider.SearchAsync(p.Queries, maxResults, context).Take(maxResults).ToListAsync();
        return string.Join("\n\n---\n\n", ret);

    }
}
