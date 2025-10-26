using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;

namespace MemNet.Abstractions;

/// <summary>
/// Embedding generator interface
/// </summary>
public interface IEmbedder
{
    /// <summary>
    /// Generate vector embedding for text
    /// </summary>
    Task<float[]> EmbedAsync(string text, CancellationToken ct = default);
    
    /// <summary>
    /// Batch generate vector embeddings
    /// </summary>
    Task<List<float[]>> EmbedBatchAsync(List<string> texts, CancellationToken ct = default);
}
