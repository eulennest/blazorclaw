using BlazorClaw.Core.Services;
using BlazorClaw.Core.Utils;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;
using System.Net.Http.Json;
using System.Text.Json;

namespace BlazorClaw.Core.Speech
{
    public class OpenAiTtsProvider(PathHelper pathHelper, HttpClient httpClient, IConfiguration conf, ILogger<OpenAiTtsProvider> logger) : ITextToSpeechProvider, ISpeechToTextProvider
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
            var strm = new TempStream();
            logger.LogInformation("{StatusCode} - {MediaType}", response.StatusCode, response.Content.Headers.ContentType?.MediaType);
            using var rets = await response.Content.ReadAsStreamAsync();
            await rets.CopyToAsync(strm);
            strm.Seek(0, SeekOrigin.Begin);
            return Tuple.Create((Stream)strm, response.Content.Headers.ContentType?.MediaType ?? "audio/opus");
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



        public async Task<string?> SpeechToTextAsync(Stream audioStream, string contentType)
        {
            var apiKey = conf.GetValue<string>("TTS:OpenAI:ApiKey");
            if (string.IsNullOrEmpty(apiKey)) return null;

            try
            {
                using var httpRequest = new HttpRequestMessage(HttpMethod.Post, "https://api.openai.com/v1/audio/transcriptions");
                httpRequest.Headers.Authorization = new System.Net.Http.Headers.AuthenticationHeaderValue("Bearer", apiKey);

                var ext = Mimetype.GetExtensionFromMimeType(contentType);
                // MultipartFormDataContent für File-Upload
                using var content = new MultipartFormDataContent();
                content.Add(new StringContent("whisper-1"), "model");
                content.Add(new StringContent("de"), "language"); // Optional: Sprache (z.B. "de" für Deutsch)
                content.Add(new StreamContent(audioStream), $"file{ext}", contentType);

                httpRequest.Content = content;

                using var response = await httpClient.SendAsync(httpRequest);

                if (!response.IsSuccessStatusCode)
                {
                    var error = await response.Content.ReadAsStringAsync();
                    logger.LogError("OpenAI SST Error: {Error}", error);
                    throw new Exception($"OpenAI SST Error: {error}");
                }

                var responseContent = await response.Content.ReadAsStringAsync();
                var jsonResult = JsonDocument.Parse(responseContent);
                var transcript = jsonResult.RootElement.GetProperty("text").GetString();

                logger.LogInformation("Transcription successful: {Length} characters", transcript?.Length ?? 0);
                return transcript;
            }
            catch (Exception ex)
            {
                logger.LogError(ex, "SST Error");
                return null;
            }
        }

        public async Task<string?> SpeechToTextAsync(string data)
        {
            data = await pathHelper.SaveMediaFileAsync(data) ?? data;
            var strm = await pathHelper.GetMediaFileAsync(data);
            if (strm == null) return null;
            return await SpeechToTextAsync(strm.Item1, strm.Item2);
        }
    }
}
