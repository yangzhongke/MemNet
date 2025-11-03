using System;
using MemNet.Abstractions;
using Microsoft.Extensions.DependencyInjection;
using StackExchange.Redis;

namespace MemNet.Redis;

/// <summary>
/// MemNet.Redis service registration extensions
/// </summary>
public static class RedisServiceCollectionExtensions
{
    /// <summary>
    /// Use Redis Stack as vector store
    /// </summary>
    /// <param name="services">Service collection</param>
    /// <param name="connectionString">Redis connection string (e.g., "localhost:6379")</param>
    /// <param name="configureOptions">Optional Redis configuration options</param>
    public static IServiceCollection WithRedis(
        this IServiceCollection services,
        string connectionString,
        Action<ConfigurationOptions>? configureOptions = null)
    {
        if (string.IsNullOrEmpty(connectionString))
        {
            throw new ArgumentNullException(nameof(connectionString));
        }

        // Configure Redis connection
        var options = ConfigurationOptions.Parse(connectionString);
        configureOptions?.Invoke(options);

        // Register IConnectionMultiplexer as singleton
        services.AddSingleton<IConnectionMultiplexer>(_ => ConnectionMultiplexer.Connect(options));

        // Register RedisVectorStore
        services.AddSingleton<IVectorStore, RedisVectorStore>();

        return services;
    }

    /// <summary>
    /// Use Redis Stack as vector store with existing IConnectionMultiplexer
    /// </summary>
    /// <param name="services">Service collection</param>
    public static IServiceCollection WithRedis(this IServiceCollection services)
    {
        services.AddSingleton<IVectorStore, RedisVectorStore>();
        return services;
    }

    /// <summary>
    /// Add MemNet with Redis vector store support
    /// </summary>
    /// <param name="services">Service collection</param>
    /// <param name="connectionString">Redis connection string</param>
    /// <param name="configureOptions">Optional Redis configuration options</param>
    public static IServiceCollection AddMemNetRedis(
        this IServiceCollection services,
        string connectionString,
        Action<ConfigurationOptions>? configureOptions = null)
    {
        if (string.IsNullOrEmpty(connectionString))
        {
            throw new ArgumentNullException(nameof(connectionString));
        }

        // Configure Redis connection
        var options = ConfigurationOptions.Parse(connectionString);
        configureOptions?.Invoke(options);

        // Register IConnectionMultiplexer
        services.AddSingleton<IConnectionMultiplexer>(_ => ConnectionMultiplexer.Connect(options));
        services.AddSingleton<IConnectionMultiplexer>(_ => ConnectionMultiplexer.Connect(options));
        // Register RedisVectorStore
        services.AddSingleton<IVectorStore, RedisVectorStore>();

        return services;
    }
}

