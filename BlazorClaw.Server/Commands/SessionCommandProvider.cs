using BlazorClaw.Core.Commands;
using BlazorClaw.Core.Sessions;
using System.CommandLine;

namespace BlazorClaw.Server.Commands;

public class SessionCommandProvider : ExecutorCommandProvider
{
    public override IEnumerable<ISystemCommand> GetCommands()
    {
        yield return new SessionRenameCommand();
        yield return new SessionResetCommand();
    }
}


public class SessionResetCommand : ISystemCommand, ISystemCommandExecutor
{
    public Command GetCommand()
    {
        var cmd = new Command("reset", "Löscht gesamte Session History")
        {
            //            new Argument<bool?>("disableLlmMessage") { Description = "True = Deaktiviert automatische Message ans LLm für neue Begrüßung" }
        };
        return cmd;
    }

    public async Task<object?> ExecuteAsync(ParseResult result, MessageContext context)
    {
        bool disableMsg = false;
        if (result.CommandResult.Command.Arguments.Count > 0)
        {
            disableMsg = result.GetValue((Argument<bool?>)result.CommandResult.Command.Arguments[0]) ?? false;
        }

        var sessionManager = context.Provider.GetRequiredService<ISessionManager>();
        var session = await sessionManager.GetSessionAsync(context.Session!.Id);
        session?.MessageHistory.Clear();
        return $"Session cleared.";
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