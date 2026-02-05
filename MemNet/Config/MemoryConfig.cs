namespace MemNet.Config;

/// <summary>
/// Memory service configuration
/// </summary>
public class MemoryConfig
{
    public VectorStoreConfig VectorStore { get; set; } = new();
    public LLMConfig LLM { get; set; } = new();
    public EmbedderConfig Embedder { get; set; } = new();

    public JinaEmbederConfig JinaEmbedder { get; set; } = new();
    /// <summary>
    /// Duplicate threshold (cosine similarity)
    /// </summary>
    public double DuplicateThreshold { get; set; } = 0.6;

    /// <summary>
    /// Enable reranking
    /// </summary>
    public bool EnableReranking { get; set; } = true;

    /// <summary>
    /// History message limit
    /// </summary>
    public int HistoryLimit { get; set; } = 10;
}

public class VectorStoreConfig
{
    public string Endpoint { get; set; }
    public string CollectionName { get; set; } = "memnet_collection";
    public string? ApiKey { get; set; }
}

public class LLMConfig
{
    public string Model { get; set; } = "gpt-4";
    public string? ApiKey { get; set; }
    public string? Endpoint { get; set; }
    public int MaxTokens { get; set; } = 3000;
}

public class EmbedderConfig
{
    public string Model { get; set; } = "text-embedding-3-small";
    public string? ApiKey { get; set; }
    public string? Endpoint { get; set; }
}

public class JinaEmbederConfig
{
    public string Model { get; set; }
    public string Vocab { get; set; }
}