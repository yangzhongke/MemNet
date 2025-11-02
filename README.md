# MemNet
MemNet 是为 .NET 开发者设计的“自我完善”记忆层，旨在为基于大模型（LLM）的应用提供长期与短期记忆管理、相似度检索、记忆合并/精简和持久化等功能，方便在对话、推荐、个性化与场景感知应用中复用历史上下文。
说人话就是：LLM是没有状态的，而MemNet帮你记住用户之前的行为和对话内容，从而让应用更智能、更个性化。
## 为什么使用 MemNet
- 将零散对话/事件转换为可检索的记忆，提高上下文感知能力。
- 内置向量检索与记忆合并策略，降低重复记忆与噪声。
- 支持多种存储后端（内存、Qdrant、Redis、Chroma、Milvus等），便于扩展与持久化。
- 与任意 LLM/Embedding 提供方集成（可插拔 Embedding 层）。

## 核心特性
- 记忆的插入、检索（基于相似度）与删除。
- 向量化与相似度搜索（可配置 Embedding 提供者）。
- 记忆合并/摘要（consolidation）机制以减少冗余。
- 本地或远程持久化（文件/SQLite/Redis/向量 DB）。
- 异步 API 支持高并发场景。
- 可配置的过期（TTL）、命名空间/会话支持。

## 安装（How to install）
使用 dotnet CLI：
```
dotnet add package MemNet
```

使用 NuGet 控制台：
```
Install-Package MemNet
```

或者在 csproj 中直接加入：
```xml
<PackageReference Include="MemNet" Version="*.*.*" />
```

（注意：将上面的包名/版本替换为你实际的 NuGet 包名和版本）

## 快速开始（Basic example）
下面示例为最小化伪代码/示例，展示插入和检索记忆的典型流程。API 名称以常见风格示例，实际使用请参照库的公开 API 文档。

```csharp
using MemNet;
using System;
using System.Threading.Tasks;

class Program
{
    static async Task Main()
    {
        // 初始化 MemNet 客户端（示例）
        var mem = new MemNetClient(new MemNetOptions
        {
            // 可选：指定向量化/embedding 提供者、存储后端等
            Storage = new InMemoryStore(),
            EmbeddingProvider = new OpenAIEmbeddingProvider(apiKey: "YOUR_KEY")
        });

        // 插入记忆
        await mem.UpsertMemoryAsync("user:alice", new MemoryRecord
        {
            Id = Guid.NewGuid().ToString(),
            Content = "Alice viewed the pricing page and liked the Pro plan.",
            Timestamp = DateTime.UtcNow
        });

        // 基于查询检索相关记忆
        var related = await mem.QueryMemoriesAsync("user:alice", "what did Alice do earlier?", topK: 5);
        foreach (var r in related)
        {
            Console.WriteLine($"{r.Score:F3} - {r.Content}");
        }
    }
}
```

## 进阶示例（Advanced）

1) 使用持久化（SQLite）+ 本地向量存储
```csharp
var mem = new MemNetClient(new MemNetOptions
{
    Storage = new SqliteStore("memnet.db"),
    VectorIndex = new FaissLikeLocalIndex(path: "vectors.index"),
    EmbeddingProvider = new CustomEmbeddingProvider(/*...*/)
});
```
用途：在重启后保留记忆并支持快速相似度检索。

2) 自定义 Embedding 提供者
- 实现 IEmbeddingProvider 接口以使用内部或第三方 embedding 服务（OpenAI, HuggingFace, 本地模型等）。
- 将实现注入到 MemNetOptions.EmbeddingProvider。

3) 记忆合并与清理策略
- 配置合并规则（例如按时间窗口合并相似短事件为摘要）。
- 配置 TTL（自动过期）以控制长期记忆池大小：
```csharp
mem.Config.MemoryTtl = TimeSpan.FromDays(30);
mem.Config.ConsolidationThreshold = 0.85; // 相似度阈值
```

4) 并发与批处理
- 使用异步批处理接口批量插入/向量化以提高吞吐：
```csharp
await mem.UpsertMemoriesAsync(userId, batchOfRecords);
```

5) 多租户／命名空间
- 每个用户/会话使用独立命名空间或前缀来隔离记忆：
```csharp
await mem.UpsertMemoryAsync(namespace: "tenantA:user:alice", record);
```

## 调优建议
- 为频繁查询的场景减少检索 topK 并增加融合摘要以降低上下文长度。
- 对高吞吐写入使用批量向量化与后台合并作业。
- 根据数据量选择合适的向量索引（内存索引 vs. disk-backed vs. 专用向量 DB）。

## API 与文档
请参阅项目的 API 文档以获取详细类/方法说明、配置项与示例（链接或本地 docs 位置）。

## 贡献与许可
- 欢迎通过 PR/Issue 贡献功能或修复。
- 请在提交前查看贡献指南与代码风格（若存在）。
- 许可证信息请参阅仓库根目录 LICENSE 文件。
