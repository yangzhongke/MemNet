using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using MemNet.Abstractions;
using MemNet.Config;
using MemNet.Models;
using Microsoft.Extensions.Options;
using StackExchange.Redis;
using NRedisStack;
using NRedisStack.RedisStackCommands;
using NRedisStack.Search;
using NRedisStack.Search.Literals.Enums;

namespace MemNet.Redis;

/// <summary>
/// Redis Stack vector store implementation with vector similarity search support
/// </summary>
public class RedisVectorStore : IVectorStore
{
    private readonly IConnectionMultiplexer _redis;
    private readonly IDatabase _db;
    private readonly VectorStoreConfig _config;
    private readonly string _indexName;
    private readonly string _keyPrefix;

    public RedisVectorStore(IConnectionMultiplexer redis, IOptions<MemoryConfig> config)
    {
        _redis = redis ?? throw new ArgumentNullException(nameof(redis));
        _config = config.Value.VectorStore;
        _db = _redis.GetDatabase();
        _indexName = $"idx:{_config.CollectionName}";
        _keyPrefix = $"{_config.CollectionName}:";
    }

    public async Task EnsureCollectionExistsAsync(int vectorSize, bool allowRecreation, CancellationToken ct = default)
    {
        var ft = _db.FT();

        // 使用 _LIST 命令检查索引是否存在
        RedisResult[] indexes = ft._List();
        bool indexExists = indexes.Any(e=>_indexName.Equals((string)e!));

        if (indexExists)
        {
            if (allowRecreation)
            {
                ft.DropIndex(_indexName);
                await CreateIndexAsync(ft, vectorSize);
            }
            return;
        }

        // 索引不存在,创建它
        await CreateIndexAsync(ft, vectorSize);
    }

    private Task CreateIndexAsync(ISearchCommands ft, int vectorSize)
    {
        var schema = new Schema()
            .AddTextField("id")
            .AddTextField("data")
            .AddTextField("user_id")
            .AddTextField("hash")
            .AddTextField("metadata")
            .AddNumericField("created_at")
            .AddNumericField("updated_at")
            .AddVectorField("embedding",
                Schema.VectorField.VectorAlgo.HNSW,
                new Dictionary<string, object>
                {
                    ["TYPE"] = "FLOAT32",
                    ["DIM"] = vectorSize,
                    ["DISTANCE_METRIC"] = "COSINE"
                });

        bool success = ft.Create(_indexName,
            new FTCreateParams()
                .On(IndexDataType.HASH)
                .Prefix(_keyPrefix),
            schema);
        if(!success)
        {
            throw new Exception("Failed to create Redis vector index.");
        }
        return Task.CompletedTask;
    }

    public async Task InsertAsync(List<MemoryItem> memories, CancellationToken ct = default)
    {
        foreach (var memory in memories)
        {
            var key = $"{_keyPrefix}{memory.Id}";
            var hashEntries = new HashEntry[]
            {
                new("id", memory.Id),
                new("data", memory.Data),
                new("user_id", memory.UserId ?? string.Empty),
                new("hash", memory.Hash ?? string.Empty),
                new("metadata", System.Text.Json.JsonSerializer.Serialize(memory.Metadata ?? new Dictionary<string, object>())),
                new("created_at", memory.CreatedAt.Ticks),
                new("updated_at", memory.UpdatedAt?.Ticks ?? 0),
                new("embedding", SerializeVector(memory.Embedding))
            };

            await _db.HashSetAsync(key, hashEntries);
        }
    }

    public async Task UpdateAsync(List<MemoryItem> memories, CancellationToken ct = default)
    {
        foreach (var memory in memories)
        {
            var key = $"{_keyPrefix}{memory.Id}";
            
            // Check if exists
            if (await _db.KeyExistsAsync(key))
            {
                var hashEntries = new HashEntry[]
                {
                    new("data", memory.Data),
                    new("hash", memory.Hash ?? string.Empty),
                    new("metadata", System.Text.Json.JsonSerializer.Serialize(memory.Metadata ?? new Dictionary<string, object>())),
                    new("updated_at", memory.UpdatedAt?.Ticks ?? DateTime.UtcNow.Ticks),
                    new("embedding", SerializeVector(memory.Embedding))
                };

                await _db.HashSetAsync(key, hashEntries);
            }
        }
    }

    public Task<List<MemorySearchResult>> SearchAsync(float[] queryVector, string? userId = null, int limit = 100, CancellationToken ct = default)
    {
        var ft = _db.FT();
        
        // Build query
        var queryStr = userId != null ? $"@user_id:{EscapeRedisQuery(userId)}" : "*";
        
        var query = new Query(queryStr)
            .SetSortBy("__embedding_score")
            .Limit(0, limit)
            .ReturnFields("id", "data", "user_id", "hash", "metadata", "created_at", "updated_at", "embedding", "__embedding_score")
            .Dialect(2);

        // Add vector similarity search parameter
        var vectorBytes = SerializeVector(queryVector);
        query.AddParam("query_vector", vectorBytes);
        query.AddParam("BLOB", vectorBytes);

        // Perform KNN search
        var searchQuery = $"{queryStr}=>[KNN {limit} @embedding $query_vector AS __embedding_score]";
        var fullQuery = new Query(searchQuery)
            .SetSortBy("__embedding_score")
            .Limit(0, limit)
            .ReturnFields("id", "data", "user_id", "hash", "metadata", "created_at", "updated_at", "__embedding_score")
            .Dialect(2);
        
        fullQuery.AddParam("query_vector", vectorBytes);

        var results = ft.Search(_indexName, fullQuery);
        
        var searchResults = new List<MemorySearchResult>();
        
        foreach (var doc in results.Documents)
        {
            var memoryItem = ParseMemoryItem(doc);
            var scoreValue = doc["__embedding_score"];
            var score = !scoreValue.IsNull && float.TryParse(scoreValue.ToString(), out var s)
                ? 1.0f - s // Convert distance to similarity
                : 0.0f;

            searchResults.Add(new MemorySearchResult
            {
                Id = memoryItem.Id,
                Memory = memoryItem,
                Score = score
            });
        }

        return Task.FromResult(searchResults);
    }

    public Task<List<MemoryItem>> ListAsync(string? userId = null, int limit = 100, CancellationToken ct = default)
    {
        var ft = _db.FT();
        
        var queryStr = userId != null ? $"@user_id:{EscapeRedisQuery(userId)}" : "*";
        var query = new Query(queryStr)
            .SetSortBy("created_at", false)
            .Limit(0, limit)
            .ReturnFields("id", "data", "user_id", "hash", "metadata", "created_at", "updated_at", "embedding")
            .Dialect(2);

        var results = ft.Search(_indexName, query);
        
        return Task.FromResult(results.Documents.Select(ParseMemoryItem).ToList());
    }

    public async Task<MemoryItem?> GetAsync(string memoryId, CancellationToken ct = default)
    {
        var key = $"{_keyPrefix}{memoryId}";
        
        if (!await _db.KeyExistsAsync(key))
        {
            return null;
        }

        var hash = await _db.HashGetAllAsync(key);
        
        if (hash.Length == 0)
        {
            return null;
        }

        return ParseMemoryItem(hash);
    }

    public async Task DeleteAsync(string memoryId, CancellationToken ct = default)
    {
        var key = $"{_keyPrefix}{memoryId}";
        await _db.KeyDeleteAsync(key);
    }

    public async Task DeleteByUserAsync(string userId, CancellationToken ct = default)
    {
        var memories = await ListAsync(userId, limit: 10000, ct);
        
        foreach (var memory in memories)
        {
            await DeleteAsync(memory.Id, ct);
        }
    }

    private MemoryItem ParseMemoryItem(Document doc)
    {
        var dict = new Dictionary<string, RedisValue>();
        foreach (var key in new[] { "id", "data", "user_id", "hash", "metadata", "created_at", "updated_at", "embedding" })
        {
            var value = doc[key];
            if (!value.IsNull)
            {
                dict[key] = value;
            }
        }
        return ParseMemoryItemFromDict(dict);
    }

    private MemoryItem ParseMemoryItem(HashEntry[] hash)
    {
        var dict = hash.ToDictionary(h => h.Name.ToString(), h => h.Value);
        return ParseMemoryItemFromDict(dict);
    }

    private MemoryItem ParseMemoryItemFromDict(Dictionary<string, RedisValue> dict)
    {
        var metadata = dict.ContainsKey("metadata") && !string.IsNullOrEmpty(dict["metadata"])
            ? System.Text.Json.JsonSerializer.Deserialize<Dictionary<string, object>>(dict["metadata"])
            : new Dictionary<string, object>();

        var embedding = dict.ContainsKey("embedding") && !string.IsNullOrEmpty(dict["embedding"])
            ? DeserializeVector((byte[])dict["embedding"])
            : Array.Empty<float>();

        var createdAtTicks = dict.ContainsKey("created_at") && long.TryParse(dict["created_at"], out var ct) 
            ? ct 
            : DateTime.UtcNow.Ticks;
        
        var updatedAtTicks = dict.ContainsKey("updated_at") && long.TryParse(dict["updated_at"], out var ut) && ut > 0
            ? (DateTime?)new DateTime(ut)
            : null;

        return new MemoryItem
        {
            Id = dict.ContainsKey("id") ? dict["id"] : string.Empty,
            Data = dict.ContainsKey("data") ? dict["data"] : string.Empty,
            UserId = dict.ContainsKey("user_id") && !string.IsNullOrEmpty(dict["user_id"]) ? dict["user_id"].ToString() : null,
            Hash = dict.ContainsKey("hash") ? dict["hash"].ToString() : null,
            Metadata = metadata,
            CreatedAt = new DateTime(createdAtTicks),
            UpdatedAt = updatedAtTicks,
            Embedding = embedding
        };
    }

    private byte[] SerializeVector(float[] vector)
    {
        var bytes = new byte[vector.Length * sizeof(float)];
        Buffer.BlockCopy(vector, 0, bytes, 0, bytes.Length);
        return bytes;
    }

    private float[] DeserializeVector(byte[] bytes)
    {
        var floats = new float[bytes.Length / sizeof(float)];
        Buffer.BlockCopy(bytes, 0, floats, 0, bytes.Length);
        return floats;
    }

    private string EscapeRedisQuery(string value)
    {
        // Escape special characters for Redis query
        return value.Replace("-", "\\-")
                   .Replace(":", "\\:")
                   .Replace("@", "\\@");
    }
}

