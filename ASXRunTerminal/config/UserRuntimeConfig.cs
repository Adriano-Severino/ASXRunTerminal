using ASXRunTerminal.Core;

namespace ASXRunTerminal.Config;

internal readonly record struct UserRuntimeConfig(
    Uri OllamaHost,
    string DefaultModel,
    TimeSpan PromptTimeout,
    TimeSpan HealthcheckTimeout,
    TimeSpan ModelsTimeout)
{
    public static readonly Uri DefaultOllamaHost = new("http://127.0.0.1:11434/", UriKind.Absolute);
    public static readonly TimeSpan DefaultPromptTimeout = TimeSpan.FromSeconds(30);
    public static readonly TimeSpan DefaultHealthcheckTimeout = TimeSpan.FromSeconds(3);
    public static readonly TimeSpan DefaultModelsTimeout = TimeSpan.FromSeconds(5);

    public static UserRuntimeConfig Default =>
        new(
            OllamaHost: DefaultOllamaHost,
            DefaultModel: OllamaModelDefaults.DefaultModel,
            PromptTimeout: DefaultPromptTimeout,
            HealthcheckTimeout: DefaultHealthcheckTimeout,
            ModelsTimeout: DefaultModelsTimeout);
}
