namespace MemNet.Models;

/// <summary>
/// Memory search result
/// </summary>
public class MemorySearchResult
{
    public string Id { get; set; } = string.Empty;
    public MemoryItem Memory { get; set; } = new();
    public float Score { get; set; }
}
