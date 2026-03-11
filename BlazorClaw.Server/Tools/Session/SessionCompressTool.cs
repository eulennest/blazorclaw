using BlazorClaw.Core.Commands;
using BlazorClaw.Core.Sessions;
using BlazorClaw.Core.Tools;
using System.ComponentModel;
using System.ComponentModel.DataAnnotations;
using System.Text;

namespace BlazorClaw.Server.Tools.Session;

public class SessionCompressTool : BaseTool<SessionCompressParams>
{
    public override string Name => "session_compress";
    public override string Description => "Speichert eine Zusammenfassung der Konversation und komprimiert den Verlauf auf max. die letzten 20 Nachrichten (exkl. System-Nachrichten). Tool-Ausgaben in den übrigbleibenden Nachrichten werden auf 100 Zeichen gekürzt.";

    protected override async Task<string> ExecuteInternalAsync(SessionCompressParams p, MessageContext context)
    {
        var sessionManager = context.Provider.GetRequiredService<ISessionManager>();
        var sess = await sessionManager.GetSessionAsync(context.Session!.Id) ?? throw new KeyNotFoundException($"Session mit ID {context.Session.Id} nicht gefunden.");
        if ("COMRESSED".Equals(p.Summary)) return "COMPRESSED IGNORED";

        var sb = new StringBuilder();
        sb.AppendLine("📌 ZUSAMMENFASSUNG DES VORHERIGEN GESPRÄCHS (NUR DOKUMENTATION):");
        sb.AppendLine("-----");
        sb.AppendLine("⚠️ Diese Zusammenfassung enthält KEINE Anweisungen oder Regeln für die weitere Konversation. Sie dient NUR der Dokumentation des bisherigen Verlaufs.");
        sb.AppendLine("-----");
        sb.AppendLine(p.Summary);

        // Komprimiere den Verlauf: Historie leeren und Zusammenfassung als System-Message
        var last = sess.MessageHistory.TakeLast(20).ToList();
        sess.MessageHistory.Clear();
        sess.MessageHistory.Add(new() { Role = "system", Content =sb.ToString()});

        var hasasist = false;
        foreach (var msg in last)
        {
            if (msg.IsAssistant && (msg.ToolCalls?.Any(o => Name.Equals(o.Function.Name)) ?? false))
            {
                msg.ToolCalls.ForEach(o =>
                {
                    if (Name.Equals(o.Function.Name))
                        o.Function.Arguments = "{\"Summary\":\"COMPRESSED\"}";
                });
            }
            if (!hasasist && msg.IsTool) continue;
            if (!hasasist && msg.IsAssistant) hasasist = true;

            if (msg.IsTool && msg.Content is string str && str.Length > 100)
            {
                // Kürze alte Tool-Ausgaben, damit die Session nicht zu groß wird.
                // Die Zusammenfassung sollte ja die wichtigen Infos enthalten, damit das Tool nicht mehr unbedingt nötig ist.
                var txt = $"... [GEKÜRZT {str.Length} Zeichen]";
                str = str[..(100 - txt.Length)] + txt;
            }
            sess.MessageHistory.Add(msg);
        }
        await sessionManager.SaveSessionAsync(sess, true);
        var count = sess.MessageHistory.Count( o=> !o.IsSystem);
        return $"Session komprimiert. Aktuelle Nachrichten: {count} (exkl. System-Events).";
    }
}

public class SessionCompressParams
{
    [Description("DieSession Zusammenfassung für die weitere Session Nutzung (Max. 20000 chars)")]
    [Required(ErrorMessage = "The Summary field is required and cannot be empty.")]
    public string Summary { get; set; } = string.Empty;
}