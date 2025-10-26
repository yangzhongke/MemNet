using System;
using MemNet.Abstractions;
using MemNet.Config;
using MemNet.Core;
using MemNet.Embedders;
using MemNet.LLMs;
using MemNet.VectorStores;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;

namespace MemNet;

/// <summary>
///     MemNet 服务注册扩展（复刻 Mem0 的配置模式）
/// </summary>
public static class ServiceCollectionExtensions
{
    /// <summary>
    ///     添加 MemNet 服务
    /// </summary>
    public static IServiceCollection AddMemNet(
        this IServiceCollection services,
        IConfiguration configuration)
    {
        // 注册配置
        services.Configure<MemoryConfig>(configuration.GetSection("MemNet"));

        // 注册核心服务
        services.AddScoped<IMemoryService, MemoryService>();

        // 注册默认实现
        services.AddHttpClient<ILLMProvider, OpenAIProvider>();
        services.AddHttpClient<IEmbedder, OpenAIEmbedder>();
        services.AddSingleton<IVectorStore, InMemoryVectorStore>();

        return services;
    }

    /// <summary>
    ///     添加 MemNet 服务（使用配置对象）
    /// </summary>
    public static IServiceCollection AddMemNet(
        this IServiceCollection services,
        Action<MemoryConfig> configureOptions)
    {
        // 注册配置
        services.Configure(configureOptions);

        // 注册核心服务
        services.AddScoped<IMemoryService, MemoryService>();

        // 注册默认实现
        services.AddHttpClient<ILLMProvider, OpenAIProvider>();
        services.AddHttpClient<IEmbedder, OpenAIEmbedder>();
        services.AddSingleton<IVectorStore, InMemoryVectorStore>();

        return services;
    }

    /// <summary>
    ///     使用自定义向量存储
    /// </summary>
    public static IServiceCollection WithVectorStore<T>(
        this IServiceCollection services)
        where T : class, IVectorStore
    {
        services.AddSingleton<IVectorStore, T>();
        return services;
    }

    /// <summary>
    ///     使用自定义 LLM 提供者
    /// </summary>
    public static IServiceCollection WithLLMProvider<T>(
        this IServiceCollection services)
        where T : class, ILLMProvider
    {
        services.AddHttpClient<ILLMProvider, T>();
        return services;
    }

    /// <summary>
    ///     使用自定义嵌入器
    /// </summary>
    public static IServiceCollection WithEmbedder<T>(
        this IServiceCollection services)
        where T : class, IEmbedder
    {
        services.AddHttpClient<IEmbedder, T>();
        return services;
    }
}