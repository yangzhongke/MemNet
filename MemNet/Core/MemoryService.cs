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
///     记忆服务核心实现（复刻 Mem0 Memory 类）
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
        // 1. 组合消息内容
        var messagesText = string.Join("\n", (IEnumerable<string>)request.Messages.Select(m => $"{m.Role}: {m.Content}"));

        // 2. 使用 LLM 提取结构化记忆
        var extractedMemories = await _llm.ExtractMemoriesAsync(messagesText, ct);

        // 3. 为每条记忆生成嵌入
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

        // 4. 检查是否存在相似记忆（去重）
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
                // 使用 LLM 合并记忆
                var merged = await _llm.MergeMemoriesAsync(similar.Memory.Data, newMem.Data, ct);
                similar.Memory.Data = merged;
                similar.Memory.UpdatedAt = DateTime.UtcNow;

                // 重新生成嵌入
                similar.Memory.Embedding = await _embedder.EmbedAsync(merged, ct);
                toUpdate.Add(similar.Memory);
            }
            else
            {
                toInsert.Add(newMem);
            }
        }

        // 5. 批量写入向量库
        if (toInsert.Any())
        {
            await _vectorStore.InsertAsync(toInsert, ct);
        }

        if (toUpdate.Any())
        {
            await _vectorStore.UpdateAsync(toUpdate, ct);
        }
        // 6. 构建响应
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
        // 1. 生成查询向量
        var queryEmbedding = await _embedder.EmbedAsync(request.Query, ct);

        // 2. 向量相似度搜索
        var results = await _vectorStore.SearchAsync(
            queryEmbedding,
            request.UserId,
            request.Limit,
            ct);

        // 3. 使用 LLM 重排序（可选）
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

        await _vectorStore.UpdateAsync(new List<MemoryItem> { memory }, ct);
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