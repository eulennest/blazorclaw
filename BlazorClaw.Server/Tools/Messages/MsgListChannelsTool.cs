using BlazorClaw.Core.Commands;
using BlazorClaw.Core.Services;
using BlazorClaw.Core.Tools;
using System.Text;

namespace BlazorClaw.Server.Tools;

public class MsgListChannelsTool : BaseTool<EmptyParams>
{
    public override string Name => "msg_listchannels";
    public override string Description => "Gibt alle registrieren Message Channels zurück.";

    protected override Task<string> ExecuteInternalAsync(EmptyParams p, MessageContext context)
    {
        var cr = context.Provider.GetRequiredService<ChannelRegistry>();

        var sb = new StringBuilder();

        sb.AppendLine($"Provider;BotID;Description");
        foreach (var item in cr)
        {
            sb.AppendLine($"{item.ChannelProvider};{item.BotId};");
        }

        return Task.FromResult(sb.ToString());
    }
}