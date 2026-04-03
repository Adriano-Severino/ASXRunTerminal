namespace ASXRunTerminal.Core;

internal static class OllamaModelDefaults
{
    public const string DefaultModel = "qwen3.5:4b";
    public const string DefaultModelEnvironmentVariable = "ASXRUN_DEFAULT_MODEL";

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
}
