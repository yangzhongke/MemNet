using System.Text.Json.Serialization;

namespace MemNet.Models;

/// <summary>
/// Memory extracted by LLM
/// </summary>
public class ExtractedMemory
{
    [JsonPropertyName("data")] public string Data { get; set; } = string.Empty;
}