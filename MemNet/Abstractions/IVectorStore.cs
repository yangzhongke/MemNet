using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using MemNet.Models;

namespace MemNet.Abstractions;

/// <summary>
/// Vector store interface (replicating Mem0 vector_stores/base.py)
/// </summary>
public interface IVectorStore
{
    /// <summary>
    /// Insert memories
    /// </summary>
    Task InsertAsync(List<MemoryItem> memories, CancellationToken ct = default);
    
    /// <summary>
    /// Update memories
    /// </summary>
    Task UpdateAsync(List<MemoryItem> memories, CancellationToken ct = default);
    
    /// <summary>
    /// Vector similarity search
    /// </summary>
    Task<List<MemorySearchResult>> SearchAsync(float[] queryVector, string? userId = null, int limit = 100, CancellationToken ct = default);
    
    /// <summary>
    /// Get all memories for specified user
    /// </summary>
    Task<List<MemoryItem>> ListAsync(string? userId = null, int limit = 100, CancellationToken ct = default);
    
    /// <summary>
    /// Get memory by ID
    /// </summary>
    Task<MemoryItem?> GetAsync(string memoryId, CancellationToken ct = default);
    
    /// <summary>
    /// Delete memory
    /// </summary>
    Task DeleteAsync(string memoryId, CancellationToken ct = default);
    
    /// <summary>
    /// Delete all memories for user
    /// </summary>
    Task DeleteByUserAsync(string userId, CancellationToken ct = default);
}
