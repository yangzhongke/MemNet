using System.Text.Json.Serialization;

namespace MemNet.Models;

/// <summary>
/// LLM 提取的记忆
/// </summary>
public class ExtractedMemory
{
    [JsonPropertyName("data")] public string Data { get; set; } = string.Empty;
}