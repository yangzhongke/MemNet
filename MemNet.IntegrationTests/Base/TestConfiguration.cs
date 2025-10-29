using System;
using System.IO;
using Microsoft.Extensions.Configuration;

namespace MemNet.IntegrationTests.Base;

/// <summary>
/// Test configuration manager
/// </summary>
public class TestConfiguration
{
    private static readonly Lazy<IConfiguration> _configuration = new(() =>
    {
        return new ConfigurationBuilder()
            .SetBasePath(Directory.GetCurrentDirectory())
            .AddJsonFile("appsettings.test.json", optional: true)
            .AddEnvironmentVariables()
            .Build();
    });

    public static IConfiguration Configuration => _configuration.Value;

    public static string GetLLMApiKey()
    {
        var key = Environment.GetEnvironmentVariable("LLM_API_KEY");
        
        if (string.IsNullOrEmpty(key))
        {
            throw new InvalidOperationException(
                "LLM API Key not configured. Set LLM_API_KEY environment variable.");
        }

        return key;
    }
    
    public static string GetLLMEndpoint()
    {
        var key = Configuration["LLM:Endpoint"];
        
        if (string.IsNullOrEmpty(key))
        {
            throw new InvalidOperationException(
                "OpenAI API Key not configured. Set 'LLM:Endpoint' in appsettings.test.json");
        }

        return key;
    }
    
    public static string GetLLMModel()
    {
        return Configuration["LLM:Model"] ?? "gpt-4o-mini";
    }
    
    public static string GetEmbedderApiKey()
    {
        var key = Environment.GetEnvironmentVariable("EMBEDDER_API_KEY");
        
        if (string.IsNullOrEmpty(key))
        {
            throw new InvalidOperationException(
                "LLM API Key not configured. Set EMBEDDER_API_KEY environment variable.");
        }

        return key;
    }
    
    public static string GetEmbedderEndpoint()
    {
        var key = Configuration["Embedder:Endpoint"];
        
        if (string.IsNullOrEmpty(key))
        {
            throw new InvalidOperationException(
                "OpenAI API Key not configured. Set 'Embedder:Endpoint' in appsettings.test.json");
        }

        return key;
    }

    public static string GetEmbedderModel()
    {
        return Configuration["Embedder:Model"] ?? "text-embedding-3-small";
    }

    

    public static string GetChromaEndpoint()
    {
        return Configuration["Chroma:Endpoint"] 
               ?? Environment.GetEnvironmentVariable("TEST_CHROMA_ENDPOINT") 
               ?? "http://localhost:8000";
    }

    public static string GetMilvusEndpoint()
    {
        return Configuration["Milvus:Endpoint"] 
               ?? Environment.GetEnvironmentVariable("TEST_MILVUS_ENDPOINT") 
               ?? "http://localhost:19530";
    }

    public static string GetQdrantEndpoint()
    {
        return Configuration["Qdrant:Endpoint"] 
               ?? Environment.GetEnvironmentVariable("TEST_QDRANT_ENDPOINT") 
               ?? "http://localhost:6333";
    }
}

