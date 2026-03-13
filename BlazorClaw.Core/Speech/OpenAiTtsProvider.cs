using Microsoft.Extensions.Configuration;
using System.Net.Http.Json;

namespace BlazorClaw.Core.Speech
{
    public class OpenAiTtsProvider(HttpClient httpClient, IConfiguration conf) : ITextToSpeechProvider
    {
        public string Name => "OpenAI";
        public string Description => "OpenAI Text-to-Speech API";

        public async Task<Tuple<Stream, string>?> TextToSpeechAsync(string voiceName, string text, object options)
        {
            var apiKey = conf.GetValue<string>("TTS:OpenAI:ApiKey");
            if (string.IsNullOrEmpty(apiKey)) return null;
            var request = new
            {
                model = "tts-1", // Oder "tts-1-hd" für bessere Qualität
                input = text,
                voice = voiceName.ToLower(), // "alloy", "echo", "fable", "onyx", "nova", "shimmer"
                response_format = "opus"
            };

            using var httpRequest = new HttpRequestMessage(HttpMethod.Post, "https://api.openai.com/v1/audio/speech");
            httpRequest.Headers.Authorization = new System.Net.Http.Headers.AuthenticationHeaderValue("Bearer", apiKey);
            httpRequest.Content = JsonContent.Create(request);

            using var response = await httpClient.SendAsync(httpRequest);

            if (!response.IsSuccessStatusCode)
            {
                var error = await response.Content.ReadAsStringAsync();
                throw new Exception($"OpenAI TTS Error: {error}");
            }
            var strm = new FileStream(Path.GetTempFileName(), new FileStreamOptions() { 
                Mode = FileMode.Create,
                Access = FileAccess.ReadWrite,
                Options = FileOptions.DeleteOnClose });

            using var rets = await response.Content.ReadAsStreamAsync();
            await rets.CopyToAsync(strm);
            strm.Seek(0, SeekOrigin.Begin);
            return Tuple.Create((Stream)strm, response.Content.Headers.ContentType?.MediaType ?? "audio/ogg");
        }

        public async IAsyncEnumerable<ITextToSpeechVoice> ListVoicesAsync()
        {
            // OpenAI hat feste Stimmen, diese Liste ist statisch

            yield return new TextToSpeechVoice("alloy", "Neutral and balanced");
            yield return new TextToSpeechVoice("echo", "Natural and clear");
            yield return new TextToSpeechVoice("fable", "Friendly and bright");
            yield return new TextToSpeechVoice("onyx", "Deep and professional");
            yield return new TextToSpeechVoice("nova", "Energetic and warm");
            yield return new TextToSpeechVoice("shimmer", "Sophisticated and smooth");
        }
    }

}
