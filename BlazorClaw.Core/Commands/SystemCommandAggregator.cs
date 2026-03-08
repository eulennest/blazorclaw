using System.CommandLine;

namespace BlazorClaw.Core.Commands;

public class SystemCommandAggregator(IEnumerable<ICommandProvider> providers) : ICommandProvider
{
    private Dictionary<ISystemCommand, ICommandProvider>? _commandMap;

    public IEnumerable<ISystemCommand> GetCommands()
    {
        if (_commandMap == null)
        {
            _commandMap = [];

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

    public Task<object?> ExecuteAsync(ISystemCommand command, ParseResult result, CommandContext context)
    {
        if (_commandMap?.TryGetValue(command, out var provider) ?? false)
        {
            return provider.ExecuteAsync(command, result, context);
        }
        throw new InvalidOperationException($"No provider found for command {result.CommandResult.Command.Name}");
    }
}