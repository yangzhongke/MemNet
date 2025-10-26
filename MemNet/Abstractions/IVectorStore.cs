using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using MemNet.Models;

namespace MemNet.Abstractions;

/// <summary>
/// 向量存储接口（复刻 Mem0 vector_stores/base.py）
/// </summary>
public interface IVectorStore
{
    /// <summary>
    /// 插入记忆
    /// </summary>
    Task InsertAsync(List<MemoryItem> memories, CancellationToken ct = default);
    
    /// <summary>
    /// 更新记忆
    /// </summary>
    Task UpdateAsync(List<MemoryItem> memories, CancellationToken ct = default);
    
    /// <summary>
    /// 向量相似度搜索
    /// </summary>
    Task<List<MemorySearchResult>> SearchAsync(float[] queryVector, string? userId = null, int limit = 100, CancellationToken ct = default);
    
    /// <summary>
    /// 获取指定用户的所有记忆
    /// </summary>
    Task<List<MemoryItem>> ListAsync(string? userId = null, int limit = 100, CancellationToken ct = default);
    
    /// <summary>
    /// 根据ID获取记忆
    /// </summary>
    Task<MemoryItem?> GetAsync(string memoryId, CancellationToken ct = default);
    
    /// <summary>
    /// 删除记忆
    /// </summary>
    Task DeleteAsync(string memoryId, CancellationToken ct = default);
    
    /// <summary>
    /// 删除用户所有记忆
    /// </summary>
    Task DeleteByUserAsync(string userId, CancellationToken ct = default);
}

