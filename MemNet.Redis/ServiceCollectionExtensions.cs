using System;
using MemNet.Abstractions;
using MemNet.Config;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using StackExchange.Redis;

namespace MemNet.Redis;

/// <summary>
/// MemNet.Redis service registration extensions
/// </summary>
public static class RedisServiceCollectionExtensions
{
    /// <summary>
    /// Add MemNet with Redis vector store support using VectorStoreConfig
    /// </summary>
    /// <param name="services">Service collection</param>
    /// <param name="configuration">configuration</param>
    /// <param name="configureOptions">Optional Redis configuration options</param>
    public static IServiceCollection WithMemNetRedis(
        this IServiceCollection services,
        IConfiguration configuration,
        Action<ConfigurationOptions>? configureOptions = null)
    {
        services.AddSingleton<IConnectionMultiplexer>(sp =>
        {
            var config = sp.GetRequiredService<VectorStoreConfig>();

            if (string.IsNullOrEmpty(config.Endpoint))
            {
                throw new InvalidOperationException("VectorStoreConfig.Endpoint is required for Redis connection");
            }

            var options = ConfigurationOptions.Parse(config.Endpoint);

            if (!string.IsNullOrEmpty(config.ApiKey))
            {
                // Parse ApiKey in "UserName:Password" format
                var parts = config.ApiKey!.Split([':'], 2);
                if (parts.Length == 2)
                {
                    options.User = parts[0];
                    options.Password = parts[1];
                }
                else
                {
                    // Fallback: treat as password only
                    options.Password = config.ApiKey;
                }
            }

            configureOptions?.Invoke(options);

            return ConnectionMultiplexer.Connect(options);
        });

        services.AddSingleton<IVectorStore, RedisVectorStore>();
        services.Configure<VectorStoreConfig>(configuration.GetSection("MemNet:VectorStore"));
        return services;
    }

    /// <summary>
    /// Add MemNet with Redis vector store support
    /// </summary>
    /// <param name="services">Service collection</param>
    /// <param name="configuration">configuration</param>
    /// <param name="connectionString">Redis connection string</param>
    /// <param name="configureOptions">Optional Redis configuration options</param>
    public static IServiceCollection WithMemNetRedis(
        this IServiceCollection services,
        IConfiguration configuration,
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
        // Register RedisVectorStore
        services.AddSingleton<IVectorStore, RedisVectorStore>();
        services.Configure<VectorStoreConfig>(configuration.GetSection("MemNet:VectorStore"));
        return services;
    }
}
