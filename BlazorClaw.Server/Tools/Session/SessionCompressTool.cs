using BlazorClaw.Core.Sessions;
using BlazorClaw.Core.Tools;
using BlazorClaw.Core.Commands;
using System.CommandLine;
using Microsoft.Extensions.DependencyInjection;

namespace BlazorClaw.Server.Tools.Session;

public class SessionCompressTool : BaseTool
{
    public SessionCompressTool() : base("session_compress", "Speichert eine Zusammenfassung der Konversation und komprimiert den Verlauf.")
    {
    }

    public override async Task<object?> ExecuteAsync(ParseResult result, CommandContext context)
    {
        var sessionManager = context.Provider.GetRequiredService<ISessionManager>();
        var sess = await sessionManager.GetSessionAsync(context.Session);
        
        if (sess == null) return "Session nicht gefunden.";

        var summary = result.GetValueForArgument((Argument<string>)result.CommandResult.Command.Arguments[0]);

        // Komprimiere den Verlauf: Historie leeren und Zusammenfassung als System-Message
        sess.MessageHistory.Clear();
        sess.MessageHistory.Add(new() { Role = "system", Content = $"Zusammenfassung des vorherigen Gesprächs: {summary}" });
        sess.CompactionCount++;

        await sessionManager.SaveToDiskAsync(sess);
        return $"Session komprimiert. Aktuelle Nachrichten: {sess.MessageHistory.Count}";
    }

    public override Command GetCommand()
    {
        var cmd = new Command(Name, Description);
        cmd.AddArgument(new Argument<string>("summary", "Die Zusammenfassung der bisherigen Konversation"));
        return cmd;
    }
}
