using BlazorClaw.Core.Commands;
using BlazorClaw.Core.Data;
using BlazorClaw.Core.DTOs;
using BlazorClaw.Core.Providers;
using BlazorClaw.Core.Services;
using BlazorClaw.Core.Tools;
using Microsoft.EntityFrameworkCore;
using System.ComponentModel;
using System.ComponentModel.DataAnnotations;
using System.Text;
using System.Text.Json;

namespace BlazorClaw.Server.Tools;

public class ImageGenerationParams
{
    [Description("Detaillierte Beschreibung des zu generierenden Bildes. Sei spezifisch und beschreibe detalliert.")]
    [Required(ErrorMessage = "Prompt is required")]
    public string Prompt { get; set; } = string.Empty;

    [Description("Model: gemini-3.1-flash, gpt-5-image-mini, etc.")]
    public string? Model { get; set; } = "gemini-3.1-flash";

    [Description("Image size: 1024x1024 (default), 1792x1024 (landscape), 1024x1792 (portrait)")]
    public string? Size { get; set; } = "1024x1024";
}

public class ImageGenerationTool(
    IConfiguration configuration,
    ILogger<ImageGenerationTool> logger,
    PathHelper env,
    IProviderManager providerManager) : BaseTool<ImageGenerationParams>
{
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

    protected override async Task<string> ExecuteInternalAsync(ImageGenerationParams p, MessageContext context)
    {
        var db = context.Provider.GetRequiredService<ApplicationDbContext>();
        var key = await db.ApiKeys.FirstOrDefaultAsync(o => o.UserId == null && o.Value.StartsWith("sk-or-v1-"));
        if (key == null) throw new Exception("No OpenRouter Key found!");

        using var httpClient = context.Provider.GetRequiredService<HttpClient>();
        httpClient.BaseAddress = new Uri("https://openrouter.ai/api/v1/");
        httpClient.DefaultRequestHeaders.Authorization = new("Bearer", key.Value);

        var prompt = p.Prompt;
        var modelKey = string.IsNullOrEmpty(p.Model) ? _models.Keys.First() : p.Model;
        var size = p.Size ?? "1024x1024";

        if (string.IsNullOrWhiteSpace(prompt))
        {
            return "Error: No prompt provided for image generation";
        }

        if (!_models.TryGetValue(modelKey, out string? modelName))
        {
            return $"Error: Invalid model '{modelKey}'. Available: {string.Join(", ", _models.Keys)}";
        }

        logger.LogInformation("Generating image with {Model}: {Prompt}", modelName, prompt);

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

        using var response = await httpClient.PostAsJsonAsync("chat/completions", request);
        var responseJson = await response.Content.ReadAsStringAsync();

        if (!response.IsSuccessStatusCode)
        {
            logger.LogError("Image generation error: {StatusCode} - {Error}", response.StatusCode, responseJson);
            throw new Exception($"{responseJson} (response.StatusCode)");
        }

        var responseData = JsonSerializer.Deserialize<ChatCompletionResponse>(responseJson, _jsonOptions);

        if (responseData?.Choices?.Count > 0 && responseData.Choices[0].Message?.Images?.Count > 0)
        {
            var imageUrl = responseData.Choices[0].Message!.Images![0].ImageUrl?.Url ??
                throw new Exception("Error: No Image URL in response");

            var mediaFile = await env.SaveMediaFileAsync(imageUrl);
            if (string.IsNullOrWhiteSpace(mediaFile))
                throw new Exception("Error: Saving Image failed");
            logger.LogInformation("Image saved to {Path}", mediaFile);

            var mediaUri = env.GetMediaUrl(mediaFile);
            var sb = new StringBuilder();
            sb.AppendLine($"✅ Bild generiert mit {modelKey}");
            sb.AppendLine($"**URL:** {mediaUri}");
            sb.AppendLine($"**Prompt:** {prompt}");
            sb.AppendLine($"**Size:** {size}");
            sb.AppendLine($"INFO: You can embed the image in markdown for webchat.");
            sb.AppendLine($"Or you can use [IMAGE:{mediaUri}] in start of your message");
            return sb.ToString();
        }

        throw new Exception("Error: No Image in response");
    }
}
