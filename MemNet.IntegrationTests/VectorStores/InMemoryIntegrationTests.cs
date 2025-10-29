using System.Threading.Tasks;
using MemNet.IntegrationTests.Base;
using MemNet.VectorStores;
using Xunit;

namespace MemNet.IntegrationTests.VectorStores;

/// <summary>
/// Integration tests for InMemoryVectorStore
/// </summary>
public class InMemoryIntegrationTests : VectorStoreTestBase<InMemoryVectorStore>
{
    protected override InMemoryVectorStore CreateVectorStore()
    {
        return new InMemoryVectorStore();
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

