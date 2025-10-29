using System.Net.Http;
using System.Threading.Tasks;
using MemNet.Config;
using MemNet.IntegrationTests.Base;
using MemNet.VectorStores;
using Microsoft.Extensions.Options;
using Xunit;

namespace MemNet.IntegrationTests.VectorStores;

/// <summary>
/// Integration tests for QdrantVectorStore
/// </summary>
public class QdrantIntegrationTests : VectorStoreTestBase<QdrantVectorStore>
{
    protected override QdrantVectorStore CreateVectorStore()
    {
        var httpClient = new HttpClient();
        var config = Options.Create(new MemoryConfig
        {
            VectorStore = new VectorStoreConfig
            {
                Endpoint = TestConfiguration.GetQdrantEndpoint(),
                CollectionName = GenerateUniqueCollectionName(),
                ApiKey = "" // Optional authentication
            }
        });

        return new QdrantVectorStore(httpClient, config);
    }

    [Fact]
    public async Task EnsureCollectionExists_ShouldCreateCollection()
    {
        await TestEnsureCollectionExistsAsync();
    }

    [Fact]
    public async Task InsertAndRetrieve_ShouldWorkCorrectly()
    {
        await TestInsertAndRetrieveAsync();
    }

    [Fact]
    public async Task VectorSearch_ShouldReturnRelevantResults()
    {
        await TestVectorSearchAsync();
    }

    [Fact]
    public async Task UpdateMemory_ShouldUpdateSuccessfully()
    {
        await TestUpdateMemoryAsync();
    }

    [Fact]
    public async Task DeleteMemory_ShouldRemoveMemory()
    {
        await TestDeleteMemoryAsync();
    }

    [Fact]
    public async Task ListMemories_ShouldFilterByUser()
    {
        await TestListMemoriesAsync();
    }

    [Fact]
    public async Task BatchOperations_ShouldHandleMultipleMemories()
    {
        await TestBatchOperationsAsync();
    }
}

