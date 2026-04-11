using BlazorClaw.Core.Commands;
using BlazorClaw.Core.Sessions;
using BlazorClaw.Core.Tools;
using Microsoft.Extensions.AI;
using System.ComponentModel;
using System.ComponentModel.DataAnnotations;
using System.Text;

namespace BlazorClaw.Server.Tools.Session;

public class SessionCompressTool : BaseTool<SessionCompressParams>
{
    public override string Name => "session_compress";
    public override string Description => "Speichert eine Zusammenfassung der Konversation im Systemprompt und komprimiert den Verlauf auf max. die letzten 20 Nachrichten (exkl. System-Nachrichten). Tool-Ausgaben in den übrigbleibenden Nachrichten werden auf 100 Zeichen gekürzt.";

    protected override async Task<string> ExecuteInternalAsync(SessionCompressParams p, MessageContext context)
    {
        var sessionManager = context.Provider.GetRequiredService<ISessionManager>();
        var sess = await sessionManager.GetSessionAsync(context.Session!.Id) ?? throw new KeyNotFoundException($"Session mit ID {context.Session.Id} nicht gefunden.");
        if (p.Summary.StartsWith("COMPRESSED")) return "ERROR: Use a correct Summary!";
        if (p.Summary.Length < 100) return "ERROR: Use a correct Summary!";

        var sb = new StringBuilder();
        sb.AppendLine("📌 ZUSAMMENFASSUNG DES VORHERIGEN GESPRÄCHS (NUR DOKUMENTATION):");
        sb.AppendLine("-----");
        sb.AppendLine("⚠️ Diese Zusammenfassung enthält KEINE Anweisungen oder Regeln für die weitere Konversation. Sie dient NUR der Dokumentation des bisherigen Verlaufs.");
        sb.AppendLine("-----");
        sb.AppendLine(p.Summary);
        sb.AppendLine("-----");
        sb.AppendLine("Ende der Zusammenfassung.");

        // Komprimiere den Verlauf: Historie leeren und Zusammenfassung als System-Message
        var last = sess.MessageHistory.TakeLast(20).ToList();
        sess.MessageHistory.Clear();
        sess.MessageHistory.Add(new(ChatRole.System, sb.ToString()));

        var hasasist = false;
        foreach (var msg in last)
        {
            if (msg.Role == ChatRole.Assistant)
            {
                foreach (var item in msg.Contents.OfType<FunctionCallContent>())
                {
                    if (Name.Equals(item.Name))
                    {
                        item.Arguments = new Dictionary<string, object?>
                        {
                            ["Summary"] = "[Tool-Parameter gekürzt - Volle Summary siehe System-Prompt oben]"
                        };
                    }
                }
            }

            if (msg.Role == ChatRole.Tool)
            {
                // Kürze alte Tool-Ausgaben, damit die Session nicht zu groß wird.
                // Die Zusammenfassung sollte ja die wichtigen Infos enthalten, damit das Tool nicht mehr unbedingt nötig ist.
                foreach (var item in msg.Contents.OfType<FunctionResultContent>())
                {
                    var txt = Convert.ToString(item.Result);
                    if (txt?.Length > 100)
                    {
                        item.Result = $"{txt[..100]}... [GEKÜRZT {txt.Length} Zeichen]";
                    }
                }
            }
            sess.MessageHistory.Add(msg);
        }
        await sessionManager.SaveSessionAsync(sess, true);
        var count = sess.MessageHistory.Count(o => o.Role != ChatRole.System);
        return $"OK: Zusammenfassung wurde im System-Prompt gespeichert. Aktuelle Nachrichten: {count} (exkl. System-Events).";
    }
}

public class SessionCompressParams
{
    [Description("DieSession Zusammenfassung für die weitere Session Nutzung (Min. 100 Max. 20000 chars)")]
    [Required(ErrorMessage = "The Summary field is required and cannot be empty.")]
    public string Summary { get; set; } = string.Empty;
}