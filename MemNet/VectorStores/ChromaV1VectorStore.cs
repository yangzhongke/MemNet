using System;
using System.Collections.Generic;
using System.Linq;
using System.Net.Http;
using System.Net.Http.Json;
using System.Text.Json.Serialization;
using System.Threading;
using System.Threading.Tasks;
using MemNet.Abstractions;
using MemNet.Config;
using MemNet.Internals;
using MemNet.Models;
using Microsoft.Extensions.Options;

namespace MemNet.VectorStores;

/// <summary>
/// Chroma vector store implementation
/// </summary>
public class ChromaV1VectorStore : IVectorStore
{
    private readonly HttpClient _httpClient;
    private readonly string _collectionName;

    public ChromaV1VectorStore(HttpClient httpClient, IOptions<MemoryConfig> config)
    {
        _httpClient = httpClient;
        var configVectorStore = config.Value.VectorStore;
        _collectionName = configVectorStore.CollectionName;

        // Configure HttpClient
        if (_httpClient.BaseAddress == null)
        {
            _httpClient.BaseAddress = new Uri(configVectorStore.Endpoint);
        }

        if (!string.IsNullOrEmpty(configVectorStore.ApiKey))
        {
            _httpClient.DefaultRequestHeaders.Add("Authorization", $"Bearer {configVectorStore.ApiKey}");
        }
    }

    public async Task EnsureCollectionExistsAsync(int vectorSize, bool allowRecreation = false, CancellationToken ct = default)
    {
        // Try to get collection
        var response = await _httpClient.GetAsync($"/api/v1/collections/{_collectionName}", ct);
            
        if (response.IsSuccessStatusCode)
        {
            // Collection exists: Chroma doesn't require pre-defined vector size, it adapts automatically;
            //therefore, we don't need to recreate it even if vector size differs.
            return;
        }

        // Collection doesn't exist, create it
        await CreateCollectionAsync(ct);
    }

    private async Task CreateCollectionAsync(CancellationToken ct)
    {
        var createRequest = new
        {
            name = _collectionName,
            metadata = new { description = "MemNet memory collection" }
        };

        var response = await _httpClient.PostAsJsonAsync("/api/v1/collections", createRequest, ct);
        await response.EnsureSuccessWithContentAsync();
    }

    public async Task InsertAsync(List<MemoryItem> memories, CancellationToken ct = default)
    {
        var request = new
        {
            ids = memories.Select(m => m.Id).ToList(),
            embeddings = memories.Select(m => m.Embedding).ToList(),
            documents = memories.Select(m => m.Data).ToList(),
            metadatas = memories.Select(m => new Dictionary<string, object>
            {
                ["userId"] = m.UserId ?? string.Empty,
                ["agentId"] = m.AgentId ?? string.Empty,
                ["runId"] = m.RunId ?? string.Empty,
                ["metadata"] = System.Text.Json.JsonSerializer.Serialize(m.Metadata),
                ["createdAt"] = m.CreatedAt.ToString("o"),
                ["updatedAt"] = m.UpdatedAt?.ToString("o") ?? string.Empty,
                ["hash"] = m.Hash ?? string.Empty
            }).ToList()
        };

        var response = await _httpClient.PostAsJsonAsync(
            $"/api/v1/collections/{_collectionName}/add",
            request,
            ct);

        await response.EnsureSuccessWithContentAsync();
    }

    public async Task UpdateAsync(List<MemoryItem> memories, CancellationToken ct = default)
    {
        var request = new
        {
            ids = memories.Select(m => m.Id).ToList(),
            embeddings = memories.Select(m => m.Embedding).ToList(),
            documents = memories.Select(m => m.Data).ToList(),
            metadatas = memories.Select(m => new Dictionary<string, object>
            {
                ["userId"] = m.UserId ?? string.Empty,
                ["agentId"] = m.AgentId ?? string.Empty,
                ["runId"] = m.RunId ?? string.Empty,
                ["metadata"] = System.Text.Json.JsonSerializer.Serialize(m.Metadata),
                ["createdAt"] = m.CreatedAt.ToString("o"),
                ["updatedAt"] = m.UpdatedAt?.ToString("o") ?? string.Empty,
                ["hash"] = m.Hash ?? string.Empty
            }).ToList()
        };

        var response = await _httpClient.PostAsJsonAsync(
            $"/api/v1/collections/{_collectionName}/update",
            request,
            ct);

        await response.EnsureSuccessWithContentAsync();
    }

    public async Task<List<MemorySearchResult>> SearchAsync(float[] queryVector, string? userId = null, int limit = 100, CancellationToken ct = default)
    {
        var searchRequest = new
        {
            query_embeddings = new[] { queryVector },
            n_results = limit,
            where = userId != null ? new Dictionary<string, object>
            {
                ["userId"] = userId
            } : null,
            include = new[] { "documents", "metadatas", "distances" }
        };

        var response = await _httpClient.PostAsJsonAsync(
            $"/api/v1/collections/{_collectionName}/query",
            searchRequest,
            ct);

        await response.EnsureSuccessWithContentAsync();

        var result = await response.Content.ReadFromJsonAsync<ChromaQueryResponse>(ct);

        if (result?.Ids == null || result.Ids.Count == 0 || result.Ids[0].Count == 0)
        {
            return new List<MemorySearchResult>();
        }

        var results = new List<MemorySearchResult>();
        for (int i = 0; i < result.Ids[0].Count; i++)
        {
            var metadata = result.Metadatas?[0][i] ?? new Dictionary<string, object>();
            var metadataStr = metadata.TryGetValue("metadata", out var metaObj) ? metaObj?.ToString() : null;

            results.Add(new MemorySearchResult
            {
                Id = result.Ids[0][i],
                Memory = new MemoryItem
                {
                    Id = result.Ids[0][i],
                    Data = result.Documents?[0][i] ?? string.Empty,
                    Embedding = Array.Empty<float>(),
                    UserId = metadata.TryGetValue("userId", out var uid) ? uid?.ToString() : null,
                    AgentId = metadata.TryGetValue("agentId", out var aid) ? aid?.ToString() : null,
                    RunId = metadata.TryGetValue("runId", out var rid) ? rid?.ToString() : null,
                    Metadata = !string.IsNullOrEmpty(metadataStr)
                        ? System.Text.Json.JsonSerializer.Deserialize<Dictionary<string, object>>(metadataStr) : new Dictionary<string, object>(),
                    CreatedAt = metadata.TryGetValue("createdAt", out var ca) && ca != null ? DateTime.Parse(ca.ToString()!) : DateTime.UtcNow,
                    UpdatedAt = metadata.TryGetValue("updatedAt", out var ua) && !string.IsNullOrEmpty(ua?.ToString()) ? DateTime.Parse(ua.ToString()!) : null,
                    Hash = metadata.TryGetValue("hash", out var hash) ? hash?.ToString() : null
                },
                Score = 1 - (result.Distances?[0][i] ?? 0) // Convert distance to similarity
            });
        }

        return results;
    }

    public async Task<List<MemoryItem>> ListAsync(string? userId = null, int limit = 100, CancellationToken ct = default)
    {
        var getRequest = new
        {
            where = userId != null ? new Dictionary<string, object>
            {
                ["userId"] = userId
            } : null,
            limit,
            include = new[] { "documents", "metadatas" }
        };

        var response = await _httpClient.PostAsJsonAsync(
            $"/api/v1/collections/{_collectionName}/get",
            getRequest,
            ct);

        await response.EnsureSuccessWithContentAsync();

        var result = await response.Content.ReadFromJsonAsync<ChromaGetResponse>(ct);

        if (result?.Ids == null || result.Ids.Count == 0)
        {
            return new List<MemoryItem>();
        }

        var items = new List<MemoryItem>();
        for (int i = 0; i < result.Ids.Count; i++)
        {
            var metadata = result.Metadatas?[i] ?? new Dictionary<string, object>();
            var metadataStr = metadata.TryGetValue("metadata", out var metaObj) ? metaObj?.ToString() : null;

            items.Add(new MemoryItem
            {
                Id = result.Ids[i],
                Data = result.Documents?[i] ?? string.Empty,
                Embedding = Array.Empty<float>(),
                UserId = metadata.TryGetValue("userId", out var uid) ? uid?.ToString() : null,
                AgentId = metadata.TryGetValue("agentId", out var aid) ? aid?.ToString() : null,
                RunId = metadata.TryGetValue("runId", out var rid) ? rid?.ToString() : null,
                Metadata = !string.IsNullOrEmpty(metadataStr)
                    ? System.Text.Json.JsonSerializer.Deserialize<Dictionary<string, object>>(metadataStr) ?? new Dictionary<string, object>()
                    : new Dictionary<string, object>(),
                CreatedAt = metadata.TryGetValue("createdAt", out var ca) && ca != null ? DateTime.Parse(ca.ToString()!) : DateTime.UtcNow,
                UpdatedAt = metadata.TryGetValue("updatedAt", out var ua) && !string.IsNullOrEmpty(ua?.ToString()) ? DateTime.Parse(ua.ToString()!) : null,
                Hash = metadata.TryGetValue("hash", out var hash) ? hash?.ToString() : null
            });
        }

        return items;
    }

    public async Task<MemoryItem?> GetAsync(string memoryId, CancellationToken ct = default)
    {
        var getRequest = new
        {
            ids = new[] { memoryId },
            include = new[] { "documents", "metadatas", "embeddings" }
        };

        var response = await _httpClient.PostAsJsonAsync(
            $"/api/v1/collections/{_collectionName}/get",
            getRequest,
            ct);

        if (!response.IsSuccessStatusCode)
        {
            return null;
        }

        var result = await response.Content.ReadFromJsonAsync<ChromaGetResponse>(ct);

        if (result?.Ids == null || result.Ids.Count == 0)
        {
            return null;
        }

        var metadata = result.Metadatas?[0] ?? new Dictionary<string, object>();
        var metadataStr = metadata.TryGetValue("metadata", out var metaObj) ? metaObj?.ToString() : null;

        return new MemoryItem
        {
            Id = result.Ids[0],
            Data = result.Documents?[0] ?? string.Empty,
            Embedding = result.Embeddings?[0] ?? Array.Empty<float>(),
            UserId = metadata.TryGetValue("userId", out var uid) ? uid?.ToString() : null,
            AgentId = metadata.TryGetValue("agentId", out var aid) ? aid?.ToString() : null,
            RunId = metadata.TryGetValue("runId", out var rid) ? rid?.ToString() : null,
            Metadata = !string.IsNullOrEmpty(metadataStr)
                ? System.Text.Json.JsonSerializer.Deserialize<Dictionary<string, object>>(metadataStr) ?? new Dictionary<string, object>()
                : new Dictionary<string, object>(),
            CreatedAt = metadata.TryGetValue("createdAt", out var ca) && ca != null ? DateTime.Parse(ca.ToString()!) : DateTime.UtcNow,
            UpdatedAt = metadata.TryGetValue("updatedAt", out var ua) && !string.IsNullOrEmpty(ua?.ToString()) ? DateTime.Parse(ua.ToString()!) : null,
            Hash = metadata.TryGetValue("hash", out var hash) ? hash?.ToString() : null
        };
    }

    public async Task DeleteAsync(string memoryId, CancellationToken ct = default)
    {
        var deleteRequest = new
        {
            ids = new[] { memoryId }
        };

        var response = await _httpClient.PostAsJsonAsync(
            $"/api/v1/collections/{_collectionName}/delete",
            deleteRequest,
            ct);

        await response.EnsureSuccessWithContentAsync();
    }

    public async Task DeleteByUserAsync(string userId, CancellationToken ct = default)
    {
        var deleteRequest = new
        {
            where = new Dictionary<string, object>
            {
                ["userId"] = userId
            }
        };

        var response = await _httpClient.PostAsJsonAsync(
            $"/api/v1/collections/{_collectionName}/delete",
            deleteRequest,
            ct);

        await response.EnsureSuccessWithContentAsync();
    }

    // Internal classes for JSON deserialization
    private class ChromaQueryResponse
    {
        [JsonPropertyName("ids")]
        public List<List<string>>? Ids { get; set; }

        [JsonPropertyName("documents")]
        public List<List<string>>? Documents { get; set; }

        [JsonPropertyName("metadatas")]
        public List<List<Dictionary<string, object>>>? Metadatas { get; set; }

        [JsonPropertyName("distances")]
        public List<List<float>>? Distances { get; set; }
    }

    private class ChromaGetResponse
    {
        [JsonPropertyName("ids")]
        public List<string>? Ids { get; set; }

        [JsonPropertyName("documents")]
        public List<string>? Documents { get; set; }

        [JsonPropertyName("metadatas")]
        public List<Dictionary<string, object>>? Metadatas { get; set; }

        [JsonPropertyName("embeddings")]
        public List<float[]>? Embeddings { get; set; }
    }
}
