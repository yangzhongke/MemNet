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
/// Milvus vector store implementation
/// </summary>
public class MilvusVectorStore : IVectorStore
{
    private readonly HttpClient _httpClient;
    private readonly VectorStoreConfig _config;
    private readonly string _collectionName;

    public MilvusVectorStore(HttpClient httpClient, IOptions<MemoryConfig> config)
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
            _httpClient.DefaultRequestHeaders.Add("Authorization", $"Bearer {_config.ApiKey}");
        }
    }

    public async Task EnsureCollectionExistsAsync(int vectorSize, bool allowRecreation = false, CancellationToken ct = default)
    {
        // Check if collection exists
        var checkRequest = new
        {
            collection_name = _collectionName
        };

        var checkResponse = await _httpClient.PostAsJsonAsync("/v1/vector/collections/describe", checkRequest, ct);
            
        if (checkResponse.IsSuccessStatusCode)
        {
            // Collection exists, verify dimension matches
            var result = await checkResponse.Content.ReadFromJsonAsync<MilvusCollectionInfo>(ct);
            var existingDimension = result?.Data?.VectorField?.Dimension ?? 0;
                
            if (existingDimension != vectorSize)
            {
                if (allowRecreation)
                {
                    // Delete and recreate with correct dimension
                    var deleteRequest = new { collection_name = _collectionName };
                    await _httpClient.PostAsJsonAsync("/v1/vector/collections/drop", deleteRequest, ct);
                    await CreateCollectionAsync(vectorSize, ct);
                }
                else
                {
                    throw new InvalidOperationException(
                        $"Collection '{_collectionName}' exists with dimension {existingDimension}, but {vectorSize} was requested. " +
                        "Set allowRecreation=true to automatically recreate the collection.");
                }
            }
            // Collection exists with correct dimension, do nothing
            return;
        }

        // Collection doesn't exist, create it
        await CreateCollectionAsync(vectorSize, ct);
    }

    private async Task CreateCollectionAsync(int vectorSize, CancellationToken ct)
    {
        var createRequest = new
        {
            collection_name = _collectionName,
            dimension = vectorSize,
            metric_type = "COSINE",
            primary_field = "id",
            vector_field = "embedding"
        };

        var response = await _httpClient.PostAsJsonAsync("/v1/vector/collections/create", createRequest, ct);
        await response.EnsureSuccessWithContentAsync();
    }

    public async Task InsertAsync(List<MemoryItem> memories, CancellationToken ct = default)
    {
        var data = memories.Select(m => new Dictionary<string, object>
        {
            ["id"] = m.Id,
            ["embedding"] = m.Embedding,
            ["data"] = m.Data,
            ["userId"] = m.UserId ?? string.Empty,
            ["agentId"] = m.AgentId ?? string.Empty,
            ["runId"] = m.RunId ?? string.Empty,
            ["metadata"] = System.Text.Json.JsonSerializer.Serialize(m.Metadata),
            ["createdAt"] = m.CreatedAt.ToString("o"),
            ["updatedAt"] = m.UpdatedAt?.ToString("o") ?? string.Empty,
            ["hash"] = m.Hash ?? string.Empty
        }).ToList();

        var request = new
        {
            collection_name = _collectionName,
            data
        };

        var response = await _httpClient.PostAsJsonAsync("/v1/vector/insert", request, ct);
        await response.EnsureSuccessWithContentAsync();
    }

    public async Task UpdateAsync(List<MemoryItem> memories, CancellationToken ct = default)
    {
        // Delete existing entries first
        var ids = memories.Select(m => m.Id).ToList();
        await DeleteMultipleAsync(ids, ct);

        // Insert updated memories
        await InsertAsync(memories, ct);
    }

    public async Task<List<MemorySearchResult>> SearchAsync(float[] queryVector, string? userId = null, int limit = 100, CancellationToken ct = default)
    {
        var searchRequest = new
        {
            collection_name = _collectionName,
            vector = queryVector,
            limit,
            output_fields = new[] { "id", "data", "userId", "agentId", "runId", "metadata", "createdAt", "updatedAt", "hash" },
            filter = userId != null ? $"userId == \"{userId}\"" : null
        };

        var response = await _httpClient.PostAsJsonAsync("/v1/vector/search", searchRequest, ct);
        await response.EnsureSuccessWithContentAsync();

        var result = await response.Content.ReadFromJsonAsync<MilvusSearchResponse>(ct);

        if (result?.Data == null || result.Data.Count == 0)
        {
            return new List<MemorySearchResult>();
        }

        return result.Data[0].Select(item => new MemorySearchResult
        {
            Id = item.Id,
            Memory = new MemoryItem
            {
                Id = item.Id,
                Data = item.Data,
                Embedding = Array.Empty<float>(),
                UserId = item.UserId,
                AgentId = item.AgentId,
                RunId = item.RunId,
                Metadata = !string.IsNullOrEmpty(item.Metadata) 
                    ? System.Text.Json.JsonSerializer.Deserialize<Dictionary<string, object>>(item.Metadata) ?? new Dictionary<string, object>()
                    : new Dictionary<string, object>(),
                CreatedAt = DateTime.Parse(item.CreatedAt),
                UpdatedAt = !string.IsNullOrEmpty(item.UpdatedAt) ? DateTime.Parse(item.UpdatedAt) : null,
                Hash = item.Hash
            },
            Score = item.Distance
        }).ToList();
    }

    public async Task<List<MemoryItem>> ListAsync(string? userId = null, int limit = 100, CancellationToken ct = default)
    {
        var queryRequest = new
        {
            collection_name = _collectionName,
            filter = userId != null ? $"userId == \"{userId}\"" : string.Empty,
            limit,
            output_fields = new[] { "id", "data", "userId", "agentId", "runId", "metadata", "createdAt", "updatedAt", "hash" }
        };

        var response = await _httpClient.PostAsJsonAsync("/v1/vector/query", queryRequest, ct);
        await response.EnsureSuccessWithContentAsync();

        var result = await response.Content.ReadFromJsonAsync<MilvusQueryResponse>(ct);

        return result?.Data?.Select(item => new MemoryItem
        {
            Id = item.Id,
            Data = item.Data,
            Embedding = Array.Empty<float>(),
            UserId = item.UserId,
            AgentId = item.AgentId,
            RunId = item.RunId,
            Metadata = !string.IsNullOrEmpty(item.Metadata)
                ? System.Text.Json.JsonSerializer.Deserialize<Dictionary<string, object>>(item.Metadata) ?? new Dictionary<string, object>()
                : new Dictionary<string, object>(),
            CreatedAt = DateTime.Parse(item.CreatedAt),
            UpdatedAt = !string.IsNullOrEmpty(item.UpdatedAt) ? DateTime.Parse(item.UpdatedAt) : null,
            Hash = item.Hash
        }).ToList() ?? new List<MemoryItem>();
    }

    public async Task<MemoryItem?> GetAsync(string memoryId, CancellationToken ct = default)
    {
        var queryRequest = new
        {
            collection_name = _collectionName,
            filter = $"id == \"{memoryId}\"",
            limit = 1,
            output_fields = new[] { "id", "embedding", "data", "userId", "agentId", "runId", "metadata", "createdAt", "updatedAt", "hash" }
        };

        var response = await _httpClient.PostAsJsonAsync("/v1/vector/query", queryRequest, ct);
        
        if (!response.IsSuccessStatusCode)
        {
            return null;
        }

        var result = await response.Content.ReadFromJsonAsync<MilvusQueryResponse>(ct);

        var item = result?.Data?.FirstOrDefault();
        if (item == null)
        {
            return null;
        }

        return new MemoryItem
        {
            Id = item.Id,
            Data = item.Data,
            Embedding = item.Embedding ?? Array.Empty<float>(),
            UserId = item.UserId,
            AgentId = item.AgentId,
            RunId = item.RunId,
            Metadata = !string.IsNullOrEmpty(item.Metadata)
                ? System.Text.Json.JsonSerializer.Deserialize<Dictionary<string, object>>(item.Metadata) ?? new Dictionary<string, object>()
                : new Dictionary<string, object>(),
            CreatedAt = DateTime.Parse(item.CreatedAt),
            UpdatedAt = !string.IsNullOrEmpty(item.UpdatedAt) ? DateTime.Parse(item.UpdatedAt) : null,
            Hash = item.Hash
        };
    }

    public async Task DeleteAsync(string memoryId, CancellationToken ct = default)
    {
        await DeleteMultipleAsync(new List<string> { memoryId }, ct);
    }

    public async Task DeleteByUserAsync(string userId, CancellationToken ct = default)
    {
        var deleteRequest = new
        {
            collection_name = _collectionName,
            filter = $"userId == \"{userId}\""
        };

        var response = await _httpClient.PostAsJsonAsync("/v1/vector/delete", deleteRequest, ct);
        await response.EnsureSuccessWithContentAsync();
    }

    private async Task DeleteMultipleAsync(List<string> ids, CancellationToken ct = default)
    {
        var deleteRequest = new
        {
            collection_name = _collectionName,
            filter = $"id in [{string.Join(",", ids.Select(id => $"\"{id}\""))}]"
        };

        var response = await _httpClient.PostAsJsonAsync("/v1/vector/delete", deleteRequest, ct);
        await response.EnsureSuccessWithContentAsync();
    }

    // Internal classes for JSON deserialization
    private class MilvusSearchResponse
    {
        [JsonPropertyName("code")]
        public int Code { get; set; }

        [JsonPropertyName("data")]
        public List<List<MilvusSearchItem>>? Data { get; set; }
    }

    private class MilvusSearchItem
    {
        [JsonPropertyName("id")]
        public string Id { get; set; } = string.Empty;

        [JsonPropertyName("distance")]
        public float Distance { get; set; }

        [JsonPropertyName("data")]
        public string Data { get; set; } = string.Empty;

        [JsonPropertyName("userId")]
        public string? UserId { get; set; }

        [JsonPropertyName("agentId")]
        public string? AgentId { get; set; }

        [JsonPropertyName("runId")]
        public string? RunId { get; set; }

        [JsonPropertyName("metadata")]
        public string Metadata { get; set; } = string.Empty;

        [JsonPropertyName("createdAt")]
        public string CreatedAt { get; set; } = string.Empty;

        [JsonPropertyName("updatedAt")]
        public string? UpdatedAt { get; set; }

        [JsonPropertyName("hash")]
        public string? Hash { get; set; }
    }

    private class MilvusQueryResponse
    {
        [JsonPropertyName("code")]
        public int Code { get; set; }

        [JsonPropertyName("data")]
        public List<MilvusQueryItem>? Data { get; set; }
    }

    private class MilvusQueryItem
    {
        [JsonPropertyName("id")]
        public string Id { get; set; } = string.Empty;

        [JsonPropertyName("embedding")]
        public float[]? Embedding { get; set; }

        [JsonPropertyName("data")]
        public string Data { get; set; } = string.Empty;

        [JsonPropertyName("userId")]
        public string? UserId { get; set; }

        [JsonPropertyName("agentId")]
        public string? AgentId { get; set; }

        [JsonPropertyName("runId")]
        public string? RunId { get; set; }

        [JsonPropertyName("metadata")]
        public string Metadata { get; set; } = string.Empty;

        [JsonPropertyName("createdAt")]
        public string CreatedAt { get; set; } = string.Empty;

        [JsonPropertyName("updatedAt")]
        public string? UpdatedAt { get; set; }

        [JsonPropertyName("hash")]
        public string? Hash { get; set; }
    }

    private class MilvusCollectionInfo
    {
        [JsonPropertyName("code")]
        public int Code { get; set; }

        [JsonPropertyName("data")]
        public CollectionData? Data { get; set; }
    }

    private class CollectionData
    {
        [JsonPropertyName("vectorField")]
        public VectorFieldInfo? VectorField { get; set; }
    }

    private class VectorFieldInfo
    {
        [JsonPropertyName("dimension")]
        public int Dimension { get; set; }
    }
}
