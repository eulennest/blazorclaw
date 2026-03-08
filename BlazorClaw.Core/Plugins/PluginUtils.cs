using Microsoft.Extensions.DependencyInjection;

namespace BlazorClaw.Core.Plugins
{
    public class PluginUtils
    {

        public static IEnumerable<Type> GetPluginTypes(Type pluginInterfaceType, params Type[] ignoredTypes)
        {
            var pluginTypes = AppDomain.CurrentDomain.GetAssemblies()
                .SelectMany(assembly => assembly.GetTypes())
                .Where(type => !ignoredTypes.Contains(type))
                .Where(type => pluginInterfaceType.IsAssignableFrom(type) && !type.IsInterface && !type.IsAbstract);
            return pluginTypes;
        }

        public static IEnumerable<T> BuildPlugins<T>(IServiceProvider serviceProvider, params Type[] ignoredTypes) where T : class
        {
            var pluginTypes = GetPluginTypes(typeof(T), ignoredTypes);
            foreach (var type in pluginTypes)
            {
                T? plugin = null;
                try
                {
                    plugin = ActivatorUtilities.CreateInstance(serviceProvider, type) as T;
                }
                catch (Exception) { }
                if (plugin != null)
                {
                    yield return plugin;
                }
            }
        }
    }
}
