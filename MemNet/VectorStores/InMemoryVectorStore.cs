using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using MemNet.Abstractions;
using MemNet.Models;

namespace MemNet.VectorStores;

/// <summary>
/// In-memory vector store implementation (for development and testing)
/// </summary>
public class InMemoryVectorStore : IVectorStore
{
    private readonly Dictionary<string, MemoryItem> _memories = new();
    private readonly object _lock = new();

    public Task EnsureCollectionExistsAsync(int vectorSize, bool allowRecreation = false, CancellationToken ct = default)
    {
        return Task.CompletedTask;
    }

    public Task InsertAsync(List<MemoryItem> memories, CancellationToken ct = default)
    {
        lock (_lock)
        {
            foreach (var memory in memories)
            {
                _memories[memory.Id] = memory;
            }
        }
        return Task.CompletedTask;
    }

    public Task UpdateAsync(List<MemoryItem> memories, CancellationToken ct = default)
    {
        lock (_lock)
        {
            foreach (var memory in memories)
            {
                if (_memories.ContainsKey(memory.Id))
                {
                    _memories[memory.Id] = memory;
                }
            }
        }
        return Task.CompletedTask;
    }

    public Task<List<MemorySearchResult>> SearchAsync(float[] queryVector, string? userId = null, int limit = 100, CancellationToken ct = default)
    {
        lock (_lock)
        {
            var results = _memories.Values
                .Where(m => userId == null || m.UserId == userId)
                .Select(m => new MemorySearchResult
                {
                    Id = m.Id,
                    Memory = m,
                    Score = CosineSimilarity(queryVector, m.Embedding)
                })
                .OrderByDescending(r => r.Score)
                .Take(limit)
                .ToList();

            return Task.FromResult(results);
        }
    }

    public Task<List<MemoryItem>> ListAsync(string? userId = null, int limit = 100, CancellationToken ct = default)
    {
        lock (_lock)
        {
            var results = _memories.Values
                .Where(m => userId == null || m.UserId == userId)
                .OrderByDescending(m => m.CreatedAt)
                .Take(limit)
                .ToList();

            return Task.FromResult(results);
        }
    }

    public Task<MemoryItem?> GetAsync(string memoryId, CancellationToken ct = default)
    {
        lock (_lock)
        {
            _memories.TryGetValue(memoryId, out var memory);
            return Task.FromResult(memory);
        }
    }

    public Task DeleteAsync(string memoryId, CancellationToken ct = default)
    {
        lock (_lock)
        {
            _memories.Remove(memoryId);
        }
        return Task.CompletedTask;
    }

    public Task DeleteByUserAsync(string userId, CancellationToken ct = default)
    {
        lock (_lock)
        {
            var toRemove = _memories.Values
                .Where(m => m.UserId == userId)
                .Select(m => m.Id)
                .ToList();

            foreach (var id in toRemove)
            {
                _memories.Remove(id);
            }
        }
        return Task.CompletedTask;
    }

    private static float CosineSimilarity(float[] a, float[] b)
    {
        if (a.Length != b.Length)
            return 0f;

        var dot = a.Zip(b, (x, y) => x * y).Sum();
        var magA = Math.Sqrt(a.Sum(x => x * x));
        var magB = Math.Sqrt(b.Sum(x => x * x));
        
        if (magA == 0 || magB == 0)
            return 0f;

        return (float)(dot / (magA * magB));
    }
}
