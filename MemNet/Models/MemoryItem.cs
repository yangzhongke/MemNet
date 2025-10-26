using System;
using System.Collections.Generic;

namespace MemNet.Models;

/// <summary>
/// 记忆项实体
/// </summary>
public class MemoryItem
{
    public string Id { get; set; } = Guid.NewGuid().ToString();
    public string Data { get; set; } = string.Empty;
    public float[] Embedding { get; set; } = Array.Empty<float>();
    public string? UserId { get; set; }
    public string? AgentId { get; set; }
    public string? RunId { get; set; }
    public Dictionary<string, object> Metadata { get; set; } = new();
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
    public DateTime? UpdatedAt { get; set; }
    public string? Hash { get; set; }
}

