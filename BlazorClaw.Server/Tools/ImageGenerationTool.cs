using BlazorClaw.Core.Commands;
using BlazorClaw.Core.Tools;
using System.ComponentModel;
using System.ComponentModel.DataAnnotations;
using System.Text;
using System.Text.Json;
using System.Text.Json.Serialization;
using System.IO;

namespace BlazorClaw.Server.Tools;

public class ImageGenerationParams
{
    [Description("Detaillierte Beschreibung des zu generierenden Bildes. Sei spezifisch und beschreibe detalliert.")]
    [Required(ErrorMessage = "Prompt is required")]
    public string Prompt { get; set; } = string.Empty;

    [Description("Model: gemini-3.1-flash, gpt-5-image-mini, etc.")]
    public string Model { get; set; } = "gemini-3.1-flash";

    [Description("Image size: 1024x1024 (default), 1792x1024 (landscape), 1024x1792 (portrait)")]
    public string Size { get; set; } = "1024x1024";
}

public class ImageGenerationTool : BaseTool<ImageGenerationParams>
{
    private readonly HttpClient _httpClient;
    private readonly ILogger<ImageGenerationTool> _logger;
    private readonly IConfiguration _configuration;
    private readonly IWebHostEnvironment _env;
    private readonly IHttpContextAccessor _httpContextAccessor;
    private readonly JsonSerializerOptions _jsonOptions = new()
    {
        PropertyNameCaseInsensitive = true
    };

    private readonly Dictionary<string, string> _models = new()
    {
        ["gemini-3.1-flash"] = "google/gemini-3.1-flash-image-preview",
        ["gemini-3-pro"] = "google/gemini-3-pro-image-preview",
        ["gpt-5-image-mini"] = "openai/gpt-5-image-mini",
        ["gpt-5-image"] = "openai/gpt-5-image",
        ["gemini-2.5-flash"] = "google/gemini-2.5-flash-image"
    };

    public override string Name => "generate_image";
    public override string Description => "Generiert ein Bild aus einer Textbeschreibung mittels KI. Unterstützt mehrere Modelle.";

    public ImageGenerationTool(
        HttpClient httpClient, 
        IConfiguration configuration, 
        ILogger<ImageGenerationTool> logger,
        IWebHostEnvironment env,
        IHttpContextAccessor httpContextAccessor)
    {
        _httpClient = httpClient;
        _configuration = configuration;
        _logger = logger;
        _env = env;
        _httpContextAccessor = httpContextAccessor;

        var apiKey = _configuration["Llm:ApiKey"] ?? "";
        _httpClient.BaseAddress = new Uri("https://openrouter.ai/api/v1/");
        _httpClient.DefaultRequestHeaders.Add("Authorization", $"Bearer {apiKey}");
        _httpClient.DefaultRequestHeaders.Add("HTTP-Referer", "https://blazorclaw.eulencode.de");
        _httpClient.DefaultRequestHeaders.Add("X-Title", "BlazorClaw");
    }

    protected override async Task<string> ExecuteInternalAsync(ImageGenerationParams p, MessageContext context)
    {
        try
        {
            var prompt = p.Prompt;
            var modelKey = string.IsNullOrEmpty(p.Model) ? "gemini-3.1-flash" : p.Model;
            var size = p.Size ?? "1024x1024";

            if (string.IsNullOrWhiteSpace(prompt))
            {
                return "Error: No prompt provided for image generation";
            }

            if (!_models.TryGetValue(modelKey, out string? modelName))
            {
                return $"Error: Invalid model '{modelKey}'. Available: {string.Join(", ", _models.Keys)}";
            }

            _logger.LogInformation("Generating image with {Model}: {Prompt}", modelName, prompt);

            // Convert size to aspect_ratio (OpenRouter format)
            var aspectRatio = size switch
            {
                "1024x1024" => "1:1",
                "1792x1024" => "16:9",
                "1024x1792" => "9:16",
                _ => "1:1"
            };

            var request = new
            {
                model = modelName,
                messages = new[]
                {
                    new { role = "user", content = prompt }
                },
                modalities = new[] { "image", "text" },
                stream = false,
                image_config = new { aspect_ratio = aspectRatio }
            };

            var json = JsonSerializer.Serialize(request);
            var content = new StringContent(json, Encoding.UTF8, "application/json");

            var response = await _httpClient.PostAsync("chat/completions", content);
            var responseJson = await response.Content.ReadAsStringAsync();

            if (!response.IsSuccessStatusCode)
            {
                _logger.LogError("Image generation error: {StatusCode} - {Error}", response.StatusCode, responseJson);
                return $"Error generating image: {response.StatusCode}";
            }

            try
            {
                var responseData = JsonSerializer.Deserialize<OpenRouterImageResponse>(responseJson, _jsonOptions);

                if (responseData?.Choices?.Length > 0 && responseData.Choices[0].Message?.Images?.Length > 0)
                {
                    var imageUrl = responseData.Choices[0].Message!.Images![0].ImageUrl!.Url!;
                    if (imageUrl == null)
                    {
                        return "Error: Image URL is null";
                    }

                    // Create uploads directory if it doesn't exist
                    var uploadsDir = Path.Combine(_env.WebRootPath ?? "./wwwroot", "uploads");
                    if (!Directory.Exists(uploadsDir))
                    {
                        Directory.CreateDirectory(uploadsDir);
                    }

                    var fileExtension = "png";
                    var fileName = $"generated-{Guid.NewGuid()}.{fileExtension}";
                    var filePath = Path.Combine(uploadsDir, fileName);

                    // Check if the URL is a Base64 data URL
                    if (imageUrl.StartsWith("data:image/"))
                    {
                        var base64Data = imageUrl.Split(',')[1];
                        var imageBytes = Convert.FromBase64String(base64Data);
                        await File.WriteAllBytesAsync(filePath, imageBytes);
                    }
                    else
                    {
                        // Download external image
                        using var downloadClient = new HttpClient();
                        var imageBytes = await downloadClient.GetByteArrayAsync(imageUrl);
                        await File.WriteAllBytesAsync(filePath, imageBytes);
                    }

                    // Get the current request's scheme and host
                    var httpRequest = _httpContextAccessor.HttpContext?.Request;
                    var baseUrl = httpRequest != null
                        ? $"{httpRequest.Scheme}://{httpRequest.Host}"
                        : "https://blazorclaw.eulencode.de";
                    var internalUrl = $"{baseUrl}/uploads/{fileName}";

                    _logger.LogInformation("Image saved to {Path}", filePath);

                    return $"✅ Bild generiert mit {modelKey}!\n\n**Prompt:** {prompt}\n**Size:** {size}\n**URL:** {internalUrl}";
                }

                return "Error: No image in response";
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error deserializing image response");
                return $"Error deserializing response: {ex.Message}";
            }
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error generating image");
            return $"Error generating image: {ex.Message}";
        }
    }

    private class OpenRouterImageResponse
    {
        [JsonPropertyName("choices")]
        public Choice[]? Choices { get; set; }
    }

    private class Choice
    {
        [JsonPropertyName("message")]
        public Message? Message { get; set; }
    }

    private class Message
    {
        [JsonPropertyName("images")]
        public Image[]? Images { get; set; }
    }

    private class Image
    {
        [JsonPropertyName("image_url")]
        public ImageUrl? ImageUrl { get; set; }
    }

    private class ImageUrl
    {
        [JsonPropertyName("url")]
        public string? Url { get; set; }
    }
}