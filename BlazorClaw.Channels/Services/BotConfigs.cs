using BlazorClaw.Core.Utils;
using Microsoft.Extensions.Configuration;
using System.Reflection;

namespace BlazorClaw.Channels.Services
{
    public class BotConfigs<T> : Dictionary<string, T> where T : BotEntry
    {
        public static string Section = $"Channels:{typeof(T).Name.Replace("BotEntry", "")}:Accounts";


        public async Task SaveAsync(IConfigurationRoot configuration, string key, T obj)
        {
            var prov = configuration.Providers.FirstOrDefault(o => o is SaveableJsonConfigurationProvider) ?? throw new ArgumentNullException(nameof(configuration));
            foreach (var prop in obj.GetType().GetProperties())
            {
                if (!prop.CanWrite || !prop.CanRead) continue;
                if (!BotConfigs<T>.AllowedType(prop.PropertyType)) continue;
                prov.Set($"{Section}:{key}:{prop.Name}", prop.GetValue(obj)?.ToString() ?? null);
            }
            await WaitForSaveAsync().ConfigureAwait(false);
        }
        public async Task DeleteAsync(IConfigurationRoot configuration, string key)
        {
            var prov = configuration.Providers.FirstOrDefault(o => o is SaveableJsonConfigurationProvider) ?? throw new ArgumentNullException(nameof(configuration));

            foreach (var prop in typeof(T).GetProperties())
            {
                if (!prop.CanWrite || !prop.CanRead) continue;
                if (!BotConfigs<T>.AllowedType(prop.PropertyType)) continue;
                prov.Set($"{Section}:{key}:{prop.Name}", null);
            }
            prov.Set($"{Section}:{key}", null);
            Remove(key);
            await WaitForSaveAsync().ConfigureAwait(false);
            RemoveSection(configuration, $"{Section}:{key}");
        }

        private Task WaitForSaveAsync()
        {
            return Task.Delay(500);
        }

        private static bool AllowedType(Type type)
        {
            return type.IsPrimitive || type == typeof(string) || type == typeof(decimal);
        }

        private void RemoveSection(IConfigurationRoot root, string key)
        {
            var providers = root.Providers.ToList();

            foreach (var provider in providers)
            {
                var dataProperty = (provider as ConfigurationProvider)?.GetType().GetProperty("Data", BindingFlags.Instance | BindingFlags.NonPublic);

                if (dataProperty?.GetValue(provider) is Dictionary<string, string?> data)
                {
                    data.Keys.Where(k => k == key || k.StartsWith(key + ConfigurationPath.KeyDelimiter)).ToList()
                         .ForEach(k => data.Remove(k));
                }
            }
        }
    }

    public abstract class BotEntry : ICloneable
    {
        public bool Enabled { get; set; } = false;

        public object Clone()
        {
            return this.MemberwiseClone();
        }
    }
}
