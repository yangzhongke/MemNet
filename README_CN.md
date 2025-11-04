# MemNet
MemNet 是为 .NET 开发者设计的“自我完善”记忆层，旨在为基于大模型（LLM）的应用提供长期与短期记忆管理、相似度检索、记忆合并/精简和持久化等功能，方便在对话、推荐、个性化与场景感知应用中复用历史上下文。

说人话就是：LLM是没有状态的，而MemNet帮你记住用户之前的行为和对话内容，从而让应用更智能、更个性化。
## 为什么使用 MemNet
- 将零散对话/事件转换为可检索的记忆，提高上下文感知能力。
- 内置向量检索与记忆合并策略，降低重复记忆与噪声。
- 支持多种存储后端（内存、Qdrant、Redis、Chroma、Milvus等），便于扩展与持久化。
- 与任意 LLM/Embedding 提供方集成（可插拔 Embedding 层）。

## 安装
使用 dotnet CLI：
```
dotnet add package MemNet
```

使用 NuGet 控制台：
```
Install-Package MemNet
```

## 快速开始

### 1. 配置 appsettings.json

首先在项目的 `appsettings.json` 文件中配置 Embedder、LLM 和 VectorStore：

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

> **提示：** 为了安全起见，建议使用更安全的方式存储 API 密钥，而不是直接写在配置文件中。

### 2. 注册服务

在 `Program.cs` 中注册 MemNet 服务：

```csharp
using MemNet;
using MemNet.Abstractions;
using MemNet.Models;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;

var configuration = new ConfigurationBuilder()
    .AddJsonFile("appsettings.json")//需要 Nuget 包：Microsoft.Extensions.Configuration.Json
    .Build();

var services = new ServiceCollection();
services.AddMemNet(configuration);

await using var serviceProvider = services.BuildServiceProvider();
var memoryService = serviceProvider.GetRequiredService<IMemoryService>();
await memoryService.InitializeAsync();
```

### 3. 添加记忆

```csharp
await memoryService.AddAsync(new AddMemoryRequest
{
    Messages =
    [
        new MessageContent
        {
            Role = "User",
            Content = "我叫杨中科，我喜欢编程。"
        },
        new MessageContent
        {
            Role = "User",
            Content = "作为一名18岁的帅哥, 我喜欢中餐."
        },
        new MessageContent
        {
            Role = "User",
            Content = "我对坚果过敏。"
        }
    ],
    UserId = "user001"
});
```

UserId 用于区分不同用户的记忆数据。还支持AgentId、RunId等维度进行更细粒度的区分。

### 4. 搜索记忆

```csharp
var searchResults = await memoryService.SearchAsync(new SearchMemoryRequest
{
    Query = "给我推荐一些美食。",
    UserId = "user001"
});

Console.WriteLine("Search Results:");
foreach (var item in searchResults)
{
    Console.WriteLine($"- {item.Memory.Data}");
}
```
执行结果：
```
Search Results:
- 偏好：喜欢中餐
- 过敏信息：对坚果过敏
```

### 5. 使用不同的向量存储
MemNet 默认使用内存向量存储，适合开发和测试环境。在生产环境中，建议使用持久化的向量存储后端，以确保记忆数据的持久化和可扩展性。

MemNet 支持多种向量存储后端：

#### 使用 Qdrant

在appsettings.json中增加向量数据库配置（修改如下配置中的值为实际的值）:
```
"VectorStore": {
    "Endpoint": "your-Qdrant-endpoint，比如http://localhost:6333",
    "ApiKey": "your-Qdrant-apikey(可选的)",
    "CollectionName": "your-collection-name(可选的，默认是'memnet_collection')"
}
```
然后修改注册代码：
```csharp
services.AddMemNet(configuration).WithQdrant();
```

#### 使用 Chroma

在appsettings.json中增加向量数据库配置（修改如下配置中的值为实际的值）:
```
"VectorStore": {
    "Endpoint": "your-Qdrant-endpoint，比如http://localhost:8000",
    "ApiKey": "your-Chroma-apikey(可选的)",
    "CollectionName": "your-collection-name(可选的，默认是'memnet_collection')",
    "Database": "YourDatabaseName",
    "Tenant": "YourTenantName"
}
```
然后修改注册代码：
```csharp
services.AddMemNet(configuration).WithQdrant(configuration);
```

#### 使用 Milvus
在appsettings.json中增加向量数据库配置（修改如下配置中的值为实际的值）:
```
"VectorStore": {
    "Endpoint": "your-Milvus-endpoint，比如http://localhost:19530",
    "ApiKey": "your-Milvus-apikey(可选的)",
    "CollectionName": "your-collection-name(可选的，默认是'memnet_collection')"
}
```
然后修改注册代码：
```csharp
services.AddMemNet(configuration).WithMilvusV2();
```

#### 使用 Redis（需要安装 MemNet.Redis 包）

Add vector database configuration to appsettings.json (replace the values with your actual values):
```
"VectorStore": {
    "Endpoint": "your-Redis-address, e.g., localhost:6379",
    "ApiKey": "your-Redis-username-and-password (可选的), e.g. user:password",
    "CollectionName": "your-collection-name (可选的), default is 'memnet_collection')"
}
```

```csharp
services.AddMemNet(configuration).WithMemNetRedis(configuration);
```
#### 更多向量存储的支持

请参考MemNet.Redis项目来自定义其他向量存储的支持。欢迎提交PR增加新的支持。