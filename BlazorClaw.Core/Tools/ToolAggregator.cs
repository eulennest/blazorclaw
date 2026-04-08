namespace BlazorClaw.Core.Tools;

public class ToolAggregator(IEnumerable<IToolProvider> providers) : IToolProvider
{
    public async IAsyncEnumerable<ITool> GetAllToolsAsync()
    {
        foreach (var prov in providers)
        {
            await foreach (var item in prov.GetAllToolsAsync())
            {
                yield return item;
            }
        }
    }

    public ITool? GetTool(string name)
    {
        foreach (var prov in providers)
        {
            var tool = prov.GetTool(name);
            if (tool != null)
            {
                return tool;
            }
        }
        return null;
    }
}
