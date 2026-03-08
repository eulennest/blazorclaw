using System.Threading.Tasks;
using System.CommandLine;
using BlazorClaw.Core.Commands;

namespace BlazorClaw.Core.Commands;

public class CommandContext
{
    public string UserId { get; set; } = string.Empty;
    public string ChannelId { get; set; } = string.Empty;
}

public interface ISystemCommandAggregator
{
    Task ExecuteAsync(string input, CommandContext context);
}

public class SystemCommandAggregator : ISystemCommandAggregator
{
    private readonly RootCommand _rootCommand;
    private readonly Dictionary<string, ISystemCommand> _commandMap = new();

    public SystemCommandAggregator(IEnumerable<ICommandProvider> providers)
    {
        _rootCommand = new RootCommand("BlazorClaw System Commands");
        foreach (var provider in providers)
        {
            foreach (var command in provider.GetCommands())
            {
                _rootCommand.AddCommand(command.GetCommand());
                _commandMap[command.GetCommand().Name] = command;
            }
        }
    }

    public async Task ExecuteAsync(string input, CommandContext context)
    {
        var result = _rootCommand.Parse(input);
        
        var commandName = result.CommandResult.Command.Name;
        if (_commandMap.TryGetValue(commandName, out var command))
        {
            await command.ExecuteAsync(result, context);
        }
    }
}
