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
///     MemNet service registration extensions (replicating Mem0's configuration pattern)
/// </summary>
public static class ServiceCollectionExtensions
{
    /// <summary>
    ///     Add MemNet services
    /// </summary>
    public static IServiceCollection AddMemNet(
        this IServiceCollection services,
        IConfiguration configuration)
    {
        // Register configuration
        services.Configure<MemoryConfig>(configuration.GetSection("MemNet"));

        // Register core services
        services.AddScoped<IMemoryService, MemoryService>();

        // Register default implementations
        services.AddHttpClient<ILLMProvider, OpenAIProvider>();
        services.AddHttpClient<IEmbedder, OpenAIEmbedder>();
        services.AddSingleton<IVectorStore, InMemoryVectorStore>();

        return services;
    }

    /// <summary>
    ///     Add MemNet services (using configuration object)
    /// </summary>
    public static IServiceCollection AddMemNet(
        this IServiceCollection services,
        Action<MemoryConfig> configureOptions)
    {
        // Register configuration
        services.Configure(configureOptions);

        // Register core services
        services.AddScoped<IMemoryService, MemoryService>();

        // Register default implementations
        services.AddHttpClient<ILLMProvider, OpenAIProvider>();
        services.AddHttpClient<IEmbedder, OpenAIEmbedder>();
        services.AddSingleton<IVectorStore, InMemoryVectorStore>();

        return services;
    }

    /// <summary>
    ///     Use custom vector store
    /// </summary>
    public static IServiceCollection WithVectorStore<T>(
        this IServiceCollection services)
        where T : class, IVectorStore
    {
        services.AddSingleton<IVectorStore, T>();
        return services;
    }

    /// <summary>
    ///     Use custom LLM provider
    /// </summary>
    public static IServiceCollection WithLLMProvider<T>(
        this IServiceCollection services)
        where T : class, ILLMProvider
    {
        services.AddHttpClient<ILLMProvider, T>();
        return services;
    }

    /// <summary>
    ///     Use custom embedder
    /// </summary>
    public static IServiceCollection WithEmbedder<T>(
        this IServiceCollection services)
        where T : class, IEmbedder
    {
        services.AddHttpClient<IEmbedder, T>();
        return services;
    }
}