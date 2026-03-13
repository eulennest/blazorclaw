namespace BlazorClaw.Core.Speech
{
    public interface ITextToSpeechProvider
    {
        string Name { get; }
        string Description { get; }

        Task<Tuple<Stream, string>?> TextToSpeechAsync(string voiceName, string text, object options);
        IAsyncEnumerable<ITextToSpeechVoice> ListVoicesAsync();
    }
    public interface ISpeechToTextProvider
    {
        string Name { get; }
        string Description { get; }

        Task<string?> SpeechToTextAsync(string data);
    }

}
