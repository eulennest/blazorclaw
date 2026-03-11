using BlazorClaw.Core.Commands;
using BlazorClaw.Core.Sessions;
using BlazorClaw.Core.Tools;
using System.ComponentModel;
using System.ComponentModel.DataAnnotations;

namespace BlazorClaw.Server.Tools.Session;

public class SessionCompressTool : BaseTool<SessionCompressParams>
{
    public override string Name => "session_compress";
    public override string Description => "Speichert eine Zusammenfassung der Konversation und komprimiert den Verlauf.";

    protected override async Task<string> ExecuteInternalAsync(SessionCompressParams p, MessageContext context)
    {
        var sessionManager = context.Provider.GetRequiredService<ISessionManager>();
        var sess = await sessionManager.GetSessionAsync(context.Session!.Id) ?? throw new KeyNotFoundException($"Session mit ID {context.Session.Id} nicht gefunden.");
        if ("COMRESSED".Equals(p.Summary)) return "COMPRESSED IGNORED";

        // Komprimiere den Verlauf: Historie leeren und Zusammenfassung als System-Message
        var last = sess.MessageHistory.TakeLast(20).ToList();
        sess.MessageHistory.Clear();
        sess.MessageHistory.Add(new() { Role = "system", Content = $"Zusammenfassung des vorherigen Gesprächs:\r\n-----\r\n{p.Summary}" });

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
        return $"Session komprimiert. Aktuelle Nachrichten: {sess.MessageHistory.Count}";
    }
}

public class SessionCompressParams
{
    [Description("Die Session Zusammenfassung für die weitere Session Nutzung (Max. 20000 chars)")]
    [Required(ErrorMessage = "The Summary field is required and cannot be empty.")]
    public string Summary { get; set; } = string.Empty;
}