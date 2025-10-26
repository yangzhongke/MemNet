using System.Collections.Generic;

namespace MemNet.Models;

/// <summary>
/// 添加记忆响应
/// </summary>
public class AddMemoryResponse
{
    public List<MemoryResult> Results { get; set; } = new();
}

public class MemoryResult
{
    public string Id { get; set; } = string.Empty;
    public string Memory { get; set; } = string.Empty;
    public string Event { get; set; } = string.Empty; // "add" or "update"
}

