using System.ComponentModel;
using System.ComponentModel.DataAnnotations;
using BlazorClaw.Core.Tools;

namespace BlazorClaw.Server.Tools.Memory;

public class MemorySearchParams
{
    [Description("Suchbegriff für Memory")]
    [Required]
    public string Query { get; set; } = string.Empty;
}

public class MemorySearchTool : BaseTool<MemorySearchParams>
{
    public override string Name => "memory_search";
    public override string Description => "Suche in Dokumenten/Notizen";

    protected override Task<string> ExecuteInternalAsync(MemorySearchParams p, ToolContext context)
    {
        // TODO: Vektor/Text Suche im Memory
        return Task.FromResult($"Suche im Memory nach {p.Query}.");
    }
}
