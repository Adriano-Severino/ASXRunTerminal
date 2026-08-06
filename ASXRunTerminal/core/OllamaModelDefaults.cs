namespace ASXRunTerminal.Core;

internal static class OllamaModelDefaults
{
    public const string DefaultModel = "qwen3.5:4b";
    public const string DefaultEmbeddingModel = "nomic-embed-text";
    public const string DefaultModelEnvironmentVariable = "ASXRUN_DEFAULT_MODEL";
    public const string DefaultEmbeddingModelEnvironmentVariable = "ASXRUN_DEFAULT_EMBEDDING_MODEL";
    public const string DefaultEndpointEnvironmentVariable = "ASXRUN_OLLAMA_ENDPOINT";

    public static readonly Uri DefaultEndpoint = new("http://127.0.0.1:11434/");

    public static string Resolve(
        string? configuredModel,
        Func<string, string?>? environmentVariableReader = null)
    {
        if (!string.IsNullOrWhiteSpace(configuredModel))
        {
            return configuredModel.Trim();
        }

        var readEnvironmentVariable = environmentVariableReader ?? Environment.GetEnvironmentVariable;
        var configuredByEnvironment = readEnvironmentVariable(DefaultModelEnvironmentVariable);
        return string.IsNullOrWhiteSpace(configuredByEnvironment)
            ? DefaultModel
            : configuredByEnvironment.Trim();
    }

    public static string ResolveEmbeddingModel(
        string? configuredModel,
        Func<string, string?>? environmentVariableReader = null)
    {
        if (!string.IsNullOrWhiteSpace(configuredModel))
        {
            return configuredModel.Trim();
        }

        var readEnvironmentVariable = environmentVariableReader ?? Environment.GetEnvironmentVariable;
        var configuredByEnvironment = readEnvironmentVariable(DefaultEmbeddingModelEnvironmentVariable);
        return string.IsNullOrWhiteSpace(configuredByEnvironment)
            ? DefaultEmbeddingModel
            : configuredByEnvironment.Trim();
    }

    public static Uri ResolveEndpoint(Func<string, string?>? environmentVariableReader = null)
    {
        var readEnvironmentVariable = environmentVariableReader ?? Environment.GetEnvironmentVariable;
        var configuredByEnvironment = readEnvironmentVariable(DefaultEndpointEnvironmentVariable);
        return string.IsNullOrWhiteSpace(configuredByEnvironment)
            ? DefaultEndpoint
            : new Uri(configuredByEnvironment.Trim());
    }
}
