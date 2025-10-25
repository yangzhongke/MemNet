namespace MemNet.Config;

/// <summary>
/// 记忆服务配置
/// </summary>
public class MemoryConfig
{
    public VectorStoreConfig VectorStore { get; set; } = new();
    public LLMConfig LLM { get; set; } = new();
    public EmbedderConfig Embedder { get; set; } = new();
    public GraphStoreConfig? GraphStore { get; set; }

    /// <summary>
    /// 去重阈值（余弦相似度）
    /// </summary>
    public float DuplicateThreshold { get; set; } = 0.9f;

    /// <summary>
    /// 是否启用重排序
    /// </summary>
    public bool EnableReranking { get; set; } = false;

    /// <summary>
    /// 历史消息限制
    /// </summary>
    public int HistoryLimit { get; set; } = 10;
}

public class VectorStoreConfig
{
    public string Provider { get; set; } = "qdrant"; // qdrant, milvus, chroma, inmemory
    public string Host { get; set; } = "localhost";
    public int Port { get; set; } = 6333;
    public string CollectionName { get; set; } = "memories";
    public int EmbeddingDimension { get; set; } = 1536;
    public string? ApiKey { get; set; }
}

public class LLMConfig
{
    public string Provider { get; set; } = "openai";
    public string Model { get; set; } = "gpt-4";
    public string? ApiKey { get; set; }
    public string? Endpoint { get; set; }
    public float Temperature { get; set; } = 0.7f;
    public int MaxTokens { get; set; } = 2000;
}

public class EmbedderConfig
{
    public string Provider { get; set; } = "openai";
    public string Model { get; set; } = "text-embedding-3-small";
    public string? ApiKey { get; set; }
    public string? Endpoint { get; set; }
    public int Dimension { get; set; } = 1536;
}

public class GraphStoreConfig
{
    public string Provider { get; set; } = "neo4j";
    public string Uri { get; set; } = "bolt://localhost:7687";
    public string? Username { get; set; }
    public string? Password { get; set; }
}