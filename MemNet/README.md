# MemNet - C# Memory Library for LLMs

MemNet 是一个完全复刻 Mem0 的 C# 记忆库，为大语言模型（LLM）提供智能记忆管理功能。

## 核心功能

- **智能记忆提取**：使用 LLM 从对话中自动提取结构化记忆
- **去重与合并**：自动检测相似记忆并智能合并
- **向量语义搜索**：基于向量相似度的语义检索
- **知识图谱存储**：提取实体和关系，构建知识图谱（可选）
- **多种存储后端**：支持内存存储、Qdrant、Milvus 等
- **灵活配置**：支持多种 LLM 和 Embedding 提供者

## 快速开始

### 1. 安装

```bash
dotnet add reference MemNet
```

### 2. 配置（appsettings.json）

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

### 3. 注册服务

```csharp
using MemNet;

var builder = WebApplication.CreateBuilder(args);

// 从配置文件注册 MemNet
builder.Services.AddMemNet(builder.Configuration);

// 可选：启用知识图谱存储
builder.Services.WithGraphStore();

var app = builder.Build();
```

或者使用代码配置：

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
.WithGraphStore(); // 启用知识图谱存储
```

### 4. 使用示例

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
        // 1. 添加记忆
        var addRequest = new AddMemoryRequest
        {
            Messages = new List<MessageContent>
            {
                new() { Role = "user", Content = userMessage }
            },
            UserId = userId
        };

        var result = await _memoryService.AddAsync(addRequest);
        Console.WriteLine($"添加了 {result.Results.Count} 条记忆");

        // 2. 搜索相关记忆
        var searchRequest = new SearchMemoryRequest
        {
            Query = userMessage,
            UserId = userId,
            Limit = 5
        };

        var memories = await _memoryService.SearchAsync(searchRequest);
        foreach (var memory in memories)
        {
            Console.WriteLine($"记忆: {memory.Memory.Data} (相似度: {memory.Score})");
        }

        // 3. 获取所有记忆
        var allMemories = await _memoryService.GetAllAsync(userId);
        Console.WriteLine($"用户共有 {allMemories.Count} 条记忆");
    }
}
```

## 架构设计

### 核心组件

```
MemNet
├── Abstractions/          # 核心接口
│   ├── IMemoryService     # 记忆服务接口
│   ├── ILLMProvider       # LLM 提供者接口
│   ├── IEmbedder          # 嵌入生成器接口
│   └── IVectorStore       # 向量存储接口
├── Core/                  # 核心实现
│   └── MemoryService      # 记忆服务实现
├── LLMs/                  # LLM 提供者
│   └── OpenAIProvider     # OpenAI 实现
├── Embedders/             # 嵌入生成器
│   └── OpenAIEmbedder     # OpenAI 嵌入实现
├── VectorStores/          # 向量存储
│   └── InMemoryVectorStore # 内存存储
├── Models/                # 数据模型
└── Config/                # 配置类
```

### 工作流程

1. **添加记忆流程**：
    - 接收用户消息
    - 使用 LLM 提取结构化记忆
    - 生成向量嵌入
    - 检查相似记忆
    - 合并或插入新记忆

2. **搜索记忆流程**：
    - 生成查询向量
    - 向量相似度搜索
    - 可选的 LLM 重排序
    - 返回相关记忆

## 扩展性

### 自定义向量存储

```csharp
public class QdrantVectorStore : IVectorStore
{
    // 实现 IVectorStore 接口
}

// 注册自定义存储
builder.Services.AddMemNet(config)
    .WithVectorStore<QdrantVectorStore>();
```

### 自定义 LLM 提供者

```csharp
public class AzureOpenAIProvider : ILLMProvider
{
    // 实现 ILLMProvider 接口
}

builder.Services.AddMemNet(config)
    .WithLLMProvider<AzureOpenAIProvider>();
```

### 自定义知识图谱存储

```csharp
public class Neo4jGraphStore : IGraphStore
{
    // 实现 IGraphStore 接口
}

builder.Services.AddMemNet(config)
    .WithGraphStore<Neo4jGraphStore>();
```

## API 参考

### IMemoryService

- `AddAsync(AddMemoryRequest)` - 添加记忆（自动提取实体关系）
- `SearchAsync(SearchMemoryRequest)` - 搜索记忆
- `GetAllAsync(userId, limit)` - 获取所有记忆
- `GetAsync(memoryId)` - 获取单条记忆
- `UpdateAsync(memoryId, content)` - 更新记忆
- `DeleteAsync(memoryId)` - 删除记忆
- `DeleteAllAsync(userId)` - 删除用户所有记忆

### IGraphStore（知识图谱）

- `AddRelationsAsync(relations)` - 添加实体关系
- `SearchEntitiesAsync(entityName)` - 搜索实体
- `GetRelationsAsync(entityName)` - 获取实体的所有关系
- `DeleteEntityAsync(entityName)` - 删除实体及其关系

## 配置选项

| 选项                 | 类型                | 默认值   | 说明           |
|--------------------|-------------------|-------|--------------|
| DuplicateThreshold | float             | 0.9   | 去重相似度阈值      |
| EnableReranking    | bool              | false | 是否启用 LLM 重排序 |
| HistoryLimit       | int               | 10    | 历史消息限制       |
| GraphStore         | GraphStoreConfig? | null  | 知识图谱配置（可选）   |

## 知识图谱功能说明

当启用知识图谱存储时，MemNet 会：

1. 自动从对话中提取实体（人物、组织、地点、概念等）
2. 识别实体之间的关系
3. 构建知识图谱，增强记忆的关联性
4. 支持基于实体的关系查询

示例：

```csharp
// 添加包含实体的记忆
await memoryService.AddAsync(new AddMemoryRequest
{
    Messages = new List<MessageContent>
    {
        new() { Role = "user", Content = "张三在北京的微软公司工作" }
    },
    UserId = "user_123"
});

// 系统会自动提取：
// 实体: 张三(Person), 北京(Location), 微软(Organization)
// 关系: 张三 -[works_at]-> 微软, 张三 -[lives_in]-> 北京
```

## 许可证

MIT License

## 参考

本项目完全复刻了 [Mem0](https://github.com/mem0ai/mem0) 的核心功能，使用 C# 实现。
