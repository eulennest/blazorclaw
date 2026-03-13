using BlazorClaw.Core.Commands;
using BlazorClaw.Core.Sessions;
using BlazorClaw.Core.Tools;
using System.ComponentModel;
using System.ComponentModel.DataAnnotations;

namespace BlazorClaw.Server.Tools.Session;

public class SessionRenameTool : BaseTool<SessionRenameParams>
{
    public override string Name => "session_rename";
    public override string Description => "Benennt die aktuelle Session um.";

    protected override async Task<string> ExecuteInternalAsync(SessionRenameParams p, MessageContext context)
    {
        var sessionManager = context.Provider.GetRequiredService<ISessionManager>();
        var session = await sessionManager.GetSessionAsync(context.Session!.Id);
        
        if (session == null)
        {
            return "Session nicht gefunden.";
        }

        session.Session.Title = p.Name;
        await sessionManager.SaveSessionAsync(session, true);

        return $"✅ Session umbenannt: \"{p.Name}\"";
    }
}

public class SessionRenameParams
{
    [Description("Der neue Name für die Session")]
    [Required(ErrorMessage = "Name is required")]
    public string Name { get; set; } = string.Empty;
}