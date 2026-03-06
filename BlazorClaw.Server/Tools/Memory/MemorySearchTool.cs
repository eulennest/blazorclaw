using System.ComponentModel;
using System.ComponentModel.DataAnnotations;
using BlazorClaw.Core.Memory;
using BlazorClaw.Core.Tools;

namespace BlazorClaw.Server.Tools.Memory;

public class MemorySearchParams
{
    [Description("Suchbegriffe für Memory")]
    [Required]
    public string[] Queries { get; set; } = Array.Empty<string>();

    [Description("Maximale Anzahl Ergebnisse")]
    public int MaxResults { get; set; } = 5;
}

public class MemorySearchTool : BaseTool<MemorySearchParams>
{
    public override string Name => "memory_search";
    public override string Description => "Suche in Dokumenten/Notizen";

    protected override async Task<string> ExecuteInternalAsync(MemorySearchParams p, ToolContext context)
    {
        var provider = context.ServiceProvider.GetRequiredService<IMemorySearchProvider>();
        return await provider.SearchAsync(p.Queries, p.MaxResults);
    }
}
