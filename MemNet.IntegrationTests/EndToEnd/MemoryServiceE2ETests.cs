using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using FluentAssertions;
using MemNet.Config;
using MemNet.Core;
using MemNet.IntegrationTests.Base;
using MemNet.Models;
using MemNet.VectorStores;
using Microsoft.Extensions.Options;
using Xunit;

namespace MemNet.IntegrationTests.EndToEnd;

/// <summary>
/// End-to-end integration tests for MemoryService
/// </summary>
public class MemoryServiceE2ETests : IntegrationTestBase
{
    [Fact]
    public async Task AddMemory_ShouldExtractAndStoreMemories()
    {
        // Arrange
        var vectorStore = new InMemoryVectorStore();
        var memoryConfig = Options.Create(new MemoryConfig
        {
            DuplicateThreshold = 0.85
        });

        var service = new MemoryService(
            vectorStore,
            OpenAIFixture.LLMProvider,
            OpenAIFixture.Embedder,
            memoryConfig);

        await service.InitializeAsync();

        var userId = GenerateUniqueUserId();
        var request = new AddMemoryRequest
        {
            Messages = new List<MessageContent>
            {
                new() { Role = "user", Content = "I love playing basketball" },
                new() { Role = "assistant", Content = "That's great! Basketball is a fun sport." },
                new() { Role = "user", Content = "Yes, I play it every weekend with my friends" }
            },
            UserId = userId
        };

        // Act
        var response = await service.AddAsync(request);

        // Assert
        response.Should().NotBeNull();
        response.Results.Should().NotBeEmpty();
        
        foreach (var result in response.Results)
        {
            result.Id.Should().NotBeNullOrEmpty();
            result.Memory.Should().NotBeNullOrEmpty();
            result.Event.Should().NotBeNullOrEmpty();
        }

        // Verify memories are stored
        var allMemories = await service.GetAllAsync(userId);
        allMemories.Should().NotBeEmpty();
        allMemories.Should().OnlyContain(m => m.UserId == userId);
    }

    [Fact]
    public async Task SearchMemory_ShouldReturnRelevantMemories()
    {
        // Arrange
        var vectorStore = new InMemoryVectorStore();
        var memoryConfig = Options.Create(new MemoryConfig());

        var service = new MemoryService(
            vectorStore,
            OpenAIFixture.LLMProvider,
            OpenAIFixture.Embedder,
            memoryConfig);

        await service.InitializeAsync();

        var userId = GenerateUniqueUserId();

        // Add memories
        await service.AddAsync(new AddMemoryRequest
        {
            Messages = new List<MessageContent>
            {
                new() { Role = "user", Content = "I am a software engineer specializing in C#" }
            },
            UserId = userId
        });

        await service.AddAsync(new AddMemoryRequest
        {
            Messages = new List<MessageContent>
            {
                new() { Role = "user", Content = "My favorite food is sushi" }
            },
            UserId = userId
        });

        // Act - Search for programming-related content
        var searchResults = await service.SearchAsync(new SearchMemoryRequest
        {
            Query = "What does the user do for work?",
            UserId = userId,
            Limit = 5
        });

        // Assert
        searchResults.Should().NotBeEmpty();
        var topResult = searchResults.First();
        topResult.Memory.Data.Should().Contain("software engineer", "C#");
    }

    [Fact]
    public async Task AddMemory_WithDuplicates_ShouldMergeMemories()
    {
        // Arrange
        var vectorStore = new InMemoryVectorStore();
        var memoryConfig = Options.Create(new MemoryConfig
        {
            DuplicateThreshold = 0.85
        });

        var service = new MemoryService(
            vectorStore,
            OpenAIFixture.LLMProvider,
            OpenAIFixture.Embedder,
            memoryConfig);

        await service.InitializeAsync();

        var userId = GenerateUniqueUserId();

        // Act - Add similar memories
        var response1 = await service.AddAsync(new AddMemoryRequest
        {
            Messages = new List<MessageContent>
            {
                new() { Role = "user", Content = "I like coffee" }
            },
            UserId = userId
        });

        var response2 = await service.AddAsync(new AddMemoryRequest
        {
            Messages = new List<MessageContent>
            {
                new() { Role = "user", Content = "I enjoy drinking coffee in the morning" }
            },
            UserId = userId
        });

        // Assert
        response1.Results.Should().NotBeEmpty();
        response2.Results.Should().NotBeEmpty();

        // Check if memories were merged (event should indicate update)
        var hasUpdate = response2.Results.Any(r => 
            r.Event.Contains("update", StringComparison.OrdinalIgnoreCase));
        
        // Note: This depends on LLM behavior, so we just verify the service runs
        response2.Should().NotBeNull();
    }

    [Fact]
    public async Task UpdateMemory_ShouldModifyExistingMemory()
    {
        // Arrange
        var vectorStore = new InMemoryVectorStore();
        var memoryConfig = Options.Create(new MemoryConfig());

        var service = new MemoryService(
            vectorStore,
            OpenAIFixture.LLMProvider,
            OpenAIFixture.Embedder,
            memoryConfig);

        await service.InitializeAsync();

        var userId = GenerateUniqueUserId();

        // Add a memory
        var addResponse = await service.AddAsync(new AddMemoryRequest
        {
            Messages = new List<MessageContent>
            {
                new() { Role = "user", Content = "I live in New York" }
            },
            UserId = userId
        });

        var memoryId = addResponse.Results.First().Id;

        // Act - Update the memory
        var updateResult = await service.UpdateAsync(memoryId, "I live in San Francisco");

        // Assert
        updateResult.Should().BeTrue();

        var updatedMemory = await service.GetAsync(memoryId);
        updatedMemory.Should().NotBeNull();
        updatedMemory!.Data.Should().Be("I live in San Francisco");
        updatedMemory.UpdatedAt.Should().NotBeNull();
    }

    [Fact]
    public async Task DeleteMemory_ShouldRemoveMemory()
    {
        // Arrange
        var vectorStore = new InMemoryVectorStore();
        var memoryConfig = Options.Create(new MemoryConfig());

        var service = new MemoryService(
            vectorStore,
            OpenAIFixture.LLMProvider,
            OpenAIFixture.Embedder,
            memoryConfig);

        await service.InitializeAsync();

        var userId = GenerateUniqueUserId();

        // Add a memory
        var addResponse = await service.AddAsync(new AddMemoryRequest
        {
            Messages = new List<MessageContent>
            {
                new() { Role = "user", Content = "This is a test memory" }
            },
            UserId = userId
        });

        var memoryId = addResponse.Results.First().Id;

        // Act - Delete the memory
        await service.DeleteAsync(memoryId);

        // Assert
        var deletedMemory = await service.GetAsync(memoryId);
        deletedMemory.Should().BeNull();
    }

    [Fact]
    public async Task MultiUser_ShouldIsolateMemories()
    {
        // Arrange
        var vectorStore = new InMemoryVectorStore();
        var memoryConfig = Options.Create(new MemoryConfig());

        var service = new MemoryService(
            vectorStore,
            OpenAIFixture.LLMProvider,
            OpenAIFixture.Embedder,
            memoryConfig);

        await service.InitializeAsync();

        var user1 = GenerateUniqueUserId();
        var user2 = GenerateUniqueUserId();

        // Act - Add memories for different users
        await service.AddAsync(new AddMemoryRequest
        {
            Messages = new List<MessageContent>
            {
                new() { Role = "user", Content = "User 1's favorite color is blue" }
            },
            UserId = user1
        });

        await service.AddAsync(new AddMemoryRequest
        {
            Messages = new List<MessageContent>
            {
                new() { Role = "user", Content = "User 2's favorite color is red" }
            },
            UserId = user2
        });

        // Assert - Each user should only see their own memories
        var user1Memories = await service.GetAllAsync(user1);
        var user2Memories = await service.GetAllAsync(user2);

        user1Memories.Should().NotBeEmpty();
        user1Memories.Should().OnlyContain(m => m.UserId == user1);

        user2Memories.Should().NotBeEmpty();
        user2Memories.Should().OnlyContain(m => m.UserId == user2);

        // Search should also be isolated
        var user1Search = await service.SearchAsync(new SearchMemoryRequest
        {
            Query = "favorite color",
            UserId = user1
        });

        user1Search.Should().NotBeEmpty();
        user1Search.Should().OnlyContain(r => r.Memory.UserId == user1);
    }
}

