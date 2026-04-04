using System.Globalization;
using ASXRunTerminal.Core;

namespace ASXRunTerminal.Config;

internal static class UserConfigFile
{
    internal const string ConfigDirectoryName = ".asxrun";
    internal const string ConfigFileName = "config";
    internal const string OllamaHostKey = "ollama_host";
    internal const string DefaultModelKey = "default_model";
    internal const string PromptTimeoutSecondsKey = "prompt_timeout_seconds";
    internal const string HealthcheckTimeoutSecondsKey = "healthcheck_timeout_seconds";
    internal const string ModelsTimeoutSecondsKey = "models_timeout_seconds";
    internal const string ThemeKey = "theme";

    private static readonly UserRuntimeConfig DefaultConfig = UserRuntimeConfig.Default;
    internal static readonly IReadOnlyList<string> SupportedKeys =
    [
        OllamaHostKey,
        DefaultModelKey,
        PromptTimeoutSecondsKey,
        HealthcheckTimeoutSecondsKey,
        ModelsTimeoutSecondsKey,
        ThemeKey
    ];

    internal static bool TryNormalizeSupportedKey(string key, out string? normalizedKey)
    {
        normalizedKey = null;
        if (string.IsNullOrWhiteSpace(key))
        {
            return false;
        }

        var candidate = key.Trim();
        foreach (var supportedKey in SupportedKeys)
        {
            if (string.Equals(candidate, supportedKey, StringComparison.OrdinalIgnoreCase))
            {
                normalizedKey = supportedKey;
                return true;
            }
        }

        return false;
    }

    internal static string GetValue(UserRuntimeConfig config, string key)
    {
        var normalizedKey = NormalizeSupportedKeyOrThrow(key);
        return normalizedKey switch
        {
            OllamaHostKey => NormalizeUri(config.OllamaHost).AbsoluteUri,
            DefaultModelKey => config.DefaultModel,
            PromptTimeoutSecondsKey => ((int)config.PromptTimeout.TotalSeconds).ToString(CultureInfo.InvariantCulture),
            HealthcheckTimeoutSecondsKey => ((int)config.HealthcheckTimeout.TotalSeconds).ToString(CultureInfo.InvariantCulture),
            ModelsTimeoutSecondsKey => ((int)config.ModelsTimeout.TotalSeconds).ToString(CultureInfo.InvariantCulture),
            ThemeKey => (string)(TerminalTheme)config.Theme,
            _ => throw new InvalidOperationException($"A chave de configuracao '{normalizedKey}' nao e suportada.")
        };
    }

    internal static UserRuntimeConfig SetValue(UserRuntimeConfig currentConfig, string key, string rawValue)
    {
        var normalizedKey = NormalizeSupportedKeyOrThrow(key);
        if (string.IsNullOrWhiteSpace(rawValue))
        {
            throw new InvalidOperationException(
                $"O valor para a chave '{normalizedKey}' nao pode estar vazio.");
        }

        var trimmedValue = rawValue.Trim();
        return normalizedKey switch
        {
            OllamaHostKey => currentConfig with { OllamaHost = ResolveOllamaHost(trimmedValue) },
            DefaultModelKey => currentConfig with { DefaultModel = trimmedValue },
            PromptTimeoutSecondsKey => currentConfig with
            {
                PromptTimeout = ResolveTimeoutInSeconds(
                    trimmedValue,
                    PromptTimeoutSecondsKey,
                    DefaultConfig.PromptTimeout)
            },
            HealthcheckTimeoutSecondsKey => currentConfig with
            {
                HealthcheckTimeout = ResolveTimeoutInSeconds(
                    trimmedValue,
                    HealthcheckTimeoutSecondsKey,
                    DefaultConfig.HealthcheckTimeout)
            },
            ModelsTimeoutSecondsKey => currentConfig with
            {
                ModelsTimeout = ResolveTimeoutInSeconds(
                    trimmedValue,
                    ModelsTimeoutSecondsKey,
                    DefaultConfig.ModelsTimeout)
            },
            ThemeKey => currentConfig with
            {
                Theme = ResolveTheme(trimmedValue)
            },
            _ => throw new InvalidOperationException($"A chave de configuracao '{normalizedKey}' nao e suportada.")
        };
    }

    public static string EnsureExists(Func<string?>? userHomeResolver = null)
    {
        var configPath = GetConfigPath(userHomeResolver);
        var configDirectory = Path.GetDirectoryName(configPath)
            ?? throw new InvalidOperationException(
                "Nao foi possivel resolver o diretorio de configuracao do usuario.");

        Directory.CreateDirectory(configDirectory);
        if (File.Exists(configPath))
        {
            return configPath;
        }

        File.WriteAllText(configPath, BuildConfigFileContent(DefaultConfig));
        return configPath;
    }

    public static UserRuntimeConfig Load(Func<string?>? userHomeResolver = null)
    {
        var configPath = EnsureExists(userHomeResolver);
        var rawEntries = ReadRawEntries(configPath);
        UserRuntimeConfig runtimeConfig = rawEntries;
        return runtimeConfig;
    }

    public static void Save(UserRuntimeConfig config, Func<string?>? userHomeResolver = null)
    {
        var configPath = EnsureExists(userHomeResolver);
        File.WriteAllText(configPath, BuildConfigFileContent(config));
    }

    internal static string GetConfigPath(Func<string?>? userHomeResolver = null)
    {
        var userHome = ResolveUserHome(userHomeResolver);
        return Path.Combine(userHome, ConfigDirectoryName, ConfigFileName);
    }

    private static string ResolveUserHome(Func<string?>? userHomeResolver)
    {
        var resolver = userHomeResolver ?? ResolveUserHomeFromEnvironment;
        var userHome = resolver();

        if (string.IsNullOrWhiteSpace(userHome))
        {
            throw new InvalidOperationException(
                "Nao foi possivel resolver o diretorio home do usuario para criar o arquivo de configuracao.");
        }

        return userHome.Trim();
    }

    private static string? ResolveUserHomeFromEnvironment()
    {
        return Environment.GetFolderPath(Environment.SpecialFolder.UserProfile);
    }

    private static string NormalizeSupportedKeyOrThrow(string key)
    {
        if (TryNormalizeSupportedKey(key, out var normalizedKey) && normalizedKey is not null)
        {
            return normalizedKey;
        }

        var value = key?.Trim() ?? string.Empty;
        throw new InvalidOperationException(
            $"A chave de configuracao '{value}' nao e suportada.");
    }

    private static string BuildConfigFileContent(UserRuntimeConfig config)
    {
        return
            $"""
            # ASXRunTerminal user config
            # This file is created automatically on first run.
            # Edit values as needed.
            {OllamaHostKey}={NormalizeUri(config.OllamaHost)}
            {DefaultModelKey}={config.DefaultModel}
            {PromptTimeoutSecondsKey}={(int)config.PromptTimeout.TotalSeconds}
            {HealthcheckTimeoutSecondsKey}={(int)config.HealthcheckTimeout.TotalSeconds}
            {ModelsTimeoutSecondsKey}={(int)config.ModelsTimeout.TotalSeconds}
            {ThemeKey}={(string)(TerminalTheme)config.Theme}
            {Environment.NewLine}
            """;
    }

    private static RawConfigEntries ReadRawEntries(string configPath)
    {
        var entries = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
        var lines = File.ReadAllLines(configPath);

        for (var index = 0; index < lines.Length; index++)
        {
            var lineNumber = index + 1;
            var trimmedLine = lines[index].Trim();

            if (trimmedLine.Length == 0 || trimmedLine.StartsWith('#'))
            {
                continue;
            }

            var separatorIndex = trimmedLine.IndexOf('=');
            if (separatorIndex <= 0)
            {
                throw new InvalidOperationException(
                    $"Linha de configuracao invalida em '{configPath}' (linha {lineNumber}). Use o formato chave=valor.");
            }

            var key = trimmedLine[..separatorIndex].Trim();
            var value = trimmedLine[(separatorIndex + 1)..].Trim();

            if (key.Length == 0)
            {
                throw new InvalidOperationException(
                    $"Linha de configuracao invalida em '{configPath}' (linha {lineNumber}). A chave nao pode estar vazia.");
            }

            entries[key] = value;
        }

        return new RawConfigEntries(
            OllamaHost: GetEntryOrNull(entries, OllamaHostKey),
            DefaultModel: GetEntryOrNull(entries, DefaultModelKey),
            PromptTimeoutSeconds: GetEntryOrNull(entries, PromptTimeoutSecondsKey),
            HealthcheckTimeoutSeconds: GetEntryOrNull(entries, HealthcheckTimeoutSecondsKey),
            ModelsTimeoutSeconds: GetEntryOrNull(entries, ModelsTimeoutSecondsKey),
            Theme: GetEntryOrNull(entries, ThemeKey));
    }

    private static string? GetEntryOrNull(IReadOnlyDictionary<string, string> entries, string key)
    {
        return entries.TryGetValue(key, out var value)
            ? value
            : null;
    }

    private static Uri ResolveOllamaHost(string? configuredHost)
    {
        if (string.IsNullOrWhiteSpace(configuredHost))
        {
            return DefaultConfig.OllamaHost;
        }

        var trimmedHost = configuredHost.Trim();
        if (!Uri.TryCreate(trimmedHost, UriKind.Absolute, out var uri))
        {
            throw new InvalidOperationException(
                $"O valor '{OllamaHostKey}' no arquivo de configuracao deve ser uma URL absoluta.");
        }

        if (!string.Equals(uri.Scheme, Uri.UriSchemeHttp, StringComparison.OrdinalIgnoreCase)
            && !string.Equals(uri.Scheme, Uri.UriSchemeHttps, StringComparison.OrdinalIgnoreCase))
        {
            throw new InvalidOperationException(
                $"O valor '{OllamaHostKey}' no arquivo de configuracao deve usar os esquemas 'http' ou 'https'.");
        }

        return NormalizeUri(uri);
    }

    private static string ResolveDefaultModel(string? configuredModel)
    {
        return string.IsNullOrWhiteSpace(configuredModel)
            ? DefaultConfig.DefaultModel
            : configuredModel.Trim();
    }

    private static TimeSpan ResolveTimeoutInSeconds(string? configuredValue, string key, TimeSpan fallback)
    {
        if (string.IsNullOrWhiteSpace(configuredValue))
        {
            return fallback;
        }

        if (!int.TryParse(configuredValue, NumberStyles.Integer, CultureInfo.InvariantCulture, out var parsedSeconds)
            || parsedSeconds <= 0)
        {
            throw new InvalidOperationException(
                $"O valor '{key}' no arquivo de configuracao deve ser um inteiro positivo em segundos.");
        }

        return TimeSpan.FromSeconds(parsedSeconds);
    }

    private static TerminalThemeMode ResolveTheme(string? configuredTheme)
    {
        if (string.IsNullOrWhiteSpace(configuredTheme))
        {
            return DefaultConfig.Theme;
        }

        TerminalTheme theme = new(configuredTheme.Trim());
        return (TerminalThemeMode)theme;
    }

    private static Uri NormalizeUri(Uri uri)
    {
        var uriString = uri.AbsoluteUri.Trim();
        if (!uriString.EndsWith('/'))
        {
            uriString = $"{uriString}/";
        }

        return new Uri(uriString, UriKind.Absolute);
    }

    private readonly record struct RawConfigEntries(
        string? OllamaHost,
        string? DefaultModel,
        string? PromptTimeoutSeconds,
        string? HealthcheckTimeoutSeconds,
        string? ModelsTimeoutSeconds,
        string? Theme)
    {
        public static implicit operator UserRuntimeConfig(RawConfigEntries entries)
        {
            return new UserRuntimeConfig(
                OllamaHost: ResolveOllamaHost(entries.OllamaHost),
                DefaultModel: ResolveDefaultModel(entries.DefaultModel),
                PromptTimeout: ResolveTimeoutInSeconds(
                    entries.PromptTimeoutSeconds,
                    PromptTimeoutSecondsKey,
                    DefaultConfig.PromptTimeout),
                HealthcheckTimeout: ResolveTimeoutInSeconds(
                    entries.HealthcheckTimeoutSeconds,
                    HealthcheckTimeoutSecondsKey,
                    DefaultConfig.HealthcheckTimeout),
                ModelsTimeout: ResolveTimeoutInSeconds(
                    entries.ModelsTimeoutSeconds,
                    ModelsTimeoutSecondsKey,
                    DefaultConfig.ModelsTimeout),
                Theme: ResolveTheme(entries.Theme));
        }
    }
}
