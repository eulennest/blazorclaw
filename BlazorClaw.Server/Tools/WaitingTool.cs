using BlazorClaw.Core.Commands;
using BlazorClaw.Core.Tools;
using System.ComponentModel;
using System.ComponentModel.DataAnnotations;

namespace BlazorClaw.Server.Tools;

public class WaitToolParams
{
    [Description("Wait Duration in seconds Range: 0 - 120  Default: 1")]
    [Range(0, 120)]
    public int? Duration { get; set; } = 1;
}

public class WaitingTool() : BaseTool<WaitToolParams>
{
    public override string Name => "sleep_sec";
    public override string Description => "Wartet eine bestimmte Anzahl an Sekunden. Kann z.B. verwendet werden um den User eine Nachricht zu senden und danach direkt weiter arbeiten zu können. (z.b. Erstelle dir nun ein schönes Bild.  [Wait_sec] -> [image_gen] -> output)";

    protected override async Task<string> ExecuteInternalAsync(WaitToolParams p, MessageContext context)
    {
        await Task.Delay(TimeSpan.FromSeconds(p.Duration ?? 1));
        return "OK";
    }
}
