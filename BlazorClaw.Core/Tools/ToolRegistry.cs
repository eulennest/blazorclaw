using BlazorClaw.Core.Tools;
using System.Reflection;

namespace BlazorClaw.Core.Tools;

public interface IToolRegistry
{
    void RegisterFromAssembly(Assembly assembly);
    IEnumerable<ITool> GetAllTools();
    ITool? GetTool(string name);
}

public class ToolRegistry : IToolRegistry
{
    private readonly Dictionary<string, ITool> _tools = new();

    public void RegisterFromAssembly(Assembly assembly)
    {
        var toolTypes = assembly.GetTypes()
            .Where(t => typeof(ITool).IsAssignableFrom(t) && !t.IsInterface && !t.IsAbstract);

        foreach (var type in toolTypes)
        {
            if (Activator.CreateInstance(type) is ITool tool)
            {
                _tools[tool.Name] = tool;
            }
        }
    }

    public IEnumerable<ITool> GetAllTools() => _tools.Values;

    public ITool? GetTool(string name) => _tools.GetValueOrDefault(name);
}
