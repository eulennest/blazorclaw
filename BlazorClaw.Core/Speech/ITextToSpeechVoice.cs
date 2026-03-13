namespace BlazorClaw.Core.Speech
{
    public interface ITextToSpeechVoice
    {
        string VoiceName { get; }
        string Description { get; }
    }
    public class TextToSpeechVoice(string name, string desc) : ITextToSpeechVoice
    {
        public virtual string VoiceName { get; protected set; } = name;

        public virtual string Description { get; protected set; } = desc;
    }
}