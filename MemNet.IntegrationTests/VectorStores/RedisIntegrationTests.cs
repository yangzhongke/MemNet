using System.Threading.Tasks;
using MemNet.Config;
using MemNet.IntegrationTests.Base;
using MemNet.Redis;
using Microsoft.Extensions.Options;
using StackExchange.Redis;
using Xunit;

namespace MemNet.IntegrationTests.VectorStores;

/// <summary>
/// Integration tests for RedisVectorStore
/// </summary>
public class RedisIntegrationTests : VectorStoreTestBase<RedisVectorStore>
{
    private IConnectionMultiplexer? _redis;

    protected override RedisVectorStore CreateVectorStore()
    {
        var endpoint = TestConfiguration.GetRedisEndpoint();
        _redis = ConnectionMultiplexer.Connect(endpoint);

        var config = Options.Create(new MemoryConfig
        {
            VectorStore = new VectorStoreConfig
            {
                Endpoint = endpoint,
                CollectionName = GenerateUniqueCollectionName(),
            }
        });

        return new RedisVectorStore(_redis, config);
    }

    protected override async Task CleanupVectorStoreAsync(RedisVectorStore vectorStore)
    {
        if (_redis != null)
        {
            var db = _redis.GetDatabase();
            var server = _redis.GetServer(_redis.GetEndPoints()[0]);
            
            // Clean up test keys
            var keys = server.Keys(pattern: $"{vectorStore.GetType().Name}*");
            foreach (var key in keys)
            {
                await db.KeyDeleteAsync(key);
            }

            await _redis.CloseAsync();
            _redis.Dispose();
        }
        
        await base.CleanupVectorStoreAsync(vectorStore);
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

