# MemNet 集成测试与CI/CD指南

## 📋 概述

本项目实现了完整的集成测试系统，连接真实的OpenAI服务和本地Docker中的向量存储（Chroma、Milvus、Qdrant）。测试通过后，自动发布NuGet包。

## 🏗️ 架构设计

### 核心特性

1. **可复用的测试基类** - 所有向量存储共享相同的测试逻辑
2. **真实环境测试** - 使用真实的OpenAI API和Docker容器
3. **自动化CI/CD** - GitHub Actions自动运行测试和发布
4. **多向量存储支持** - Chroma、Milvus、Qdrant、InMemory

### 项目结构

```
MemNet.IntegrationTests/
├── Base/
│   ├── IntegrationTestBase.cs       # 所有集成测试的基类
│   ├── VectorStoreTestBase.cs       # 向量存储测试基类（可复用）
│   └── TestConfiguration.cs         # 测试配置管理
├── Fixtures/
│   ├── DockerComposeFixture.cs      # Docker容器生命周期管理
│   └── OpenAIFixture.cs             # OpenAI服务初始化
├── VectorStores/
│   ├── ChromaIntegrationTests.cs    # Chroma测试
│   ├── MilvusIntegrationTests.cs    # Milvus测试
│   ├── QdrantIntegrationTests.cs    # Qdrant测试
│   └── InMemoryIntegrationTests.cs  # 内存测试
├── EndToEnd/
│   └── MemoryServiceE2ETests.cs     # 端到端测试
└── docker-compose.test.yml          # 测试环境配置
```

## 🚀 快速开始

### 前置条件

1. **.NET 8.0 SDK**
2. **Docker Desktop** (Windows/Mac) 或 **Docker Engine** (Linux)
3. **OpenAI API Key**

### 本地运行步骤

#### 1. 配置OpenAI API Key

**方式A：环境变量（推荐用于CI/CD）**
```cmd
set OPENAI_API_KEY=sk-your-api-key-here
```

**方式B：配置文件（推荐用于本地开发）**
```cmd
cd MemNet.IntegrationTests
copy appsettings.test.example.json appsettings.test.json
```

编辑 `appsettings.test.json`，填入您的API Key：
```json
{
  "OpenAI": {
    "ApiKey": "sk-your-actual-api-key-here"
  }
}
```

#### 2. 启动Docker服务

```cmd
cd MemNet.IntegrationTests
docker-compose -f docker-compose.test.yml up -d
```

等待所有服务启动（约30-60秒）：
```cmd
docker-compose -f docker-compose.test.yml ps
```

#### 3. 运行测试

**运行所有测试：**
```cmd
dotnet test
```

**运行特定测试套件：**
```cmd
# 只测试Chroma
dotnet test --filter "FullyQualifiedName~ChromaIntegrationTests"

# 只测试端到端场景
dotnet test --filter "FullyQualifiedName~MemoryServiceE2ETests"

# 测试所有向量存储
dotnet test --filter "FullyQualifiedName~VectorStores"
```

**详细输出：**
```cmd
dotnet test --logger "console;verbosity=detailed"
```

#### 4. 清理Docker环境

```cmd
docker-compose -f docker-compose.test.yml down -v
```

## 🧪 测试覆盖范围

### 向量存储测试（所有存储共享）

每个向量存储都会执行以下测试：

| 测试场景 | 描述 |
|---------|------|
| `EnsureCollectionExists` | 集合创建和维度验证 |
| `InsertAndRetrieve` | 插入和检索内存 |
| `VectorSearch` | 语义相似度搜索 |
| `UpdateMemory` | 更新现有内存 |
| `DeleteMemory` | 删除内存 |
| `ListMemories` | 按用户过滤列表 |
| `BatchOperations` | 批量操作性能 |

### 端到端测试

| 测试场景 | 描述 |
|---------|------|
| `AddMemory_ShouldExtractAndStoreMemories` | 从对话中提取并存储记忆 |
| `SearchMemory_ShouldReturnRelevantMemories` | 语义搜索相关记忆 |
| `AddMemory_WithDuplicates_ShouldMergeMemories` | 去重和合并相似记忆 |
| `UpdateMemory_ShouldModifyExistingMemory` | 更新记忆内容 |
| `DeleteMemory_ShouldRemoveMemory` | 删除特定记忆 |
| `MultiUser_ShouldIsolateMemories` | 多用户数据隔离 |

## 🔧 添加新的向量存储测试

要为新的向量存储添加测试，只需：

```csharp
public class MyNewVectorStoreTests : VectorStoreTestBase<MyNewVectorStore>
{
    protected override MyNewVectorStore CreateVectorStore()
    {
        // 配置并返回您的向量存储实例
        var config = Options.Create(new MemoryConfig
        {
            VectorStore = new VectorStoreConfig
            {
                Endpoint = "http://localhost:9999",
                CollectionName = GenerateUniqueCollectionName()
            }
        });
        return new MyNewVectorStore(new HttpClient(), config);
    }

    // 自动继承所有7个标准测试
    [Fact]
    public async Task EnsureCollectionExists_ShouldCreateCollection()
        => await TestEnsureCollectionExistsAsync();

    [Fact]
    public async Task InsertAndRetrieve_ShouldWorkCorrectly()
        => await TestInsertAndRetrieveAsync();
    
    // ... 更多测试
}
```

## 🤖 GitHub Actions CI/CD

### 集成测试工作流

**触发条件：**
- Push 到 `main` 或 `develop` 分支
- Pull Request 到 `main` 或 `develop`
- 手动触发

**流程：**
1. ✅ Checkout 代码
2. ✅ 设置 .NET 8.0
3. ✅ 启动 Docker 服务（Chroma、Milvus、Qdrant）
4. ✅ 等待服务健康检查
5. ✅ 运行所有集成测试
6. ✅ 上传测试报告和覆盖率

**配置文件：** `.github/workflows/integration-tests.yml`

### NuGet发布工作流

**触发条件：**
- 推送版本标签（如 `v1.0.0`）
- 手动触发（指定版本号）

**流程：**
1. ✅ 运行完整集成测试（必须通过）
2. ✅ 构建 Release 版本
3. ✅ 打包 NuGet 包（包含符号包）
4. ✅ 发布到 NuGet.org
5. ✅ 创建 GitHub Release

**配置文件：** `.github/workflows/publish-nuget.yml`

### 配置GitHub Secrets

在GitHub仓库设置中添加以下Secrets：

| Secret | 描述 | 获取方式 |
|--------|------|---------|
| `OPENAI_API_KEY` | OpenAI API密钥 | https://platform.openai.com/api-keys |
| `NUGET_API_KEY` | NuGet.org API密钥 | https://www.nuget.org/account/apikeys |

**设置步骤：**
1. 进入仓库 Settings → Secrets and variables → Actions
2. 点击 "New repository secret"
3. 添加上述两个secrets

## 📦 发布新版本

### 方式1：使用Git标签（推荐）

```cmd
git tag v1.0.0
git push origin v1.0.0
```

GitHub Actions会自动：
- 运行集成测试
- 构建并发布NuGet包
- 创建GitHub Release

### 方式2：手动触发

1. 进入 GitHub Actions
2. 选择 "Publish NuGet Package" 工作流
3. 点击 "Run workflow"
4. 输入版本号（如 `1.0.0`）

## 🐛 故障排除

### Docker服务无法启动

**问题：** 端口被占用
```
Error: Bind for 0.0.0.0:8000 failed: port is already allocated
```

**解决：**
```cmd
# 查看占用端口的进程
netstat -ano | findstr :8000

# 停止其他Docker容器
docker ps
docker stop <container_id>
```

### OpenAI API错误

**问题：** 401 Unauthorized

**解决：**
1. 检查API Key是否正确
2. 确认API Key有足够的额度
3. 验证环境变量/配置文件设置

**问题：** 429 Rate Limit

**解决：**
- 降低测试并发度
- 等待一段时间后重试
- 升级OpenAI账户等级

### 测试超时

**问题：** xUnit测试超时

**解决：**
```csharp
[Fact(Timeout = 60000)] // 增加到60秒
public async Task YourTest() { ... }
```

### Milvus连接问题

Milvus启动较慢，需要更长的健康检查时间（约30秒）。

**检查Milvus状态：**
```cmd
docker logs memnet-test-milvus
curl http://localhost:9091/healthz
```

## 📊 查看测试报告

### 本地查看

测试结果保存在 `TestResults` 目录：
```cmd
dir TestResults /s
```

### GitHub Actions查看

1. 进入 GitHub Actions
2. 选择最近的测试运行
3. 查看 "Test Results" 和 "Coverage Reports"

## 🔐 安全最佳实践

1. ✅ **永远不要**提交 `appsettings.test.json`（已在 .gitignore）
2. ✅ 使用环境变量存储敏感信息
3. ✅ 定期轮换 API Keys
4. ✅ 为测试使用限制权限的API Keys
5. ✅ 监控API使用情况和成本

## 📈 性能优化

### 并行测试

xUnit默认并行运行测试类，但同一类内的测试串行执行。

**禁用特定测试的并行：**
```csharp
[Collection("Sequential")]
public class MyTests { ... }
```

### 减少OpenAI调用

- 使用 `InMemoryVectorStore` 测试不依赖向量存储的功能
- Mock LLM和Embedder用于单元测试
- 缓存embedding结果

## 🤝 贡献指南

添加新测试时：

1. 继承 `VectorStoreTestBase<T>` 实现向量存储测试
2. 在 `EndToEnd` 添加新的业务场景测试
3. 更新 `docker-compose.test.yml` 添加新服务
4. 更新 README 文档

## 📝 许可证

MIT License - 详见 LICENSE 文件

## 🙋 获取帮助

- 查看 [Issues](https://github.com/yourusername/MemNet/issues)
- 阅读项目 [Wiki](https://github.com/yourusername/MemNet/wiki)
- 联系维护者

---

**祝测试愉快！** 🎉

