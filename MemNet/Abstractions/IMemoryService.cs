using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using MemNet.Models;

namespace MemNet.Abstractions;

/// <summary>
/// 记忆服务接口
/// </summary>
public interface IMemoryService
{
    /// <summary>
    /// 添加记忆
    /// </summary>
    Task<AddMemoryResponse> AddAsync(AddMemoryRequest request, CancellationToken ct = default);
    
    /// <summary>
    /// 搜索记忆
    /// </summary>
    Task<List<MemorySearchResult>> SearchAsync(SearchMemoryRequest request, CancellationToken ct = default);
    
    /// <summary>
    /// 获取所有记忆
    /// </summary>
    Task<List<MemoryItem>> GetAllAsync(string? userId = null, int limit = 100, CancellationToken ct = default);
    
    /// <summary>
    /// 获取指定记忆
    /// </summary>
    Task<MemoryItem?> GetAsync(string memoryId, CancellationToken ct = default);
    
    /// <summary>
    /// 更新记忆
    /// </summary>
    Task<bool> UpdateAsync(string memoryId, string content, CancellationToken ct = default);
    
    /// <summary>
    /// 删除记忆
    /// </summary>
    Task DeleteAsync(string memoryId, CancellationToken ct = default);
    
    /// <summary>
    /// 删除所有记忆
    /// </summary>
    Task DeleteAllAsync(string userId, CancellationToken ct = default);
}

