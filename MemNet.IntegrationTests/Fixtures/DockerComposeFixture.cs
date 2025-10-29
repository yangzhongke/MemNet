using System;
using System.Diagnostics;
using System.IO;
using System.Net.Http;
using System.Threading.Tasks;

namespace MemNet.IntegrationTests.Fixtures;

/// <summary>
/// Docker Compose fixture for managing test containers
/// </summary>
public class DockerComposeFixture : IDisposable
{
    private readonly string _dockerComposeFile;
    private bool _started;

    public DockerComposeFixture()
    {
        _dockerComposeFile = Path.Combine(
            Directory.GetCurrentDirectory(),
            "docker-compose.test.yml");
    }

    public async Task StartAsync()
    {
        if (_started) return;

        Console.WriteLine("Starting Docker containers for integration tests...");
        
        // Stop any existing containers
        await RunDockerComposeCommand("down -v");
        
        // Start containers
        await RunDockerComposeCommand("up -d");
        
        // Wait for services to be healthy
        Console.WriteLine("Waiting for services to be healthy...");
        await WaitForHealthyServices();
        
        _started = true;
        Console.WriteLine("All services are ready!");
    }

    public async Task StopAsync()
    {
        if (!_started) return;

        Console.WriteLine("Stopping Docker containers...");
        await RunDockerComposeCommand("down -v");
        _started = false;
    }

    private async Task RunDockerComposeCommand(string arguments)
    {
        var startInfo = new ProcessStartInfo
        {
            FileName = "docker-compose",
            Arguments = $"-f \"{_dockerComposeFile}\" {arguments}",
            RedirectStandardOutput = true,
            RedirectStandardError = true,
            UseShellExecute = false,
            CreateNoWindow = true
        };

        using var process = Process.Start(startInfo);
        if (process == null)
        {
            throw new InvalidOperationException("Failed to start docker-compose process");
        }

        await process.WaitForExitAsync();

        if (process.ExitCode != 0)
        {
            var error = await process.StandardError.ReadToEndAsync();
            Console.WriteLine($"Docker-compose error: {error}");
        }
    }

    private async Task WaitForHealthyServices()
    {
        var maxAttempts = 30;
        var delayBetweenAttempts = TimeSpan.FromSeconds(2);

        // Wait for Chroma
        await WaitForService("http://localhost:8000/api/v2/heartbeat", "Chroma", maxAttempts, delayBetweenAttempts);
        
        // Wait for Milvus
        await WaitForService("http://localhost:9091/healthz", "Milvus", maxAttempts, delayBetweenAttempts);
        
        // Wait for Qdrant
        await WaitForService("http://localhost:6333/healthz", "Qdrant", maxAttempts, delayBetweenAttempts);
    }

    private async Task WaitForService(string healthUrl, string serviceName, int maxAttempts, TimeSpan delay)
    {
        using var httpClient = new HttpClient { Timeout = TimeSpan.FromSeconds(5) };
        
        for (var i = 0; i < maxAttempts; i++)
        {
            try
            {
                var response = await httpClient.GetAsync(healthUrl);
                if (response.IsSuccessStatusCode)
                {
                    Console.WriteLine($"{serviceName} is healthy!");
                    return;
                }
            }
            catch
            {
                // Service not ready yet
            }

            if (i < maxAttempts - 1)
            {
                Console.WriteLine($"Waiting for {serviceName} to be ready... (attempt {i + 1}/{maxAttempts})");
                await Task.Delay(delay);
            }
        }

        throw new TimeoutException($"{serviceName} did not become healthy within the expected time");
    }

    public void Dispose()
    {
        StopAsync().GetAwaiter().GetResult();
    }
}

