# Quick Start - Integration Tests

## Windows 快速开始

### 1. 一键运行脚本
```cmd
set OPENAI_API_KEY=sk-your-api-key-here
run-integration-tests.cmd
```

### 2. 手动步骤
```cmd
REM 设置API Key
set OPENAI_API_KEY=sk-your-api-key-here

REM 启动Docker服务
cd MemNet.IntegrationTests
docker-compose -f docker-compose.test.yml up -d

REM 等待服务就绪（约30-60秒）
timeout /t 30

REM 运行测试
cd ..
dotnet test
```

## Linux/Mac 快速开始

### 1. 一键运行脚本
```bash
export OPENAI_API_KEY=sk-your-api-key-here
chmod +x run-integration-tests.sh
./run-integration-tests.sh
```

### 2. 手动步骤
```bash
# 设置API Key
export OPENAI_API_KEY=sk-your-api-key-here

# 启动Docker服务
cd MemNet.IntegrationTests
docker-compose -f docker-compose.test.yml up -d

# 等待服务就绪（约30-60秒）
sleep 30

# 运行测试
cd ..
dotnet test
```

## 配置文件方式（推荐用于本地开发）

1. 复制示例配置：
```cmd
cd MemNet.IntegrationTests
copy appsettings.test.example.json appsettings.test.json
```

2. 编辑 `appsettings.test.json`：
```json
{
  "OpenAI": {
    "ApiKey": "sk-your-actual-api-key-here"
  }
}
```

3. 运行测试：
```cmd
cd ..
dotnet test
```

## 清理环境

```cmd
docker-compose -f MemNet.IntegrationTests\docker-compose.test.yml down -v
```

## 查看详细文档

完整文档请参阅：[INTEGRATION_TESTS_GUIDE.md](INTEGRATION_TESTS_GUIDE.md)
@echo off
REM MemNet Integration Tests Setup Script
REM This script helps you quickly set up and run integration tests

echo ========================================
echo MemNet Integration Tests Setup
echo ========================================
echo.

REM Check if Docker is running
docker info >nul 2>&1
if %errorlevel% neq 0 (
    echo [ERROR] Docker is not running!
    echo Please start Docker Desktop and try again.
    pause
    exit /b 1
)
echo [OK] Docker is running

REM Check if OpenAI API Key is set
if "%OPENAI_API_KEY%"=="" (
    echo.
    echo [WARNING] OPENAI_API_KEY environment variable is not set!
    echo.
    echo Please set your OpenAI API Key:
    echo   set OPENAI_API_KEY=sk-your-api-key-here
    echo.
    echo Or configure it in appsettings.test.json
    echo.
    set /p CONTINUE="Continue anyway? (y/n): "
    if /i not "%CONTINUE%"=="y" exit /b 1
)

echo.
echo ========================================
echo Starting Docker Services
echo ========================================
cd MemNet.IntegrationTests
docker-compose -f docker-compose.test.yml up -d

echo.
echo Waiting for services to be ready (this may take up to 60 seconds)...
timeout /t 10 /nobreak >nul

REM Wait for Chroma
:wait_chroma
echo Checking Chroma...
curl -s http://localhost:8000/api/v1/heartbeat >nul 2>&1
if %errorlevel% neq 0 (
    timeout /t 2 /nobreak >nul
    goto wait_chroma
)
echo [OK] Chroma is ready

REM Wait for Qdrant
:wait_qdrant
echo Checking Qdrant...
curl -s http://localhost:6333/healthz >nul 2>&1
if %errorlevel% neq 0 (
    timeout /t 2 /nobreak >nul
    goto wait_qdrant
)
echo [OK] Qdrant is ready

REM Wait for Milvus (takes longer)
:wait_milvus
echo Checking Milvus...
curl -s http://localhost:9091/healthz >nul 2>&1
if %errorlevel% neq 0 (
    timeout /t 5 /nobreak >nul
    goto wait_milvus
)
echo [OK] Milvus is ready

echo.
echo ========================================
echo All services are ready!
echo ========================================
echo.

echo Running integration tests...
echo.
cd ..
dotnet test MemNet.IntegrationTests\MemNet.IntegrationTests.csproj --logger "console;verbosity=normal"

echo.
echo ========================================
echo Tests completed!
echo ========================================
echo.
echo To stop Docker services, run:
echo   docker-compose -f MemNet.IntegrationTests\docker-compose.test.yml down -v
echo.
pause

