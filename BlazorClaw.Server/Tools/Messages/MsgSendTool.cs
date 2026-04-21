using BlazorClaw.Core.Commands;
using BlazorClaw.Core.Services;
using BlazorClaw.Core.Sessions;
using BlazorClaw.Core.Tools;
using Microsoft.Extensions.AI;
using System.ComponentModel;
using System.ComponentModel.DataAnnotations;

namespace BlazorClaw.Server.Tools;

public class MsgSendTool : BaseTool<MsgSendTool.Params>
{
    public override string Name => "msg_send";
    public override string Description => "Sendet eine Nachricht an einen User per Channel";

    protected override async Task<string> ExecuteInternalAsync(Params p, MessageContext context)
    {
        var cr = context.Provider.GetRequiredService<ChannelRegistry>();
        var bots = cr.Where(o => o.ChannelProvider.Equals(p.Provider, StringComparison.InvariantCultureIgnoreCase));
        var bot = string.IsNullOrWhiteSpace(p.BotID) ? bots.FirstOrDefault() : bots.FirstOrDefault(o => o.BotId.Equals(p.BotID, StringComparison.InvariantCultureIgnoreCase));
        if (bot == null) throw new KeyNotFoundException("No Bot found");

        var scs = new ChannelSession(bot, p.Reciepient);
        var msg = new ChatMessage(ChatRole.Assistant, p.Message);
        await bot.SendUserAsync(scs, msg);
        return "Message send.";
    }

    public class Params
    {
        [Required]
        public string Provider { get; set; }

        [Description("BotId des Providers (Optional: wenn nicht angegeben, erster Bot für den Provider)")]
        public string? BotID { get; set; }


        [Required]
        [Description("Empfänger im Format für den entsprechenden Provider")]
        public string Reciepient { get; set; } = string.Empty;

        [Required]
        [Description("Zu sendende Nachricht, Media-Content kann, per MEDIA-Tags gesendet werden")]
        public string Message { get; set; } = string.Empty;
    }
}