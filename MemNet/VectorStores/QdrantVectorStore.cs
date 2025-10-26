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
/// Qdrant vector store implementation
/// </summary>
public class QdrantVectorStore : IVectorStore
{
    private readonly HttpClient _httpClient;
    private readonly VectorStoreConfig _config;
    private readonly string _collectionName;

    public QdrantVectorStore(HttpClient httpClient, IOptions<MemoryConfig> config)
    {
        _httpClient = httpClient;
        _config = config.Value.VectorStore;
        _collectionName = _config.CollectionName;

        // Configure HttpClient
        if (_httpClient.BaseAddress == null)
        {
            _httpClient.BaseAddress = new Uri(_config.Endpoint);
        }

        if (!string.IsNullOrEmpty(_config.ApiKey))
        {
            _httpClient.DefaultRequestHeaders.Add("api-key", _config.ApiKey);
        }
    }

    public async Task EnsureCollectionExistsAsync(int vectorSize, bool allowRecreation = false, CancellationToken ct = default)
    {
        // Check if collection exists
        var response = await _httpClient.GetAsync($"/collections/{_collectionName}", ct);
            
        if (response.IsSuccessStatusCode)
        {
            // Collection exists, check if vector size matches
            var result = await response.Content.ReadFromJsonAsync<QdrantCollectionInfo>(ct);
            var existingSize = result?.Result?.Config?.Params?.Vectors?.Size ?? 0;
                
            if (existingSize != vectorSize)
            {
                if (allowRecreation)
                {
                    // Delete and recreate with correct size
                    await _httpClient.DeleteAsync($"/collections/{_collectionName}", ct);
                    await CreateCollectionAsync(vectorSize, ct);
                }
                else
                {
                    throw new InvalidOperationException(
                        $"Collection '{_collectionName}' exists with vector size {existingSize}, but {vectorSize} was requested. " +
                        "Set allowRecreation=true to automatically recreate the collection.");
                }
            }
            // Collection exists with correct size, do nothing
            return;
        }

        // Collection doesn't exist, create it
        await CreateCollectionAsync(vectorSize, ct);
    }

    private async Task CreateCollectionAsync(int vectorSize, CancellationToken ct)
    {
        var createRequest = new
        {
            vectors = new
            {
                size = vectorSize,
                distance = "Cosine"
            }
        };

        var response = await _httpClient.PutAsJsonAsync($"/collections/{_collectionName}", createRequest, ct);
        await response.EnsureSuccessWithContentAsync();
    }

    public async Task InsertAsync(List<MemoryItem> memories, CancellationToken ct = default)
    {
        var points = memories.Select(m => new
        {
            id = m.Id,
            vector = m.Embedding,
            payload = new
            {
                data = m.Data,
                userId = m.UserId,
                agentId = m.AgentId,
                runId = m.RunId,
                metadata = m.Metadata,
                createdAt = m.CreatedAt.ToString("o"),
                updatedAt = m.UpdatedAt?.ToString("o"),
                hash = m.Hash
            }
        }).ToList();

        var request = new { points };

        var response = await _httpClient.PutAsJsonAsync(
            $"/collections/{_collectionName}/points?wait=true",
            request,
            ct);

        await response.EnsureSuccessWithContentAsync();
    }

    public async Task UpdateAsync(List<MemoryItem> memories, CancellationToken ct = default)
    {
        // Qdrant uses upsert, so we use the same logic as insert
        await InsertAsync(memories, ct);
    }

    public async Task<List<MemorySearchResult>> SearchAsync(float[] queryVector, string? userId = null, int limit = 100, CancellationToken ct = default)
    {
        var searchRequest = new
        {
            vector = queryVector,
            limit,
            with_payload = true,
            filter = userId != null ? new
            {
                must = new[]
                {
                    new
                    {
                        key = "userId",
                        match = new { value = userId }
                    }
                }
            } : null
        };

        var response = await _httpClient.PostAsJsonAsync(
            $"/collections/{_collectionName}/points/search",
            searchRequest,
            ct);

        await response.EnsureSuccessWithContentAsync();

        var result = await response.Content.ReadFromJsonAsync<QdrantSearchResponse>(ct);

        return result?.Result?.Select(r => new MemorySearchResult
        {
            Id = r.Id,
            Memory = new MemoryItem
            {
                Id = r.Id,
                Data = r.Payload.Data,
                Embedding = Array.Empty<float>(), // Not returned in search
                UserId = r.Payload.UserId,
                AgentId = r.Payload.AgentId,
                RunId = r.Payload.RunId,
                Metadata = r.Payload.Metadata ?? new Dictionary<string, object>(),
                CreatedAt = DateTime.Parse(r.Payload.CreatedAt),
                UpdatedAt = !string.IsNullOrEmpty(r.Payload.UpdatedAt) ? DateTime.Parse(r.Payload.UpdatedAt) : null,
                Hash = r.Payload.Hash
            },
            Score = r.Score
        }).ToList() ?? new List<MemorySearchResult>();
    }

    public async Task<List<MemoryItem>> ListAsync(string? userId = null, int limit = 100, CancellationToken ct = default)
    {
        var scrollRequest = new
        {
            limit,
            with_payload = true,
            with_vector = false,
            filter = userId != null ? new
            {
                must = new[]
                {
                    new
                    {
                        key = "userId",
                        match = new { value = userId }
                    }
                }
            } : null
        };

        var response = await _httpClient.PostAsJsonAsync(
            $"/collections/{_collectionName}/points/scroll",
            scrollRequest,
            ct);

        await response.EnsureSuccessWithContentAsync();

        var result = await response.Content.ReadFromJsonAsync<QdrantScrollResponse>(ct);

        return result?.Result?.Points?.Select(p => new MemoryItem
        {
            Id = p.Id,
            Data = p.Payload.Data,
            Embedding = Array.Empty<float>(),
            UserId = p.Payload.UserId,
            AgentId = p.Payload.AgentId,
            RunId = p.Payload.RunId,
            Metadata = p.Payload.Metadata ?? new Dictionary<string, object>(),
            CreatedAt = DateTime.Parse(p.Payload.CreatedAt),
            UpdatedAt = !string.IsNullOrEmpty(p.Payload.UpdatedAt) ? DateTime.Parse(p.Payload.UpdatedAt) : null,
            Hash = p.Payload.Hash
        }).ToList() ?? new List<MemoryItem>();
    }

    public async Task<MemoryItem?> GetAsync(string memoryId, CancellationToken ct = default)
    {
        var response = await _httpClient.GetAsync(
            $"/collections/{_collectionName}/points/{memoryId}",
            ct);

        if (!response.IsSuccessStatusCode)
        {
            return null;
        }

        var result = await response.Content.ReadFromJsonAsync<QdrantPointResponse>(ct);

        if (result?.Result == null)
        {
            return null;
        }

        return new MemoryItem
        {
            Id = result.Result.Id,
            Data = result.Result.Payload.Data,
            Embedding = result.Result.Vector ?? Array.Empty<float>(),
            UserId = result.Result.Payload.UserId,
            AgentId = result.Result.Payload.AgentId,
            RunId = result.Result.Payload.RunId,
            Metadata = result.Result.Payload.Metadata ?? new Dictionary<string, object>(),
            CreatedAt = DateTime.Parse(result.Result.Payload.CreatedAt),
            UpdatedAt = !string.IsNullOrEmpty(result.Result.Payload.UpdatedAt) ? DateTime.Parse(result.Result.Payload.UpdatedAt) : null,
            Hash = result.Result.Payload.Hash
        };
    }

    public async Task DeleteAsync(string memoryId, CancellationToken ct = default)
    {
        var request = new
        {
            points = new[] { memoryId }
        };

        var response = await _httpClient.PostAsJsonAsync(
            $"/collections/{_collectionName}/points/delete?wait=true",
            request,
            ct);

        await response.EnsureSuccessWithContentAsync();
    }

    public async Task DeleteByUserAsync(string userId, CancellationToken ct = default)
    {
        var deleteRequest = new
        {
            filter = new
            {
                must = new[]
                {
                    new
                    {
                        key = "userId",
                        match = new { value = userId }
                    }
                }
            }
        };

        var response = await _httpClient.PostAsJsonAsync(
            $"/collections/{_collectionName}/points/delete?wait=true",
            deleteRequest,
            ct);

        await response.EnsureSuccessWithContentAsync();
    }

    // Internal classes for JSON deserialization
    private class QdrantSearchResponse
    {
        [JsonPropertyName("result")]
        public List<QdrantSearchResult>? Result { get; set; }
    }

    private class QdrantSearchResult
    {
        [JsonPropertyName("id")]
        public string Id { get; set; } = string.Empty;

        [JsonPropertyName("score")]
        public float Score { get; set; }

        [JsonPropertyName("payload")]
        public QdrantPayload Payload { get; set; } = new();
    }

    private class QdrantScrollResponse
    {
        [JsonPropertyName("result")]
        public QdrantScrollResult? Result { get; set; }
    }

    private class QdrantScrollResult
    {
        [JsonPropertyName("points")]
        public List<QdrantPoint>? Points { get; set; }
    }

    private class QdrantPointResponse
    {
        [JsonPropertyName("result")]
        public QdrantPoint? Result { get; set; }
    }

    private class QdrantPoint
    {
        [JsonPropertyName("id")]
        public string Id { get; set; } = string.Empty;

        [JsonPropertyName("vector")]
        public float[]? Vector { get; set; }

        [JsonPropertyName("payload")]
        public QdrantPayload Payload { get; set; } = new();
    }

    private class QdrantPayload
    {
        [JsonPropertyName("data")]
        public string Data { get; set; } = string.Empty;

        [JsonPropertyName("userId")]
        public string? UserId { get; set; }

        [JsonPropertyName("agentId")]
        public string? AgentId { get; set; }

        [JsonPropertyName("runId")]
        public string? RunId { get; set; }

        [JsonPropertyName("metadata")]
        public Dictionary<string, object>? Metadata { get; set; }

        [JsonPropertyName("createdAt")]
        public string CreatedAt { get; set; } = string.Empty;

        [JsonPropertyName("updatedAt")]
        public string? UpdatedAt { get; set; }

        [JsonPropertyName("hash")]
        public string? Hash { get; set; }
    }

    private class QdrantCollectionInfo
    {
        [JsonPropertyName("result")]
        public QdrantCollectionResult? Result { get; set; }
    }

    private class QdrantCollectionResult
    {
        [JsonPropertyName("config")]
        public QdrantCollectionConfig Config { get; set; } = new();
    }

    private class QdrantCollectionConfig
    {
        [JsonPropertyName("params")]
        public QdrantCollectionParams Params { get; set; } = new();
    }

    private class QdrantCollectionParams
    {
        [JsonPropertyName("vectors")]
        public QdrantVectorParams Vectors { get; set; } = new();
    }

    private class QdrantVectorParams
    {
        [JsonPropertyName("size")]
        public int Size { get; set; }
    }
}
