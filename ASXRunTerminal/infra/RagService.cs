using ASXRunTerminal.Core;
using ASXRunTerminal.Subagents;
using ASXRunTerminal.Config;
using Microsoft.Extensions.AI;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.DependencyInjection.Extensions;
using Microsoft.Extensions.Logging;
using System;
namespace ASXRunTerminal.Infra
{
    public static class RagService
    {
        public static IHost Initialize(Config.LoggingConfiguration? loggingConfig = null)
        {
            var config = loggingConfig ?? LoggingConfiguration.Default;

            return Host.CreateDefaultBuilder()
                .ConfigureServices((context, services) =>
                {
                    // Register structured logging
                    services.AddSingleton(config);
                    services.AddSingleton<StructuredLoggerProvider>(sp =>
                        new StructuredLoggerProvider(config, Guid.NewGuid().ToString("N")));
                    services.AddSingleton<ILoggerFactory>(sp =>
                    {
                        var provider = sp.GetRequiredService<StructuredLoggerProvider>();
                        return new StructuredLoggerFactory(provider);
                    });

                    // Vector store configuration
                    var configPath = Path.Combine(
                        Environment.GetFolderPath(Environment.SpecialFolder.UserProfile),
                        ".asxrun",
                        "vector-store.db");
                    var vectorConfig = VectorStoreConfiguration.CreatePersistent(configPath);

                    services.AddSingleton(vectorConfig);

                    // Ollama HTTP client setup with logging
                    services.AddHttpClient<OllamaHttpClient>();
                    services.AddSingleton<OllamaHttpClient>(sp =>
                    {
                        var httpClient = sp.GetRequiredService<HttpClient>();
                        var logger = sp.GetRequiredService<ILogger<OllamaHttpClient>>();
                        return new OllamaHttpClient(httpClient, null, null, OllamaModelDefaults.DefaultEndpoint, null, logger);
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
                        var logger = sp.GetRequiredService<ILogger<OllamaEmbeddingGenerator>>();
                        return new OllamaEmbeddingGenerator(httpClient, vectorConfig.EmbeddingModel, null, logger);
                    });

                    // Register vector store
                    services.AddSingleton<SqliteVectorStore>(sp =>
                    {
                        var logger = sp.GetRequiredService<ILogger<SqliteVectorStore>>();
                        return new SqliteVectorStore(vectorConfig, logger);
                    });

                    // Register code reviewer subagent
                    services.AddSingleton<ICodeReviewerSubagent, CodeReviewerSubagent>();
                })
                .Build();
        }
    }

    internal sealed class StructuredLoggerFactory : ILoggerFactory
    {
        private readonly StructuredLoggerProvider _provider;

        public StructuredLoggerFactory(StructuredLoggerProvider provider)
        {
            _provider = provider;
        }

        public void AddProvider(ILoggerProvider provider)
        {
            // Not supported for structured logger
        }

        public ILogger CreateLogger(string categoryName)
        {
            return _provider.CreateLogger(categoryName);
        }

        public void Dispose()
        {
            _provider.Dispose();
        }
    }
}
