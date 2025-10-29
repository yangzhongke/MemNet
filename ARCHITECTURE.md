# MemNet 集成测试架构图

## 整体架构概览

```
┌─────────────────────────────────────────────────────────────────────┐
│                         GitHub Repository                            │
│                                                                       │
│  ┌────────────────┐  ┌──────────────────┐  ┌───────────────────┐   │
│  │   Main Code    │  │  Integration     │  │  CI/CD Workflows  │   │
│  │   MemNet/      │  │  Tests           │  │  .github/         │   │
│  └────────────────┘  └──────────────────┘  └───────────────────┘   │
└─────────────────────────────────────────────────────────────────────┘
                              │
                              │ Push/PR
                              ▼
┌─────────────────────────────────────────────────────────────────────┐
│                      GitHub Actions Runner                           │
│                                                                       │
│  ┌──────────────────────────────────────────────────────────────┐   │
│  │  Integration Tests Workflow                                  │   │
│  │  ┌────────────┐  ┌─────────────┐  ┌──────────┐  ┌────────┐ │   │
│  │  │ Setup .NET │→ │ Start Docker │→ │ Run Tests│→ │ Report │ │   │
│  │  └────────────┘  └─────────────┘  └──────────┘  └────────┘ │   │
│  └──────────────────────────────────────────────────────────────┘   │
│                              │                                        │
│                              │ Tests Pass                             │
│                              ▼                                        │
│  ┌──────────────────────────────────────────────────────────────┐   │
│  │  Publish NuGet Workflow (on tag)                             │   │
│  │  ┌─────────┐  ┌────────┐  ┌──────┐  ┌────────────────────┐ │   │
│  │  │ Retest  │→ │ Build  │→ │ Pack │→ │ Push to NuGet.org  │ │   │
│  │  └─────────┘  └────────┘  └──────┘  └────────────────────┘ │   │
│  └──────────────────────────────────────────────────────────────┘   │
└─────────────────────────────────────────────────────────────────────┘
```

## 测试项目内部架构

```
MemNet.IntegrationTests/
│
├─ Base/                                    # 基础设施层
│  ├─ IntegrationTestBase                   # 所有测试的根基类
│  │  └─ 提供: Docker管理, OpenAI连接
│  │
│  ├─ VectorStoreTestBase<T>                # 泛型测试基类 ⭐核心
│  │  └─ 提供: 7个可复用测试场景
│  │
│  └─ TestConfiguration                     # 配置管理
│     └─ 读取: appsettings.test.json + 环境变量
│
├─ Fixtures/                                # 测试夹具
│  ├─ DockerComposeFixture                  # Docker容器生命周期
│  │  ├─ StartAsync() - 启动容器
│  │  ├─ WaitForHealthy() - 健康检查
│  │  └─ StopAsync() - 清理容器
│  │
│  └─ OpenAIFixture                         # OpenAI服务
│     ├─ Embedder - 文本向量化
│     └─ LLMProvider - 记忆提取/合并
│
├─ VectorStores/                            # 向量存储测试 (4个)
│  ├─ ChromaIntegrationTests ──┐
│  ├─ MilvusIntegrationTests ──┤
│  ├─ QdrantIntegrationTests ──┼─→ 继承 VectorStoreTestBase<T>
│  └─ InMemoryIntegrationTests ┘    └─→ 自动获得7个测试
│
└─ EndToEnd/                                # 端到端测试
   └─ MemoryServiceE2ETests                 # 完整业务流程测试
      ├─ AddMemory
      ├─ SearchMemory
      ├─ UpdateMemory
      ├─ DeleteMemory
      ├─ DuplicateMerge
      └─ MultiUserIsolation
```

## 测试继承层次结构

```
                    IAsyncLifetime (xUnit)
                           │
                           ▼
              ┌────────────────────────┐
              │ IntegrationTestBase    │  ◄─── 管理Docker & OpenAI
              │                        │
              │ - DockerFixture        │
              │ - OpenAIFixture        │
              │ - InitializeAsync()    │
              │ - DisposeAsync()       │
              └────────────────────────┘
                           │
                           ▼
              ┌────────────────────────────────┐
              │ VectorStoreTestBase<T>         │  ◄─── 可复用测试逻辑
              │                                │
              │ - CreateVectorStore() abstract │
              │                                │
              │ + TestEnsureCollectionExists() │
              │ + TestInsertAndRetrieve()      │
              │ + TestVectorSearch()           │
              │ + TestUpdateMemory()           │
              │ + TestDeleteMemory()           │
              │ + TestListMemories()           │
              │ + TestBatchOperations()        │
              └────────────────────────────────┘
                           │
         ┌─────────────────┼─────────────────┬─────────────┐
         ▼                 ▼                 ▼             ▼
    ┌─────────┐      ┌─────────┐      ┌─────────┐   ┌──────────┐
    │ Chroma  │      │ Milvus  │      │ Qdrant  │   │ InMemory │
    │ Tests   │      │ Tests   │      │ Tests   │   │ Tests    │
    └─────────┘      └─────────┘      └─────────┘   └──────────┘
```

## 测试执行流程

```
┌─────────────────────────────────────────────────────────────────┐
│  测试运行开始                                                    │
└─────────────────────────────────────────────────────────────────┘
                              │
                              ▼
         ┌────────────────────────────────────┐
         │ IntegrationTestBase.InitializeAsync│
         │  1. 启动Docker服务                 │
         │  2. 等待健康检查                   │
         │  3. 验证OpenAI连接                 │
         └────────────────────────────────────┘
                              │
                              ▼
         ┌────────────────────────────────────┐
         │ 并行执行多个测试类                 │
         │ (xUnit默认并行策略)                │
         └────────────────────────────────────┘
                              │
                ┌─────────────┼─────────────┐
                ▼             ▼             ▼
         ┌──────────┐  ┌──────────┐  ┌──────────┐
         │ Chroma   │  │ Milvus   │  │ Qdrant   │
         │ Tests    │  │ Tests    │  │ Tests    │
         │ (7个)    │  │ (7个)    │  │ (7个)    │
         └──────────┘  └──────────┘  └──────────┘
                │             │             │
                └─────────────┼─────────────┘
                              ▼
         ┌────────────────────────────────────┐
         │ 端到端测试 (6个场景)              │
         │  - 使用InMemory快速测试            │
         │  - 调用真实OpenAI                  │
         └────────────────────────────────────┘
                              │
                              ▼
         ┌────────────────────────────────────┐
         │ IntegrationTestBase.DisposeAsync   │
         │  - 清理测试数据                    │
         │  - Docker容器保持运行(可复用)      │
         └────────────────────────────────────┘
                              │
                              ▼
         ┌────────────────────────────────────┐
         │ 生成测试报告                       │
         │  - TRX格式                         │
         │  - 代码覆盖率                      │
         │  - 控制台输出                      │
         └────────────────────────────────────┘
```

## 单个测试的详细流程

以 `ChromaIntegrationTests.VectorSearch_ShouldReturnRelevantResults` 为例：

```
[Fact]
public async Task VectorSearch_ShouldReturnRelevantResults()
    => await TestVectorSearchAsync();
         │
         ▼
    VectorStoreTestBase<ChromaV2VectorStore>.TestVectorSearchAsync()
         │
         ├─→ CreateVectorStore()                    # 子类实现
         │   └─→ new ChromaV2VectorStore(...)
         │
         ├─→ EnsureCollectionExistsAsync()          # 创建集合
         │   └─→ Chroma API: POST /collections
         │
         ├─→ CreateMemoryItem() × 3                 # 准备测试数据
         │   ├─→ OpenAI Embedder.EmbedAsync()       # 调用真实OpenAI
         │   │   └─→ POST https://api.openai.com/v1/embeddings
         │   └─→ 返回: MemoryItem[]
         │
         ├─→ InsertAsync()                          # 插入向量
         │   └─→ Chroma API: POST /points/upsert
         │
         ├─→ OpenAI Embedder.EmbedAsync()           # 查询向量化
         │   └─→ "programming languages"
         │
         ├─→ SearchAsync()                          # 相似度搜索
         │   └─→ Chroma API: POST /points/query
         │
         └─→ FluentAssertions                       # 断言验证
             ├─ results.Should().NotBeEmpty()
             ├─ topResult.Score.Should().BeGreaterThan(0)
             └─ topResult.Memory.Data.Should().Contain("programming")
```

## Docker服务依赖图

```
┌─────────────────────────────────────────────────────────────┐
│  docker-compose.test.yml                                     │
└─────────────────────────────────────────────────────────────┘
                              │
         ┌────────────────────┼────────────────────┐
         ▼                    ▼                    ▼
┌──────────────┐    ┌──────────────┐    ┌──────────────┐
│   Chroma     │    │   Milvus     │    │   Qdrant     │
│              │    │              │    │              │
│ Port: 8000   │    │ Port: 19530  │    │ Port: 6333   │
│              │    │       9091   │    │      6334    │
│              │    │              │    │              │
│ Health: /api │    │ Health: /hea │    │ Health: /hea │
│   /v1/heartb │    │   lthz       │    │   lthz       │
│   eat        │    │              │    │              │
│              │    │              │    │              │
│ Volume:      │    │ Volume:      │    │ Volume:      │
│   chroma_dat │    │   milvus_dat │    │   qdrant_dat │
│   a          │    │   a          │    │   a          │
└──────────────┘    └──────────────┘    └──────────────┘
         │                  │                    │
         └──────────────────┼────────────────────┘
                            │
                    (localhost network)
                            │
                            ▼
                ┌───────────────────────┐
                │  测试运行器            │
                │  (dotnet test)        │
                └───────────────────────┘
```

## CI/CD数据流

```
┌──────────────┐
│ Developer    │
│ Push Code    │
└──────┬───────┘
       │
       ▼
┌────────────────────────────────────────────────────────┐
│ GitHub Actions - Integration Tests                     │
│                                                         │
│  Secrets:                                              │
│  ┌──────────────────┐                                 │
│  │ OPENAI_API_KEY   │────┐                            │
│  └──────────────────┘    │                            │
│                           ▼                            │
│  Environment Variables:                                │
│  - TEST_CHROMA_ENDPOINT=http://localhost:8000         │
│  - TEST_MILVUS_ENDPOINT=http://localhost:19530        │
│  - TEST_QDRANT_ENDPOINT=http://localhost:6333         │
│                                                         │
│  ┌──────────────────────────────────────────────────┐ │
│  │ 1. Docker Services (GitHub Services)            │ │
│  │    chroma, milvus, qdrant                        │ │
│  └──────────────────────────────────────────────────┘ │
│                           │                            │
│                           ▼                            │
│  ┌──────────────────────────────────────────────────┐ │
│  │ 2. Run Tests                                     │ │
│  │    dotnet test --logger trx                      │ │
│  └──────────────────────────────────────────────────┘ │
│                           │                            │
│                           ▼                            │
│  ┌──────────────────────────────────────────────────┐ │
│  │ 3. Upload Results                                │ │
│  │    - Test Results (TRX)                          │ │
│  │    - Code Coverage (Cobertura)                   │ │
│  └──────────────────────────────────────────────────┘ │
└─────────────────────┬──────────────────────────────────┘
                      │ Tests Pass
                      ▼
┌────────────────────────────────────────────────────────┐
│ GitHub Actions - Publish NuGet (on Tag)               │
│                                                         │
│  Trigger: git tag v1.0.0                               │
│                                                         │
│  ┌──────────────────────────────────────────────────┐ │
│  │ 1. Re-run Integration Tests                      │ │
│  └──────────────────────────────────────────────────┘ │
│                           │                            │
│                           ▼                            │
│  ┌──────────────────────────────────────────────────┐ │
│  │ 2. Build Release                                 │ │
│  │    dotnet build --configuration Release          │ │
│  └──────────────────────────────────────────────────┘ │
│                           │                            │
│                           ▼                            │
│  ┌──────────────────────────────────────────────────┐ │
│  │ 3. Pack NuGet                                    │ │
│  │    dotnet pack /p:PackageVersion=1.0.0           │ │
│  └──────────────────────────────────────────────────┘ │
│                           │                            │
│                           ▼                            │
│  ┌──────────────────────────────────────────────────┐ │
│  │ 4. Push to NuGet.org                             │ │
│  │    dotnet nuget push --api-key $SECRET           │ │
│  └──────────────────────────────────────────────────┘ │
│                           │                            │
│                           ▼                            │
│  ┌──────────────────────────────────────────────────┐ │
│  │ 5. Create GitHub Release                         │ │
│  │    - Release Notes                               │ │
│  │    - NuGet Package Attachment                    │ │
│  └──────────────────────────────────────────────────┘ │
└────────────────────┬───────────────────────────────────┘
                     │
                     ▼
          ┌──────────────────┐
          │  NuGet.org       │
          │  Package Live!   │
          └──────────────────┘
```

## 配置文件优先级

```
TestConfiguration.GetOpenAIApiKey()
         │
         ├─→ Environment.GetEnvironmentVariable("OPENAI_API_KEY")
         │   ├─ 优先级: 最高
         │   └─ 用途: CI/CD, 临时覆盖
         │
         └─→ Configuration["OpenAI:ApiKey"]
             ├─ 来源: appsettings.test.json
             ├─ 优先级: 次要
             └─ 用途: 本地开发
```

## 扩展点架构

添加新的向量存储只需3步：

```
1. 实现 IVectorStore 接口
   ┌────────────────────────┐
   │ MyNewVectorStore       │
   │ : IVectorStore         │
   └────────────────────────┘
            │
            ▼
2. 创建测试类 (5行代码)
   ┌──────────────────────────────────────────┐
   │ MyNewVectorStoreTests                    │
   │ : VectorStoreTestBase<MyNewVectorStore>  │
   │                                          │
   │ protected override MyNewVectorStore      │
   │     CreateVectorStore() { ... }          │
   └──────────────────────────────────────────┘
            │
            ▼
3. 自动获得7个测试 ✅
   - EnsureCollectionExists
   - InsertAndRetrieve
   - VectorSearch
   - UpdateMemory
   - DeleteMemory
   - ListMemories
   - BatchOperations
```

---

## 图例说明

```
┌─────┐
│ Box │  = 组件/模块/服务
└─────┘

   │
   ▼     = 数据/控制流

   ─→    = 调用/依赖关系

  ┌┴┐
  ├─┤   = 分支/多路径
  └┬┘
```

这个架构设计确保了：
✅ 高内聚、低耦合
✅ 易于扩展
✅ 测试隔离
✅ 生产级质量

