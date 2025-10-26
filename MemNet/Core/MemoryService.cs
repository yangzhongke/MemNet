using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using MemNet.Abstractions;
using MemNet.Config;
using MemNet.Models;
using Microsoft.Extensions.Options;

namespace MemNet.Core;

/// <summary>
///     Memory service core implementation (replicating Mem0 Memory class)
/// </summary>
public class MemoryService : IMemoryService
{
    private readonly MemoryConfig _config;
    private readonly IEmbedder _embedder;
    private readonly ILLMProvider _llm;
    private readonly IVectorStore _vectorStore;

    public MemoryService(
        IVectorStore vectorStore,
        ILLMProvider llm,
        IEmbedder embedder,
        IOptions<MemoryConfig> config)
    {
        _vectorStore = vectorStore;
        _llm = llm;
        _embedder = embedder;
        _config = config.Value;
    }

    public async Task<AddMemoryResponse> AddAsync(AddMemoryRequest request, CancellationToken ct = default)
    {
        // 1. Combine message content
        var messagesText = string.Join("\n", (IEnumerable<string>)request.Messages.Select(m => $"{m.Role}: {m.Content}"));

        // 2. Extract structured memories using LLM
        var extractedMemories = await _llm.ExtractMemoriesAsync(messagesText, ct);

        // 3. Generate embeddings for each memory
        var memoriesToAdd = new List<MemoryItem>();
        foreach (var extracted in extractedMemories)
        {
            var embedding = await _embedder.EmbedAsync(extracted.Data, ct);
            memoriesToAdd.Add(new MemoryItem
            {
                Id = Guid.NewGuid().ToString(),
                Data = extracted.Data,
                Embedding = embedding,
                UserId = request.UserId,
                AgentId = request.AgentId,
                RunId = request.RunId,
                Metadata = request.Metadata ?? new Dictionary<string, object>(),
                CreatedAt = DateTime.UtcNow
            });
        }

        // 4. Check for similar memories (deduplication)
        var toUpdate = new List<MemoryItem>();
        var toInsert = new List<MemoryItem>();

        foreach (var newMem in memoriesToAdd)
        {
            var similarMemories = await _vectorStore.SearchAsync(
                newMem.Embedding,
                request.UserId,
                5,
                ct);

            var similar = similarMemories.FirstOrDefault(s =>
                s.Score > _config.DuplicateThreshold);

            if (similar != null)
            {
                // Merge memories using LLM
                var merged = await _llm.MergeMemoriesAsync(similar.Memory.Data, newMem.Data, ct);
                similar.Memory.Data = merged;
                similar.Memory.UpdatedAt = DateTime.UtcNow;

                // Regenerate embedding
                similar.Memory.Embedding = await _embedder.EmbedAsync(merged, ct);
                toUpdate.Add(similar.Memory);
            }
            else
            {
                toInsert.Add(newMem);
            }
        }

        // 5. Batch write to vector store
        if (toInsert.Any())
        {
            await _vectorStore.InsertAsync(toInsert, ct);
        }

        if (toUpdate.Any())
        {
            await _vectorStore.UpdateAsync(toUpdate, ct);
        }
        // 6. Build response
        return new AddMemoryResponse
        {
            Results = toInsert.Select(m => new MemoryResult
            {
                Id = m.Id,
                Memory = m.Data,
                Event = "add"
            }).Concat(toUpdate.Select(m => new MemoryResult
            {
                Id = m.Id,
                Memory = m.Data,
                Event = "update"
            })).ToList()
        };
    }

    public async Task<List<MemorySearchResult>> SearchAsync(SearchMemoryRequest request, CancellationToken ct = default)
    {
        // 1. Generate query vector
        var queryEmbedding = await _embedder.EmbedAsync(request.Query, ct);

        // 2. Vector similarity search
        var results = await _vectorStore.SearchAsync(
            queryEmbedding,
            request.UserId,
            request.Limit,
            ct);

        // 3. Use LLM reranking (optional)
        if (_config.EnableReranking)
        {
            results = await _llm.RerankAsync(request.Query, results, ct);
        }

        return results;
    }

    public async Task<List<MemoryItem>> GetAllAsync(string? userId = null, int limit = 100,
        CancellationToken ct = default)
    {
        return await _vectorStore.ListAsync(userId, limit, ct);
    }

    public async Task<MemoryItem?> GetAsync(string memoryId, CancellationToken ct = default)
    {
        return await _vectorStore.GetAsync(memoryId, ct);
    }

    public async Task<bool> UpdateAsync(string memoryId, string content, CancellationToken ct = default)
    {
        var memory = await _vectorStore.GetAsync(memoryId, ct);
        if (memory == null)
        {
            return false;
        }

        memory.Data = content;
        memory.UpdatedAt = DateTime.UtcNow;
        memory.Embedding = await _embedder.EmbedAsync(content, ct);

        await _vectorStore.UpdateAsync([memory], ct);
        return true;
    }

    public async Task DeleteAsync(string memoryId, CancellationToken ct = default)
    {
        await _vectorStore.DeleteAsync(memoryId, ct);
    }

    public async Task DeleteAllAsync(string userId, CancellationToken ct = default)
    {
        await _vectorStore.DeleteByUserAsync(userId, ct);
    }
}