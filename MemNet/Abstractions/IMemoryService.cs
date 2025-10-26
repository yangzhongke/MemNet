using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using MemNet.Models;

namespace MemNet.Abstractions;

/// <summary>
/// Memory service interface
/// </summary>
public interface IMemoryService
{
    /// <summary>
    /// Add memory
    /// </summary>
    Task<AddMemoryResponse> AddAsync(AddMemoryRequest request, CancellationToken ct = default);
    
    /// <summary>
    /// Search memories
    /// </summary>
    Task<List<MemorySearchResult>> SearchAsync(SearchMemoryRequest request, CancellationToken ct = default);
    
    /// <summary>
    /// Get all memories
    /// </summary>
    Task<List<MemoryItem>> GetAllAsync(string? userId = null, int limit = 100, CancellationToken ct = default);
    
    /// <summary>
    /// Get specific memory
    /// </summary>
    Task<MemoryItem?> GetAsync(string memoryId, CancellationToken ct = default);
    
    /// <summary>
    /// Update memory
    /// </summary>
    Task<bool> UpdateAsync(string memoryId, string content, CancellationToken ct = default);
    
    /// <summary>
    /// Delete memory
    /// </summary>
    Task DeleteAsync(string memoryId, CancellationToken ct = default);
    
    /// <summary>
    /// Delete all memories
    /// </summary>
    Task DeleteAllAsync(string userId, CancellationToken ct = default);
}
