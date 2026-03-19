using System.CommandLine;
using System.Text;

namespace BlazorClaw.Core.Commands;

public class SystemCommandAggregator(IEnumerable<ICommandProvider> providers) : ICommandProvider
{
    private Dictionary<ISystemCommand, ICommandProvider>? _commandMap;

    public IEnumerable<ISystemCommand> GetCommands()
    {
        if (_commandMap == null)
        {
            _commandMap = [];
            _commandMap[new HelpCommand(_commandMap)] = this;

            foreach (var provider in providers)
            {
                foreach (var command in provider.GetCommands())
                {
                    _commandMap[command] = provider;
                }
            }
        }
        return _commandMap.Keys;
    }

    public Task<object?> ExecuteAsync(ISystemCommand command, ParseResult result, MessageContext context)
    {
        if (_commandMap?.TryGetValue(command, out var provider) ?? false)
        {
            if (provider == this)
            {
                return SelfExecuteAsync(command, result, context);
            }
            return provider.ExecuteAsync(command, result, context);
        }
        throw new InvalidOperationException($"No provider found for command {result.CommandResult.Command.Name}");
    }

    public Task<object?> SelfExecuteAsync(ISystemCommand cmd, ParseResult result, MessageContext context)
    {
        if (cmd is ISystemCommandExecutor executor)
        {
            return executor.ExecuteAsync(result, context);
        }
        throw new InvalidOperationException($"Command {cmd.GetCommand().Name} does not implement ISystemCommandExecutor");
    }
}

public class HelpCommand(Dictionary<ISystemCommand, ICommandProvider> commandMap) : ISystemCommand, ISystemCommandExecutor
{

    public Command GetCommand()
    {
        var cmd = new Command("help", "Zeigt alle möglichen Commands")
        {
        };
        return cmd;
    }

    public async Task<object?> ExecuteAsync(ParseResult result, MessageContext context)
    {
        var sb = new StringBuilder();
        sb.AppendLine("```");
        foreach (var item in commandMap)
        {
            var cmd = item.Key.GetCommand();
            var arglist = string.Join(' ', cmd.Arguments.Select(o => $"[{o.Name}]"));
            sb.AppendLine($"/{cmd.Name} {arglist}");
            sb.AppendLine($"{cmd.Description}");
            foreach (var arg in cmd.Arguments)
            {
                sb.AppendLine($" - {arg.Name} - {arg.Description}");
            }
            sb.AppendLine();
        }
        sb.AppendLine("```");

        return sb.ToString().Trim();
    }


}