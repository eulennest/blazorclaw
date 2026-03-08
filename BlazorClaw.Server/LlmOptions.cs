public class LlmOptions
{
    public const string Section = "Llm";
    public string Model { get; set; } = "openrouter/google/gemini-3.1-flash-lite-preview";
    public double Temperature { get; set; } = 0.7;
    public int MaxTokens { get; set; } = 5000;
}