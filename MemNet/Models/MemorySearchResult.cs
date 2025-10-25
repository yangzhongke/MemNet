namespace MemNet.Models;

/// <summary>
/// 搜索记忆结果
/// </summary>
public class MemorySearchResult
{
    public string Id { get; set; } = string.Empty;
    public MemoryItem Memory { get; set; } = new();
    public float Score { get; set; }
}

