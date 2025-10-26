namespace MemNet.Config;

/// <summary>
/// Memory service configuration
/// </summary>
public class MemoryConfig
{
    public VectorStoreConfig VectorStore { get; set; } = new();
    public LLMConfig LLM { get; set; } = new();
    public EmbedderConfig Embedder { get; set; } = new();

    /// <summary>
    /// Duplicate threshold (cosine similarity)
    /// </summary>
    public float DuplicateThreshold { get; set; } = 0.9f;

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
    public string Endpoint { get; set; } = "http://localhost:6333";
    public string CollectionName { get; set; } = "memories";
    public string? ApiKey { get; set; }
}

public class LLMConfig
{
    public string Model { get; set; } = "gpt-4";
    public string? ApiKey { get; set; }
    public string? Endpoint { get; set; }
}

public class EmbedderConfig
{
    public string Model { get; set; } = "text-embedding-3-small";
    public string? ApiKey { get; set; }
    public string? Endpoint { get; set; }
}