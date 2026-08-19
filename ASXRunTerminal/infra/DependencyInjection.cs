using ASXRunTerminal.Config;
using ASXRunTerminal.Core;
using ASXRunTerminal.Subagents;
using ASXRunTerminal.Commands;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;

namespace ASXRunTerminal.Infra;

/// <summary>
/// Dependency injection configuration for ASXRunTerminal services.
/// </summary>
internal static class DependencyInjection
{
    /// <summary>
    /// Configures all core services for the application.
    /// </summary>
    public static IServiceCollection AddCoreServices(this IServiceCollection services, Config.LoggingConfiguration? loggingConfig = null)
    {
        var config = loggingConfig ?? Config.LoggingConfiguration.Default;

        // Register logging configuration
        services.AddSingleton(config);
        services.AddSingleton<StructuredLoggerProvider>(sp =>
            new StructuredLoggerProvider(config, Guid.NewGuid().ToString("N")));
        services.AddSingleton<ILoggerFactory>(sp =>
        {
            var provider = sp.GetRequiredService<StructuredLoggerProvider>();
            return new StructuredLoggerFactory(provider);
        });

        // Register HTTP clients
        services.AddHttpClient();
        services.AddSingleton<OllamaHttpClient>(sp =>
        {
            var httpClient = sp.GetRequiredService<HttpClient>();
            var logger = sp.GetRequiredService<ILogger<OllamaHttpClient>>();
            return new OllamaHttpClient(httpClient, null, null, OllamaModelDefaults.DefaultEndpoint, null, logger);
        });

        // Register vector store configuration
        var configPath = Path.Combine(
            Environment.GetFolderPath(Environment.SpecialFolder.UserProfile),
            ".asxrun",
            "vector-store.db");
        var vectorConfig = VectorStoreConfiguration.CreatePersistent(configPath);
        services.AddSingleton(vectorConfig);

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

        // Register chat client
        services.AddSingleton(sp =>
        {
            var ollamaHttpClient = sp.GetRequiredService<OllamaHttpClient>();
            return ollamaHttpClient.ChatClient;
        });

        // Register code reviewer subagent
        services.AddSingleton<ICodeReviewerSubagent, CodeReviewerSubagent>();

        // Register tool runtime
        services.AddSingleton<IToolRuntime, ToolRuntime>();

        // Register MCP clients
        services.AddSingleton<IMcpClientFactory, McpClientFactory>();

        return services;
    }

    /// <summary>
    /// Registers command handlers with the DI container.
    /// </summary>
    public static IServiceCollection AddCommands(this IServiceCollection services)
    {
        // Register all commands as singletons
        services.AddSingleton<AskCommand>();
        services.AddSingleton<ChatCommand>();
        services.AddSingleton<AgentCommand>();
        services.AddSingleton<CodeReviewCommand>();
        services.AddSingleton<DoctorCommand>();
        services.AddSingleton<ModelsCommand>();
        services.AddSingleton<ContextCommand>();
        services.AddSingleton<PatchCommand>();
        services.AddSingleton<HistoryCommand>();
        services.AddSingleton<ResumeCommand>();
        services.AddSingleton<McpCommand>();
        services.AddSingleton<ConfigCommand>();
        services.AddSingleton<SkillsCommand>();
        services.AddSingleton<SkillCommand>();
        services.AddSingleton<HelpCommand>();
        services.AddSingleton<VersionCommand>();

        // Register command registry
        services.AddSingleton<CommandRegistry>();

        return services;
    }

    /// <summary>
    /// Registers configuration services.
    /// </summary>
    public static IServiceCollection AddConfigurationServices(this IServiceCollection services)
    {
        // Configuration file classes are static, so they don't need DI registration
        // They are used directly via their static methods

        return services;
    }

    /// <summary>
    /// Creates a fully configured service provider with all services.
    /// </summary>
    public static IServiceProvider CreateServiceProvider(Config.LoggingConfiguration? loggingConfig = null)
    {
        var services = new ServiceCollection();

        services.AddCoreServices(loggingConfig);
        services.AddCommands();
        services.AddConfigurationServices();

        return services.BuildServiceProvider();
    }

    /// <summary>
    /// Creates a host with all services configured.
    /// </summary>
    public static IHost CreateHost(Config.LoggingConfiguration? loggingConfig = null)
    {
        return Host.CreateDefaultBuilder()
            .ConfigureServices((context, services) =>
            {
                services.AddCoreServices(loggingConfig);
                services.AddCommands();
                services.AddConfigurationServices();
            })
            .Build();
    }
}

/// <summary>
/// Factory for creating MCP clients.
/// </summary>
internal interface IMcpClientFactory
{
    IMcpClient CreateStdioClient(McpServerProcessOptions options);
    IMcpClient CreateRemoteClient(McpServerRemoteOptions options);
}

/// <summary>
/// Default implementation of MCP client factory.
/// </summary>
internal sealed class McpClientFactory : IMcpClientFactory
{
    private readonly ILogger<McpClientFactory> _logger;

    public McpClientFactory(ILogger<McpClientFactory>? logger = null)
    {
        _logger = logger ?? Microsoft.Extensions.Logging.Abstractions.NullLogger<McpClientFactory>.Instance;
    }

    public IMcpClient CreateStdioClient(McpServerProcessOptions options)
    {
        _logger.LogDebug("Creating MCP stdio client for command: {Command}", options.Command);
        return new McpStdioClient(options);
    }

    public IMcpClient CreateRemoteClient(McpServerRemoteOptions options)
    {
        _logger.LogDebug("Creating MCP remote client for endpoint: {Endpoint}", options.Endpoint);
        var remoteClientLogger = Microsoft.Extensions.Logging.Abstractions.NullLogger<McpRemoteClient>.Instance;
        return new McpRemoteClient(options, null, null, remoteClientLogger);
    }
}
