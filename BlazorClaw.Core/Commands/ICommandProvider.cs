using BlazorClaw.Core.Models;
using System.CommandLine;
using System.Threading.Tasks;

namespace BlazorClaw.Core.Commands;

public interface ICommandProvider
{
    IEnumerable<ISystemCommand> GetCommands();
    Task<object?> ExecuteAsync(ISystemCommand cmd, ParseResult result, CommandContext context);
}

public interface ISystemCommand
{
    Command GetCommand();
}

public interface ISystemCommandExecutor
{
    Task<object?> ExecuteAsync(ParseResult result, CommandContext context);
}

public class CommandContext
{
    public ChatSession? Session { get; set; }
    public required IServiceProvider Provider { get; set; }
    public string? UserId { get; set; }
    public string ChannelProvider { get; set; } = string.Empty;
    public string ChannelId { get; set; } = string.Empty;
}

public abstract class ExecutorCommandProvider : ICommandProvider
{
    public Task<object?> ExecuteAsync(ISystemCommand cmd, ParseResult result, CommandContext context)
    {
        if (cmd is ISystemCommandExecutor executor)
        {
            return executor.ExecuteAsync(result, context);
        }
        throw new InvalidOperationException($"Command {cmd.GetCommand().Name} does not implement ISystemCommandExecutor");
    }

    public abstract IEnumerable<ISystemCommand> GetCommands();
}