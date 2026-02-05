using System;
using System.Collections.Generic;
using System.Linq;
using System.Net.Http;
using System.Net.Http.Json;
using System.Text.Json;
using System.Text.Json.Serialization;
using System.Threading;
using System.Threading.Tasks;
using MemNet.Abstractions;
using MemNet.Config;
using MemNet.Internals;
using MemNet.Models;
using Microsoft.Extensions.Options;

namespace MemNet.LLMs;

/// <summary>
///     OpenAI LLM provider implementation (replicating Mem0 llms/openai.py)
/// </summary>
public class OpenAIProvider : ILLMProvider
{
    private readonly LLMConfig _config;
    private readonly HttpClient _httpClient;

    public OpenAIProvider(HttpClient httpClient, IOptions<MemoryConfig> config)
    {
        _httpClient = httpClient;
        _config = config.Value.LLM;

        // Configure HttpClient
        if (_httpClient.BaseAddress == null)
        {
            _httpClient.BaseAddress = new Uri(_config.Endpoint ?? "https://api.openai.com/v1/");
        }

        if (!_httpClient.DefaultRequestHeaders.Contains("Authorization"))
        {
            _httpClient.DefaultRequestHeaders.Add("Authorization", $"Bearer {_config.ApiKey}");
        }
    }

    public async Task<List<ExtractedMemory>> ExtractMemoriesAsync(string message,CancellationToken ct = default)
    {
        var systemPrompt = """
                           You are a memory extraction expert. Extract key facts, preferences, and important context from the conversation.
                           Return a JSON object with this structure:
                           {
                               "memories": [
                                   {"data": "extracted fact 1"},
                                   {"data": "extracted fact 2"},
                               ]
                           }

                           Only extract factual statements, user preferences, personal information, and important context.
                           Be concise and specific. Each memory should be a standalone fact.
                           """;

        var request = new
        {
            model = _config.Model,
            messages = new[]
            {
                new { role = "system", content = systemPrompt },
                new { role = "user", content = message }
            },
            response_format = new { type = "json_object" },
            max_tokens= _config.MaxTokens
        };

        var content = await CompleteChatAsync(ct, request);

        int think = content.IndexOf("</think>");
        if (think > 0)
        {
            content = content.Substring(think);
            int start = content.IndexOf("{");
            int end = content.LastIndexOf("}") + 1;
            if (start >= 0 && end > start)
                content = content.Substring(start, end - start);
        }
        else
        {
            int start = content.IndexOf("{");
            int end = content.LastIndexOf("}") + 1;
            if (start >= 0 && end > start)
                content = content.Substring(start,end - start);
        }

        if (string.IsNullOrWhiteSpace(content))
        {
            return new List<ExtractedMemory>();
        }
        MemoryExtractionResult? extraction = null;
        try
        {
             extraction = JsonSerializer.Deserialize<MemoryExtractionResult>(content);
        }
        catch (Exception ex)
        {
            
        }
        return extraction?.Memories ?? new List<ExtractedMemory>();
    }

    private async Task<string> CompleteChatAsync(CancellationToken ct, object request)
    {
        var response = await _httpClient.PostAsJsonAsync("chat/completions", request, ct);
        await response.EnsureSuccessWithContentAsync();
        
        var result =
            await response.Content.ReadFromJsonAsync<ChatCompletionResponse>(ct);
        var content = result?.Choices?[0].Message.Content?.Trim() ?? string.Empty;
       return content;
    }

    public async Task<string> MergeMemoriesAsync(string existing, string newMemory, CancellationToken ct = default)
    {
        var systemPrompt = $"""
                            You are a memory merging expert. You can merge two memory statements into one coherent, accurate statement.

                            Rules:
                            1. Preserve all factual information from both.
                            2. If they conflict, prefer the newer information.
                            3. Merge them into a concise, and non-redundant factual summary, keeping all unique information.
                            4. Return ONLY the merged memory text, no explanation.
                            5. All of "I", "User", "My" and "Me" refer to the user.
                            
                            Example:
                            Existing Memory: I love jogging; 
                            New Memory: My interest in jogging.
                            Output: I love jogging.
                            """;
        
        string userMessage = $"""
                             Existing Memory: "{existing}"

                             New Memory: "{newMemory}"
                             """;

        var request = new
        {
            model = _config.Model,
            messages = new[]
            {
                new { role = "system", content = systemPrompt },
                new { role = "user", content = userMessage}
            }
        };

        var content = await CompleteChatAsync(ct, request);
        return content;
    }

    public async Task<List<MemorySearchResult>> RerankAsync(string query, List<MemorySearchResult> results,
        CancellationToken ct = default)
    {
        if (results.Count == 0)
        {
            return results;
        }

        var memoriesText = string.Join("\n", results.Select((r, i) =>
            $"{i}. {r.Memory.Data}"));

        var systemPrompt = $$"""
                             Given the query: "{{query}}"

                             Rank these memories by relevance (most relevant first):
                             {{memoriesText}}

                             Return a JSON object with this structure:
                             {
                                 "ranked_indices": [0, 2, 1, ...]
                             }

                             Only return indices of relevant memories, omit irrelevant ones.
                             """;
        //"ranked_indices": [0, 2, 1, ...]
        var request = new
        {
            model = _config.Model,
            messages = new[]
            {
                new { role = "system", content = systemPrompt },
            },
            response_format = new { type = "json_object" },
            max_tokens = _config.MaxTokens
        };

        var content = await CompleteChatAsync(ct, request);

        int think = content.IndexOf("</think>");
        if (think > 0)
        {
            content = content.Substring(think);
            int start = content.IndexOf("{");
            int end = content.LastIndexOf("}") + 1;
            if (start >= 0 && end > start)
                content = content.Substring(start, end - start);
        }
        else
        {
            int start = content.IndexOf("{");
            int end = content.LastIndexOf("}") + 1;
            if (start >= 0 && end > start)
                content = content.Substring(start, end - start);
        }

        if (string.IsNullOrWhiteSpace(content))
        {
            //return results;
            return new List<MemorySearchResult>();
        }
        RankingResult? ranking = null;
        var reranked = new List<MemorySearchResult>();
        try
        {
            ranking = JsonSerializer.Deserialize<RankingResult>(content);
        }
        catch (Exception ex)
        {
            
        }

        if ( ranking?.RankedIndices == null )
        {
            //return results;
            return new List<MemorySearchResult>();
        }
               
        foreach (var index in ranking.RankedIndices)
        {
            if (index >= 0 && index < results.Count)
            {
                reranked.Add(results[index]);
            }
        }

        return reranked;
    }
   
    // Internal classes for JSON deserialization
    private class ChatCompletionResponse
    {
        [JsonPropertyName("choices")] public List<Choice> Choices { get; set; } = new();
    }

    private class Choice
    {
        [JsonPropertyName("message")] public Message Message { get; set; } = new();
    }

    private class Message
    {
        [JsonPropertyName("content")] public string Content { get; set; }
    }

    private class MemoryExtractionResult
    {
        [JsonPropertyName("memories")] public List<ExtractedMemory> Memories { get; set; } = new();
    }

    private class RankingResult
    {
        [JsonPropertyName("ranked_indices")] public List<int> RankedIndices { get; set; } = new();
    }
}