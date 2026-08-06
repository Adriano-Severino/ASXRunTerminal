using ASXRunTerminal.Core;
using ASXRunTerminal.Subagents;
using Microsoft.Extensions.AI;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.DependencyInjection.Extensions;
using System;
namespace ASXRunTerminal.Infra
{
    public static class RagService
    {
        public static IHost Initialize()
        {
            return Host.CreateDefaultBuilder()
                .ConfigureServices((context, services) =>
                {
                    // Vector store configuration
                    var configPath = Path.Combine(
                        Environment.GetFolderPath(Environment.SpecialFolder.UserProfile),
                        ".asxrun",
                        "vector-store.db");
                    var vectorConfig = VectorStoreConfiguration.CreatePersistent(configPath);

                    services.AddSingleton(vectorConfig);

                    // Ollama HTTP client setup
                    services.AddHttpClient<OllamaHttpClient>(client =>
                    {
                        client.BaseAddress = OllamaModelDefaults.DefaultEndpoint;
                    });

                    // Register chat client using our custom adapter
                    services.AddSingleton<IChatClient>(sp =>
                    {
                        var ollamaHttpClient = sp.GetRequiredService<OllamaHttpClient>();
                        return ollamaHttpClient.ChatClient;
                    });

                    // Register embedding generator
                    services.AddSingleton<OllamaEmbeddingGenerator>(sp =>
                    {
                        var httpClient = sp.GetRequiredService<HttpClient>();
                        return new OllamaEmbeddingGenerator(httpClient, vectorConfig.EmbeddingModel);
                    });

                    // Register vector store
                    services.AddSingleton<SqliteVectorStore>(sp =>
                    {
                        return new SqliteVectorStore(vectorConfig);
                    });

                    // Register code reviewer subagent
                    services.AddSingleton<ICodeReviewerSubagent, CodeReviewerSubagent>();
                })
                .Build();
        }
    }
}
