using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;

namespace MemNet.Abstractions;

/// <summary>
/// 嵌入生成器接口（复刻 Mem0 embeddings/base.py）
/// </summary>
public interface IEmbedder
{
    /// <summary>
    /// 生成文本的向量嵌入
    /// </summary>
    Task<float[]> EmbedAsync(string text, CancellationToken ct = default);
    
    /// <summary>
    /// 批量生成向量嵌入
    /// </summary>
    Task<List<float[]>> EmbedBatchAsync(List<string> texts, CancellationToken ct = default);
}

