using Microsoft.Extensions.Configuration;

namespace BlazorClaw.Channels.Services
{
    public class BotConfigs<T> : Dictionary<string, T> where T : BotEntry
    {
        public static string Section = $"Channels:{typeof(T).Name.Replace("BotEntry", "")}:Accounts";


        public void Save(IConfiguration configuration, string key, T obj)
        {
            foreach (var prop in obj.GetType().GetProperties())
            {
                if (!prop.CanWrite || !prop.CanRead) continue;
                if (!BotConfigs<T>.AllowedType(prop.PropertyType)) continue;
                configuration[$"{Section}:{key}:{prop.Name}"] = prop.GetValue(obj)?.ToString() ?? null;
            }
        }
        public void Delete(IConfiguration configuration, string key)
        {
            configuration[$"{Section}:{key}"] = null;
        }

        private static bool AllowedType(Type type)
        {
            return type.IsPrimitive || type == typeof(string) || type == typeof(decimal);
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
