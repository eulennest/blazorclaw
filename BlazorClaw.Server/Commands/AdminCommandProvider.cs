using System.CommandLine;
using BlazorClaw.Core.Commands;

namespace BlazorClaw.Server.Commands;

public class AdminCommandProvider : ICommandProvider
{
    public IEnumerable<ISystemCommand> GetCommands()
    {
        return new List<ISystemCommand> { new StatusCommand(), new RegisterCommand() };
    }
}

public class StatusCommand : ISystemCommand
{
    public Command GetCommand() => new("status", "Status von BlazorClaw anzeigen");
    public Task ExecuteAsync(ParseResult result, CommandContext context)
    {
        Console.WriteLine($"Status: BlazorClaw is running. User: {context.UserId}, Channel: {context.ChannelId}");
        return Task.CompletedTask;
    }
}

public class RegisterCommand : ISystemCommand
{
    public Command GetCommand() 
    {
        var cmd = new Command("register", "Registriert einen User");
        cmd.AddArgument(new Argument<string>("token", "Der Registrierungstoken"));
        return cmd;
    }
    
    public Task ExecuteAsync(ParseResult result, CommandContext context)
    {
        var token = result.GetValueForArgument(result.CommandResult.Command.Arguments[0]);
        Console.WriteLine($"Registrierung versucht mit Token: {token} für User: {context.UserId}");
        return Task.CompletedTask;
    }
}
