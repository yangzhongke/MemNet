# MemNet - C# Memory Library for LLMs

MemNet is a complete C# replication of Mem0, providing intelligent memory management for Large Language Models (LLM).

## Core Features

- **Intelligent Memory Extraction**: Automatically extract structured memories from conversations using LLM
- **Deduplication and Merging**: Automatically detect similar memories and intelligently merge them
- **Vector Semantic Search**: Semantic retrieval based on vector similarity
- **Knowledge Graph Storage**: Extract entities and relationships to build knowledge graphs (optional)
- **Multiple Storage Backends**: Support for in-memory storage, Qdrant, Milvus, etc.
- **Flexible Configuration**: Support for multiple LLM and Embedding providers

## Quick Start

### 1. Installation

```bash
dotnet add reference MemNet
```

### 2. Configuration (appsettings.json)

```json
{
  "MemNet": {
    "LLM": {
      "Provider": "openai",
      "Model": "gpt-4",
      "ApiKey": "your-openai-api-key"
    },
    "Embedder": {
      "Provider": "openai",
      "Model": "text-embedding-3-small",
      "ApiKey": "your-openai-api-key"
    },
    "VectorStore": {
      "Provider": "inmemory",
      "CollectionName": "memories"
    },
    "GraphStore": {
      "Provider": "inmemory"
    },
    "DuplicateThreshold": 0.9,
    "EnableReranking": false
  }
}
```

### 3. Register Services

```csharp
using MemNet;

var builder = WebApplication.CreateBuilder(args);

// Register MemNet from configuration file
builder.Services.AddMemNet(builder.Configuration);

// Optional: Enable knowledge graph storage
builder.Services.WithGraphStore();

var app = builder.Build();
```

Or use code configuration:

```csharp
builder.Services.AddMemNet(config =>
{
    config.LLM = new LLMConfig
    {
        Provider = "openai",
        Model = "gpt-4",
        ApiKey = "your-api-key"
    };
    config.Embedder = new EmbedderConfig
    {
        Provider = "openai",
        Model = "text-embedding-3-small",
        ApiKey = "your-api-key"
    };
    config.GraphStore = new GraphStoreConfig
    {
        Provider = "inmemory"
    };
})
.WithGraphStore(); // Enable knowledge graph storage
```

### 4. Usage Example

```csharp
using MemNet.Abstractions;
using MemNet.Models;

public class ChatService
{
    private readonly IMemoryService _memoryService;

    public ChatService(IMemoryService memoryService)
    {
        _memoryService = memoryService;
    }

    public async Task ProcessConversation(string userId, string userMessage)
    {
        // 1. Add memory
        var addRequest = new AddMemoryRequest
        {
            Messages = new List<MessageContent>
            {
                new() { Role = "user", Content = userMessage }
            },
            UserId = userId
        };

        var result = await _memoryService.AddAsync(addRequest);
        Console.WriteLine($"Added {result.Results.Count} memories");

        // 2. Search related memories
        var searchRequest = new SearchMemoryRequest
        {
            Query = userMessage,
            UserId = userId,
            Limit = 5
        };

        var memories = await _memoryService.SearchAsync(searchRequest);
        foreach (var memory in memories)
        {
            Console.WriteLine($"Memory: {memory.Memory.Data} (Similarity: {memory.Score})");
        }

        // 3. Get all memories
        var allMemories = await _memoryService.GetAllAsync(userId);
        Console.WriteLine($"User has {allMemories.Count} memories");
    }
}
```

## Architecture Design

### Core Components

```
MemNet
├── Abstractions/          # Core interfaces
│   ├── IMemoryService     # Memory service interface
│   ├── ILLMProvider       # LLM provider interface
│   ├── IEmbedder          # Embedding generator interface
│   └── IVectorStore       # Vector store interface
├── Core/                  # Core implementation
│   └── MemoryService      # Memory service implementation
├── LLMs/                  # LLM providers
│   └── OpenAIProvider     # OpenAI implementation
├── Embedders/             # Embedding generators
│   └── OpenAIEmbedder     # OpenAI embedding implementation
├── VectorStores/          # Vector stores
│   └── InMemoryVectorStore # In-memory storage
├── Models/                # Data models
└── Config/                # Configuration classes
```

### Workflow

1. **Add Memory Flow**:
    - Receive user messages
    - Extract structured memories using LLM
    - Generate vector embeddings
    - Check for similar memories
    - Merge or insert new memories

2. **Search Memory Flow**:
    - Generate query vector
    - Vector similarity search
    - Optional LLM reranking
    - Return relevant memories

## Extensibility

### Custom Vector Store

```csharp
public class QdrantVectorStore : IVectorStore
{
    // Implement IVectorStore interface
}

// Register custom store
builder.Services.AddMemNet(config)
    .WithVectorStore<QdrantVectorStore>();
```

### Custom LLM Provider

```csharp
public class AzureOpenAIProvider : ILLMProvider
{
    // Implement ILLMProvider interface
}

builder.Services.AddMemNet(config)
    .WithLLMProvider<AzureOpenAIProvider>();
```

### Custom Knowledge Graph Store

```csharp
public class Neo4jGraphStore : IGraphStore
{
    // Implement IGraphStore interface
}

builder.Services.AddMemNet(config)
    .WithGraphStore<Neo4jGraphStore>();
```

## API Reference

### IMemoryService

- `AddAsync(AddMemoryRequest)` - Add memory (automatically extract entity relationships)
- `SearchAsync(SearchMemoryRequest)` - Search memories
- `GetAllAsync(userId, limit)` - Get all memories
- `GetAsync(memoryId)` - Get single memory
- `UpdateAsync(memoryId, content)` - Update memory
- `DeleteAsync(memoryId)` - Delete memory
- `DeleteAllAsync(userId)` - Delete all user memories

### IGraphStore (Knowledge Graph)

- `AddRelationsAsync(relations)` - Add entity relationships
- `SearchEntitiesAsync(entityName)` - Search entities
- `GetRelationsAsync(entityName)` - Get all relationships of an entity
- `DeleteEntityAsync(entityName)` - Delete entity and its relationships

## Configuration Options

| Option             | Type              | Default | Description                   |
|--------------------|-------------------|---------|-------------------------------|
| DuplicateThreshold | float             | 0.9     | Similarity threshold for deduplication |
| EnableReranking    | bool              | false   | Enable LLM reranking          |
| HistoryLimit       | int               | 10      | History message limit         |
| GraphStore         | GraphStoreConfig? | null    | Knowledge graph configuration (optional) |

## Knowledge Graph Features

When knowledge graph storage is enabled, MemNet will:

1. Automatically extract entities from conversations (people, organizations, places, concepts, etc.)
2. Identify relationships between entities
3. Build a knowledge graph to enhance memory associations
4. Support entity-based relationship queries

Example:

```csharp
// Add memory containing entities
await memoryService.AddAsync(new AddMemoryRequest
{
    Messages = new List<MessageContent>
    {
        new() { Role = "user", Content = "John works at Microsoft in Seattle" }
    },
    UserId = "user_123"
});

// System will automatically extract:
// Entities: John(Person), Seattle(Location), Microsoft(Organization)
// Relations: John -[works_at]-> Microsoft, John -[located_in]-> Seattle
```

## License

MIT License

## Reference

This project is a complete replication of [Mem0](https://github.com/mem0ai/mem0) core functionality, implemented in C#.
