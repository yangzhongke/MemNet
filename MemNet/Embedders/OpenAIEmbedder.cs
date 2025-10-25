using System.Net.Http.Json;
using System.Text.Json.Serialization;
using MemNet.Abstractions;
using MemNet.Config;
using Microsoft.Extensions.Options;

namespace MemNet.Embedders;

/// <summary>
/// OpenAI 嵌入生成器实现（复刻 Mem0 embeddings/openai.py）
/// </summary>
public class OpenAIEmbedder : IEmbedder
{
    private readonly HttpClient _httpClient;
    private readonly EmbedderConfig _config;

    public OpenAIEmbedder(HttpClient httpClient, IOptions<MemoryConfig> config)
    {
        _httpClient = httpClient;
        _config = config.Value.Embedder;

        // 配置 HttpClient
        if (_httpClient.BaseAddress == null)
        {
            _httpClient.BaseAddress = new Uri(_config.Endpoint ?? "https://api.openai.com/v1/");
        }
        
        if (!_httpClient.DefaultRequestHeaders.Contains("Authorization"))
        {
            _httpClient.DefaultRequestHeaders.Add("Authorization", $"Bearer {_config.ApiKey}");
        }
    }

    public async Task<float[]> EmbedAsync(string text, CancellationToken ct = default)
    {
        var request = new
        {
            input = text,
            model = _config.Model
        };

        var response = await _httpClient.PostAsJsonAsync("embeddings", request, ct);
        response.EnsureSuccessStatusCode();

        var result = await response.Content.ReadFromJsonAsync<EmbeddingResponse>(ct);
        return result?.Data?[0].Embedding ?? Array.Empty<float>();
    }

    public async Task<List<float[]>> EmbedBatchAsync(List<string> texts, CancellationToken ct = default)
    {
        var request = new
        {
            input = texts,
            model = _config.Model
        };

        var response = await _httpClient.PostAsJsonAsync("embeddings", request, ct);
        response.EnsureSuccessStatusCode();

        var result = await response.Content.ReadFromJsonAsync<EmbeddingResponse>(ct);
        return result?.Data?.Select(d => d.Embedding).ToList() ?? new List<float[]>();
    }

    // 内部类用于 JSON 反序列化
    private class EmbeddingResponse
    {
        [JsonPropertyName("data")]
        public List<EmbeddingData> Data { get; set; } = new();
    }

    private class EmbeddingData
    {
        [JsonPropertyName("embedding")]
        public float[] Embedding { get; set; } = Array.Empty<float>();
    }
}

