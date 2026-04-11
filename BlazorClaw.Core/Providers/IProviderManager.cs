using Microsoft.Extensions.AI;

namespace BlazorClaw.Core.Providers
{
    public interface IProviderConfiguration
    {
        string Uri { get; }
        string? Token { get; }
    }

    public interface IProviderManager
    {
        IEnumerable<ProviderInfo> GetProviders();
        IAsyncEnumerable<string> GetModelsAsync(string provider);
        IAsyncEnumerable<string> GetModelsAsync();
        Task<bool> SetProviderAsync(string provider, IProviderConfiguration config);
        ValueTask<IChatClient> GetChatClientAsync(string model, CancellationToken ct = default);
        ValueTask<IEmbeddingGenerator<string, Embedding<float>>> GetEmbeddingClientAsync(string model, CancellationToken ct = default);
    }

    public sealed record ProviderInfo(string Id, FeatureSupport Features)
    {
        public  bool Supports(FeatureSupport feature) => (Features & feature) == feature;
        public override string ToString() => Id;
    }

    [Flags]
    public enum FeatureSupport
    {
        None = 0,
        Chat = 1,
        Embeddings = 2,
        Vision = 4,
        ImageGeneration = 8
    }

    public class ProviderConfiguration : IProviderConfiguration
    {
        public required string Uri { get; set; }
        public string? Token { get; set; }
    }
}
