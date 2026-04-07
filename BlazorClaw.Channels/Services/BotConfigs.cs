namespace BlazorClaw.Channels.Services
{
    public class BotConfigs<T> : Dictionary<string, T>
    {
        public static string Section = $"Channels:{typeof(T).Name.Replace("BotEntry", "")}:Accounts";
    }
}
