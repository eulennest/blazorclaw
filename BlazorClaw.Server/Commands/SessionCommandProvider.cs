using BlazorClaw.Core.Commands;
using BlazorClaw.Core.Models;
using BlazorClaw.Core.Sessions;
using System.CommandLine;

namespace BlazorClaw.Server.Commands;

public class SessionCommandProvider : ExecutorCommandProvider
{
    public override IEnumerable<ISystemCommand> GetCommands()
    {
        yield return new SessionRenameCommand();
    }
}

public class SessionRenameCommand : ISystemCommand, ISystemCommandExecutor
{
    public Command GetCommand()
    {
        var cmd = new Command("rename", "Benennt die aktuelle Session um")
        {
            new Argument<string>("name") { Description = "Neuer Name für die Session" }
        };
        return cmd;
    }

    public async Task<object?> ExecuteAsync(ParseResult result, MessageContext context)
    {
        var name = result.GetValue<string>((Argument<string>)result.CommandResult.Command.Arguments[0]);
        if (string.IsNullOrWhiteSpace(name))
        {
            return "Bitte einen Namen angeben: /rename <name>";
        }

        var sessionManager = context.Provider.GetRequiredService<ISessionManager>();
        var session = await sessionManager.GetSessionAsync(context.Session!.Id);
        
        if (session == null)
        {
            return "Session nicht gefunden.";
        }

        session.Session.Title = name;
        await sessionManager.SaveSessionAsync(session, true);

        return $"✅ Session umbenannt: \"{name}\"";
    }
}