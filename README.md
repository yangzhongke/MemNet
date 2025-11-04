# MemNet

MemNet is a "self-improving" memory layer designed for .NET developers, providing long-term and short-term memory management, similarity search, memory consolidation/refinement, and persistence for LLM-based applications. It facilitates context reuse in conversations, recommendations, personalization, and context-aware applications.

In simple terms: LLMs are stateless, and MemNet helps you remember users' previous behaviors and conversation content, making your applications smarter and more personalized.

## Why Use MemNet

- Transform scattered conversations/events into retrievable memories to enhance context awareness.
- Built-in vector search and memory consolidation strategies to reduce duplicate memories and noise.
- Support for multiple storage backends (in-memory, Qdrant, Redis, Chroma, Milvus, etc.) for easy expansion and persistence.
- Integration with any LLM/Embedding provider (pluggable embedding layer).

## Installation

Using dotnet CLI:
```bash
dotnet add package MemNet
```

Using NuGet Package Manager Console:
```powershell
Install-Package MemNet
```

## Quick Start

### 1. Configure appsettings.json

First, configure the Embedder, LLM, and VectorStore in your project's `appsettings.json` file:

```json
{
  "MemNet": {
    "Embedder": {
      "Endpoint": "https://api.openai.com/v1/",
      "Model": "text-embedding-3-large",
      "ApiKey": "your-embedding-api-key"
    },
    "LLM": {
      "Endpoint": "https://api.openai.com/v1/",
      "Model": "gpt-4",
      "ApiKey": "your-llm-api-key"
    }
  }
}
```

> **Tip:** For security purposes, it's recommended to use more secure methods to store API keys rather than writing them directly in the configuration file.

### 2. Register Services

Register MemNet services in `Program.cs`:

```csharp
using MemNet;
using MemNet.Abstractions;
using MemNet.Models;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;

var configuration = new ConfigurationBuilder()
    .AddJsonFile("appsettings.json") // Requires NuGet Package: Microsoft.Extensions.Configuration.Json
    .Build();

var services = new ServiceCollection();
services.AddMemNet(configuration);

await using var serviceProvider = services.BuildServiceProvider();
var memoryService = serviceProvider.GetRequiredService<IMemoryService>();
await memoryService.InitializeAsync();
```

### 3. Add Memories

```csharp
await memoryService.AddAsync(new AddMemoryRequest
{
    Messages =
    [
        new MessageContent
        {
            Role = "User",
            Content = "My name is Zack. I love programming."
        },
        new MessageContent
        {
            Role = "User",
            Content = "As a 18-years-old boy, I'm into Chinese food."
        },
        new MessageContent
        {
            Role = "User",
            Content = "I'm allergic to nuts."
        }
    ],
    UserId = "user001"
});
```

UserId is used to distinguish memory data between different users. It also supports finer-grained differentiation through dimensions such as AgentId and RunId.

### 4. Search Memories

```csharp
var searchResults = await memoryService.SearchAsync(new SearchMemoryRequest
{
    Query = "Please recommend some food.",
    UserId = "user001"
});

Console.WriteLine("Search Results:");
foreach (var item in searchResults)
{
    Console.WriteLine($"- {item.Memory.Data}");
}
```

Execution result:
```
Search Results:
- Cuisine preference: Chinese food
- Allergy: nuts
```

### 5. Using Different Vector Stores

MemNet uses in-memory vector storage by default, which is suitable for development and testing environments. For production environments, it's recommended to use persistent vector storage backends to ensure data persistence and scalability.

MemNet supports multiple vector storage backends:

#### Using Qdrant

Add vector database configuration to appsettings.json (replace the values with your actual values):
```
"VectorStore": {
    "Endpoint": "your-Qdrant-endpoint, e.g., http://localhost:6333",
    "ApiKey": "your-Qdrant-apikey (optional)",
    "CollectionName": "your-collection-name (optional, default is 'memnet_collection')"
}
```

Then modify the registration code:
```csharp
services.AddMemNet(configuration).WithQdrant();
```

#### Using Chroma

Add vector database configuration to appsettings.json (replace the values with your actual values):
```
"VectorStore": {
    "Endpoint": "your-Chroma-endpoint, e.g., http://localhost:8000",
    "ApiKey": "your-Chroma-apikey (optional)",
    "CollectionName": "your-collection-name (optional, default is 'memnet_collection')",
    "Database": "YourDatabaseName",
    "Tenant": "YourTenantName"
}
```

Then modify the registration code:
```csharp
services.AddMemNet(configuration).WithChromaV2();
```

#### Using Milvus

Add vector database configuration to appsettings.json (replace the values with your actual values):
```
"VectorStore": {
    "Endpoint": "your-Milvus-endpoint, e.g., http://localhost:19530",
    "ApiKey": "your-Milvus-apikey (optional)",
    "CollectionName": "your-collection-name (optional, default is 'memnet_collection')"
}
```

Then modify the registration code:
```csharp
services.AddMemNet(configuration).WithMilvusV2();
```

#### Using Redis (Requires MemNet.Redis package)

Add vector database configuration to appsettings.json (replace the values with your actual values):
```
"VectorStore": {
    "Endpoint": "your-Redis-address, e.g., localhost:6379",
    "ApiKey": "your-Redis-username-and-password (optional), e.g. user:password",
    "CollectionName": "your-collection-name (optional, default is 'memnet_collection')"
}
```

```csharp
services.AddMemNet(configuration).WithMemNetRedis("connection-string, e.g., localhost:6379");
```

#### More Vector Store Support

Please refer to the MemNet.Redis project to customize support for other vector stores. Contributions via pull requests to add new integrations are welcome.