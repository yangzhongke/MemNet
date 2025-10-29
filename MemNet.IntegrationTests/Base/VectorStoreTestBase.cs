using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using FluentAssertions;
using MemNet.Abstractions;
using MemNet.Models;
using Xunit;

namespace MemNet.IntegrationTests.Base;

/// <summary>
/// Base class for vector store integration tests with reusable test scenarios
/// </summary>
/// <typeparam name="TVectorStore">The vector store implementation type</typeparam>
public abstract class VectorStoreTestBase<TVectorStore> : IntegrationTestBase
    where TVectorStore : IVectorStore
{
    protected abstract TVectorStore CreateVectorStore();

    protected virtual async Task CleanupVectorStoreAsync(TVectorStore vectorStore)
    {
        // Override in derived classes if cleanup is needed
        await Task.CompletedTask;
    }

    /// <summary>
    /// Test: Create collection with specific vector size
    /// </summary>
    protected async Task TestEnsureCollectionExistsAsync()
    {
        var vectorStore = CreateVectorStore();
        var vectorSize = await OpenAIFixture.GetEmbeddingDimensionAsync();

        // Should create collection successfully
        await vectorStore.EnsureCollectionExistsAsync(vectorSize, allowRecreation: true);
        
        // Should not throw when called again with same configuration
        await vectorStore.EnsureCollectionExistsAsync(vectorSize, allowRecreation: false);

        await CleanupVectorStoreAsync(vectorStore);
    }

    /// <summary>
    /// Test: Insert and retrieve memories
    /// </summary>
    protected async Task TestInsertAndRetrieveAsync()
    {
        var vectorStore = CreateVectorStore();
        var vectorSize = await OpenAIFixture.GetEmbeddingDimensionAsync();
        await vectorStore.EnsureCollectionExistsAsync(vectorSize, allowRecreation: true);

        var userId = GenerateUniqueUserId();
        var testData = "User loves programming in C#";
        var embedding = await OpenAIFixture.Embedder.EmbedAsync(testData);

        var memory = new MemoryItem
        {
            Id = Guid.NewGuid().ToString(),
            Data = testData,
            Embedding = embedding,
            UserId = userId,
            Metadata = new Dictionary<string, object> { { "category", "preference" } },
            CreatedAt = DateTime.UtcNow
        };

        // Insert memory
        await vectorStore.InsertAsync(new List<MemoryItem> { memory });

        // Retrieve by ID
        var retrieved = await vectorStore.GetAsync(memory.Id);
        retrieved.Should().NotBeNull();
        retrieved!.Data.Should().Be(testData);
        retrieved.UserId.Should().Be(userId);
        retrieved.Metadata.Should().ContainKey("category");

        await CleanupVectorStoreAsync(vectorStore);
    }

    /// <summary>
    /// Test: Vector similarity search
    /// </summary>
    protected async Task TestVectorSearchAsync()
    {
        var vectorStore = CreateVectorStore();
        var vectorSize = await OpenAIFixture.GetEmbeddingDimensionAsync();
        await vectorStore.EnsureCollectionExistsAsync(vectorSize, allowRecreation: true);

        var userId = GenerateUniqueUserId();

        // Insert multiple memories
        var memories = new List<MemoryItem>
        {
            await CreateMemoryItem("User loves C# programming", userId),
            await CreateMemoryItem("User enjoys Python coding", userId),
            await CreateMemoryItem("User likes pizza for dinner", userId)
        };

        await vectorStore.InsertAsync(memories);

        // Search for programming-related content
        var queryVector = await OpenAIFixture.Embedder.EmbedAsync("programming languages");
        var results = await vectorStore.SearchAsync(queryVector, userId, limit: 2);

        results.Should().NotBeEmpty();
        results.Should().HaveCountLessOrEqualTo(2);
        
        // The top result should be about programming
        var topResult = results.First();
        topResult.Memory.Data.Should().Contain("programming", "coding");
        topResult.Score.Should().BeGreaterThan(0);

        await CleanupVectorStoreAsync(vectorStore);
    }

    /// <summary>
    /// Test: Update memory
    /// </summary>
    protected async Task TestUpdateMemoryAsync()
    {
        var vectorStore = CreateVectorStore();
        var vectorSize = await OpenAIFixture.GetEmbeddingDimensionAsync();
        await vectorStore.EnsureCollectionExistsAsync(vectorSize, allowRecreation: true);

        var userId = GenerateUniqueUserId();
        var memory = await CreateMemoryItem("Original content", userId);
        
        await vectorStore.InsertAsync(new List<MemoryItem> { memory });

        // Update memory
        memory.Data = "Updated content";
        memory.Embedding = await OpenAIFixture.Embedder.EmbedAsync(memory.Data);
        memory.UpdatedAt = DateTime.UtcNow;

        await vectorStore.UpdateAsync(new List<MemoryItem> { memory });

        // Verify update
        var retrieved = await vectorStore.GetAsync(memory.Id);
        retrieved.Should().NotBeNull();
        retrieved!.Data.Should().Be("Updated content");
        retrieved.UpdatedAt.Should().NotBeNull();

        await CleanupVectorStoreAsync(vectorStore);
    }

    /// <summary>
    /// Test: Delete memory
    /// </summary>
    protected async Task TestDeleteMemoryAsync()
    {
        var vectorStore = CreateVectorStore();
        var vectorSize = await OpenAIFixture.GetEmbeddingDimensionAsync();
        await vectorStore.EnsureCollectionExistsAsync(vectorSize, allowRecreation: true);

        var userId = GenerateUniqueUserId();
        var memory = await CreateMemoryItem("Memory to delete", userId);
        
        await vectorStore.InsertAsync(new List<MemoryItem> { memory });

        // Verify it exists
        var retrieved = await vectorStore.GetAsync(memory.Id);
        retrieved.Should().NotBeNull();

        // Delete memory
        await vectorStore.DeleteAsync(memory.Id);

        // Verify it's deleted
        var afterDelete = await vectorStore.GetAsync(memory.Id);
        afterDelete.Should().BeNull();

        await CleanupVectorStoreAsync(vectorStore);
    }

    /// <summary>
    /// Test: List memories with user filtering
    /// </summary>
    protected async Task TestListMemoriesAsync()
    {
        var vectorStore = CreateVectorStore();
        var vectorSize = await OpenAIFixture.GetEmbeddingDimensionAsync();
        await vectorStore.EnsureCollectionExistsAsync(vectorSize, allowRecreation: true);

        var user1 = GenerateUniqueUserId();
        var user2 = GenerateUniqueUserId();

        // Insert memories for different users
        await vectorStore.InsertAsync(new List<MemoryItem>
        {
            await CreateMemoryItem("User1 memory 1", user1),
            await CreateMemoryItem("User1 memory 2", user1),
            await CreateMemoryItem("User2 memory 1", user2)
        });

        // List user1's memories
        var user1Memories = await vectorStore.ListAsync(user1, limit: 100);
        user1Memories.Should().HaveCount(2);
        user1Memories.Should().OnlyContain(m => m.UserId == user1);

        // List user2's memories
        var user2Memories = await vectorStore.ListAsync(user2, limit: 100);
        user2Memories.Should().HaveCount(1);
        user2Memories.Should().OnlyContain(m => m.UserId == user2);

        await CleanupVectorStoreAsync(vectorStore);
    }

    /// <summary>
    /// Test: Batch operations performance
    /// </summary>
    protected async Task TestBatchOperationsAsync()
    {
        var vectorStore = CreateVectorStore();
        var vectorSize = await OpenAIFixture.GetEmbeddingDimensionAsync();
        await vectorStore.EnsureCollectionExistsAsync(vectorSize, allowRecreation: true);

        var userId = GenerateUniqueUserId();
        var batchSize = 10;

        // Create batch of memories
        var memories = new List<MemoryItem>();
        for (int i = 0; i < batchSize; i++)
        {
            memories.Add(await CreateMemoryItem($"Batch memory {i}", userId));
        }

        // Insert batch
        await vectorStore.InsertAsync(memories);

        // Verify all inserted
        var allMemories = await vectorStore.ListAsync(userId, limit: 100);
        allMemories.Should().HaveCount(batchSize);

        await CleanupVectorStoreAsync(vectorStore);
    }

    protected async Task<MemoryItem> CreateMemoryItem(string data, string userId)
    {
        var embedding = await OpenAIFixture.Embedder.EmbedAsync(data);
        return new MemoryItem
        {
            Id = Guid.NewGuid().ToString(),
            Data = data,
            Embedding = embedding,
            UserId = userId,
            Metadata = new Dictionary<string, object>(),
            CreatedAt = DateTime.UtcNow
        };
    }
}

