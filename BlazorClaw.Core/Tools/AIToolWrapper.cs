using BlazorClaw.Core.Commands;
using Microsoft.Extensions.AI;
using System.Text.Json;

namespace BlazorClaw.Core.Tools;

public class AIToolWrapper(ITool tool, MessageContext context) : AIFunction
{
    public override string Name => tool.Name;
    public override string Description => tool.Description;

    public override JsonElement JsonSchema => tool.GetSchema();

    protected override async ValueTask<object?> InvokeCoreAsync(AIFunctionArguments arguments, CancellationToken cancellationToken)
    {
        return await tool.ExecuteAsync(tool.BuildArguments(arguments.Context), context).ConfigureAwait(false);
    }
}
