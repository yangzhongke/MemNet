using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using MemNet.Models;

namespace MemNet.Abstractions;

/// <summary>
///     LLM provider interface (replicating Mem0 llms/base.py)
/// </summary>
public interface ILLMProvider
{
    /// <summary>
    ///     Extract structured memories from messages
    /// </summary>
    Task<List<ExtractedMemory>> ExtractMemoriesAsync(string messages, CancellationToken ct = default);

    /// <summary>
    ///     Merge two memories
    /// </summary>
    Task<string> MergeMemoriesAsync(string existing, string newMemory, CancellationToken ct = default);

    /// <summary>
    ///     Rerank search results
    /// </summary>
    Task<List<MemorySearchResult>> RerankAsync(string query, List<MemorySearchResult> results,
        CancellationToken ct = default);
}