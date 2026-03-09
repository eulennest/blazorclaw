using BlazorClaw.Core.Models;
using BlazorClaw.Core.Sessions;
using System.CommandLine;

namespace BlazorClaw.Core.Commands;

public interface ICommandProvider
{
    IEnumerable<ISystemCommand> GetCommands();
    Task<object?> ExecuteAsync(ISystemCommand cmd, ParseResult result, MessageContext context);
}

public interface ISystemCommand
{
    Command GetCommand();
}

public interface ISystemCommandExecutor
{
    Task<object?> ExecuteAsync(ParseResult result, MessageContext context);
}

public class MessageContext
{
    public ChatSession? Session { get; set; }
    public required IServiceProvider Provider { get; set; }
    public string? UserId { get; set; }
    public IChannelSession? Channel { get; set; }
}

public class MessageContextAccessor
{
    public void SetContext(MessageContext state)
    {
        Context = state;
    }
    public MessageContext? Context { get; private set; }
}


public abstract class ExecutorCommandProvider : ICommandProvider
{
    public Task<object?> ExecuteAsync(ISystemCommand cmd, ParseResult result, MessageContext context)
    {
        if (cmd is ISystemCommandExecutor executor)
        {
            return executor.ExecuteAsync(result, context);
        }
        throw new InvalidOperationException($"Command {cmd.GetCommand().Name} does not implement ISystemCommandExecutor");
    }

    public abstract IEnumerable<ISystemCommand> GetCommands();
}