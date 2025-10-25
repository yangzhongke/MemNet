using MemNet.Models;

namespace MemNet.Abstractions;

/// <summary>
///     LLM 提供者接口（复刻 Mem0 llms/base.py）
/// </summary>
public interface ILLMProvider
{
    /// <summary>
    ///     从消息中提取结构化记忆
    /// </summary>
    Task<List<ExtractedMemory>> ExtractMemoriesAsync(string messages, CancellationToken ct = default);

    /// <summary>
    ///     合并两条记忆
    /// </summary>
    Task<string> MergeMemoriesAsync(string existing, string newMemory, CancellationToken ct = default);

    /// <summary>
    ///     重排序搜索结果
    /// </summary>
    Task<List<MemorySearchResult>> RerankAsync(string query, List<MemorySearchResult> results,
        CancellationToken ct = default);

    /// <summary>
    ///     从文本中提取实体和关系
    /// </summary>
    Task<List<EntityRelation>> ExtractEntitiesAsync(string text, CancellationToken ct = default);
}