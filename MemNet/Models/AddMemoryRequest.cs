using System.Collections.Generic;

namespace MemNet.Models;

/// <summary>
/// 添加记忆请求
/// </summary>
public class AddMemoryRequest
{
    public List<MessageContent> Messages { get; set; } = new();
    public string? UserId { get; set; }
    public string? AgentId { get; set; }
    public string? RunId { get; set; }
    public Dictionary<string, object>? Metadata { get; set; }
    public List<string>? Filters { get; set; }
    public bool? Prompt { get; set; }
}

