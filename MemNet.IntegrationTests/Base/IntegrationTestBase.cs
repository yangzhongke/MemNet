using System;
using System.Threading.Tasks;
using MemNet.IntegrationTests.Fixtures;
using Xunit;

namespace MemNet.IntegrationTests.Base;

/// <summary>
/// Base class for all integration tests
/// </summary>
public abstract class IntegrationTestBase : IAsyncLifetime
{
    protected readonly DockerComposeFixture DockerFixture;
    protected readonly OpenAIFixture OpenAIFixture;

    protected IntegrationTestBase()
    {
        DockerFixture = new DockerComposeFixture();
        OpenAIFixture = new OpenAIFixture();
    }

    public virtual async Task InitializeAsync()
    {
        // Start Docker containers once
        await DockerFixture.StartAsync();
        
        // Verify OpenAI connection
        await VerifyOpenAIConnection();
    }

    public virtual Task DisposeAsync()
    {
        // Docker cleanup is handled by the fixture
        return Task.CompletedTask;
    }

    private async Task VerifyOpenAIConnection()
    {
        try
        {
            // Test embedder
            var testVector = await OpenAIFixture.Embedder.EmbedAsync("test");
            if (testVector == null || testVector.Length == 0)
            {
                throw new InvalidOperationException("Embedder returned empty vector");
            }
        }
        catch (Exception ex)
        {
            throw new InvalidOperationException(
                "Failed to connect to OpenAI. Please check your API key.", ex);
        }
    }

    protected string GenerateUniqueUserId()
    {
        return $"user_{Guid.NewGuid():N}";
    }

    protected string GenerateUniqueCollectionName()
    {
        return $"test_col_{Guid.NewGuid():N}";
    }
}

