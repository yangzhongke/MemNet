# MemNet
# MemNet

[![Build Status](https://github.com/yourusername/MemNet/workflows/Integration%20Tests/badge.svg)](https://github.com/yourusername/MemNet/actions)
[![NuGet](https://img.shields.io/nuget/v/MemNet.svg)](https://www.nuget.org/packages/MemNet/)
[![License: MIT](https://img.shields.io/badge/License-MIT-yellow.svg)](https://opensource.org/licenses/MIT)

MemNet is a self‑improving memory layer for LLM applications for .NET developer.

## ✨ Features

- 🧠 **Intelligent Memory Management** - Automatic extraction, storage, and retrieval of conversational memories
- 🔍 **Semantic Search** - Vector-based similarity search using embeddings
- 🔄 **Auto-Deduplication** - Smart merging of similar memories
- 🎯 **Multi-Vector Store Support** - Chroma, Milvus, Qdrant, and In-Memory
- 🚀 **Production Ready** - Comprehensive integration tests with real services
- 🌐 **OpenAI Integration** - Built-in support for OpenAI embeddings and LLM

## 📦 Installation

```bash
dotnet add package MemNet
```

## 🧪 Testing & CI/CD

This project includes comprehensive integration tests that connect to real OpenAI services and Docker-based vector stores.

- **📚 [Quick Start Guide](QUICKSTART_TESTS.md)** - Get started with testing in 5 minutes
- **📖 [Complete Testing Guide](INTEGRATION_TESTS_GUIDE.md)** - In-depth documentation
- **🤖 Automated CI/CD** - GitHub Actions for testing and NuGet publishing

### Run Tests Locally

```bash
# Set your OpenAI API Key
set OPENAI_API_KEY=sk-your-api-key-here

# Run the automated script
run-integration-tests.cmd
```

Or see [QUICKSTART_TESTS.md](QUICKSTART_TESTS.md) for detailed instructions.



