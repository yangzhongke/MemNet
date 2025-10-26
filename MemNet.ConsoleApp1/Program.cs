using System;
using MemNet;
using MemNet.Abstractions;
using MemNet.Models;
using Microsoft.Extensions.DependencyInjection;

var services = new ServiceCollection();
services.AddMemNet(config =>
{
    config.Embedder.Endpoint = "https://personalopenai1.openai.azure.com/openai/v1/";
    config.Embedder.Model = "text-embedding-3-large";
    config.Embedder.ApiKey = Environment.GetEnvironmentVariable("OpenAIEmbeddingKey");

    config.LLM.Endpoint = "https://yangz-mf8s64eg-eastus2.cognitiveservices.azure.com/openai/v1/";
    config.LLM.Model = "gpt-5-nano";
    config.LLM.ApiKey = Environment.GetEnvironmentVariable("OpenAIChatKey");

    config.EnableReranking = true;
});

await using var sp = services.BuildServiceProvider();
var memoryService = sp.GetRequiredService<IMemoryService>();
await memoryService.AddAsync(new AddMemoryRequest
{
    Messages =
    [
        new MessageContent
        {
            Role = "User",
            Content = "My name is Zack. I love programming."
        },
        new MessageContent
        {
            Role = "User",
            Content = "As a 18-years-old boy, I'm into Chinese food."
        }
    ],
    UserId = "user001"
});

var resp = await memoryService.SearchAsync(new SearchMemoryRequest
{
    Query = "What do I like?", //"Am I old?",
    UserId = "user001"
});
Console.WriteLine("Search Results:");
foreach (var item in resp.ToArray())
{
    Console.WriteLine($"- {item.Memory.Data}");
}