using System.CommandLine;
using System.Threading.Tasks;

namespace BlazorClaw.Core.Commands;

public interface ISystemCommand
{
    Command GetCommand();
    Task ExecuteAsync(ParseResult result, CommandContext context);
}

public interface ICommandProvider
{
    IEnumerable<ISystemCommand> GetCommands();
}
