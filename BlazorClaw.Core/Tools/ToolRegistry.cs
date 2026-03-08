namespace BlazorClaw.Core.Tools;

public interface IToolRegistry
{
    IEnumerable<ITool> GetAllTools();
    ITool? GetTool(string name);
}

public class ToolRegistry : IToolRegistry
{
    private readonly Dictionary<string, ITool> _tools = new();

    public ToolRegistry(IEnumerable<ITool> tools)
    {
        foreach (var tool in tools)
        {
            _tools[tool.Name] = tool;
        }
    }

    public IEnumerable<ITool> GetAllTools() => _tools.Values;

    public ITool? GetTool(string name) => _tools.GetValueOrDefault(name);
}
