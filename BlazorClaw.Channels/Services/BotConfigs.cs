namespace BlazorClaw.Channels.Services
{
    public class BotConfigs<T> : Dictionary<string, T> where T : BotEntry
    {
        public static string Section = $"Channels:{typeof(T).Name.Replace("BotEntry", "")}:Accounts";
    }

    public abstract class BotEntry
    {
        public bool Enabled { get; set; } = false;
    }
}
