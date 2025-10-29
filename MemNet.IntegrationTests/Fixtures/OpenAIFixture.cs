using System.Net.Http;
using System.Threading.Tasks;
using MemNet.Abstractions;
using MemNet.Config;
using MemNet.Embedders;
using MemNet.IntegrationTests.Base;
using MemNet.LLMs;
using Microsoft.Extensions.Options;

namespace MemNet.IntegrationTests.Fixtures;

/// <summary>
/// OpenAI services fixture
/// </summary>
public class OpenAIFixture
{
    public IEmbedder Embedder { get; }
    public ILLMProvider LLMProvider { get; }

    public OpenAIFixture()
    {
        var httpClientEmbedder = new HttpClient();
        
        // Setup Embedder
        var embedderConfig = Options.Create(new MemoryConfig
        {
            Embedder = new EmbedderConfig
            {
                ApiKey = TestConfiguration.GetEmbedderApiKey(),
                Endpoint = TestConfiguration.GetEmbedderEndpoint(),
                Model = TestConfiguration.GetEmbedderModel(),
            }
        });
        Embedder = new OpenAIEmbedder(httpClientEmbedder, embedderConfig);
        
        var httpClientLLM = new HttpClient();
        // Setup LLM Provider
        var llmConfig = Options.Create(new MemoryConfig
        {
            LLM = new LLMConfig
            {
                ApiKey = TestConfiguration.GetLLMApiKey(),
                Endpoint = TestConfiguration.GetLLMEndpoint(),
                Model = TestConfiguration.GetLLMModel()
            }
        });
        LLMProvider = new OpenAIProvider(httpClientLLM, llmConfig);
    }

    public async Task<int> GetEmbeddingDimensionAsync()
    {
        return await Embedder.GetVectorSizeAsync();
    }
}

