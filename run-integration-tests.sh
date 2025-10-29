#!/bin/bash
# MemNet Integration Tests Setup Script (Linux/Mac)
# This script helps you quickly set up and run integration tests

set -e

echo "========================================"
echo "MemNet Integration Tests Setup"
echo "========================================"
echo ""

# Check if Docker is running
if ! docker info >/dev/null 2>&1; then
    echo "[ERROR] Docker is not running!"
    echo "Please start Docker and try again."
    exit 1
fi
echo "[OK] Docker is running"

# Check if OpenAI API Key is set
if [ -z "$OPENAI_API_KEY" ]; then
    echo ""
    echo "[WARNING] OPENAI_API_KEY environment variable is not set!"
    echo ""
    echo "Please set your OpenAI API Key:"
    echo "  export OPENAI_API_KEY=sk-your-api-key-here"
    echo ""
    echo "Or configure it in appsettings.test.json"
    echo ""
    read -p "Continue anyway? (y/n): " CONTINUE
    if [ "$CONTINUE" != "y" ]; then
        exit 1
    fi
fi

echo ""
echo "========================================"
echo "Starting Docker Services"
echo "========================================"
cd MemNet.IntegrationTests
docker-compose -f docker-compose.test.yml up -d

echo ""
echo "Waiting for services to be ready (this may take up to 60 seconds)..."
sleep 10

# Wait for Chroma
echo "Checking Chroma..."
until curl -f http://localhost:8000/api/v1/heartbeat >/dev/null 2>&1; do
    sleep 2
done
echo "[OK] Chroma is ready"

# Wait for Qdrant
echo "Checking Qdrant..."
until curl -f http://localhost:6333/healthz >/dev/null 2>&1; do
    sleep 2
done
echo "[OK] Qdrant is ready"

# Wait for Milvus (takes longer)
echo "Checking Milvus..."
until curl -f http://localhost:9091/healthz >/dev/null 2>&1; do
    sleep 5
done
echo "[OK] Milvus is ready"

echo ""
echo "========================================"
echo "All services are ready!"
echo "========================================"
echo ""

echo "Running integration tests..."
echo ""
cd ..
dotnet test MemNet.IntegrationTests/MemNet.IntegrationTests.csproj --logger "console;verbosity=normal"

echo ""
echo "========================================"
echo "Tests completed!"
echo "========================================"
echo ""
echo "To stop Docker services, run:"
echo "  docker-compose -f MemNet.IntegrationTests/docker-compose.test.yml down -v"
echo ""

