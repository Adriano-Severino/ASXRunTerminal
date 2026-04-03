using ASXRunTerminal.Config;
using ASXRunTerminal.Core;

namespace ASXRunTerminal.Tests;

public sealed class UserConfigFileTests
{
    [Fact]
    public void EnsureExists_CreatesConfigFileInsideUserHome()
    {
        var testRoot = BuildTestRoot();
        var userHome = Path.Combine(testRoot, "home");

        try
        {
            var configPath = UserConfigFile.EnsureExists(() => userHome);

            Assert.Equal(
                Path.Combine(userHome, UserConfigFile.ConfigDirectoryName, UserConfigFile.ConfigFileName),
                configPath);
            Assert.True(File.Exists(configPath));

            var fileContent = File.ReadAllText(configPath);
            Assert.Contains("# ASXRunTerminal user config", fileContent);
            Assert.Contains($"default_model={OllamaModelDefaults.DefaultModel}", fileContent);
            Assert.Contains("ollama_host=http://127.0.0.1:11434/", fileContent);
            Assert.Contains("prompt_timeout_seconds=30", fileContent);
            Assert.Contains("healthcheck_timeout_seconds=3", fileContent);
            Assert.Contains("models_timeout_seconds=5", fileContent);
        }
        finally
        {
            DeleteTestRoot(testRoot);
        }
    }

    [Fact]
    public void EnsureExists_WhenFileAlreadyExists_DoesNotOverwriteContent()
    {
        var testRoot = BuildTestRoot();
        var userHome = Path.Combine(testRoot, "home");
        var configDirectory = Path.Combine(userHome, UserConfigFile.ConfigDirectoryName);
        var configPath = Path.Combine(configDirectory, UserConfigFile.ConfigFileName);

        try
        {
            Directory.CreateDirectory(configDirectory);
            File.WriteAllText(configPath, "default_model=llama3.2:latest");

            _ = UserConfigFile.EnsureExists(() => userHome);

            var fileContent = File.ReadAllText(configPath);
            Assert.Equal("default_model=llama3.2:latest", fileContent);
        }
        finally
        {
            DeleteTestRoot(testRoot);
        }
    }

    [Fact]
    public void EnsureExists_WhenUserHomeIsBlank_ThrowsInvalidOperationException()
    {
        var exception = Assert.Throws<InvalidOperationException>(
            () => UserConfigFile.EnsureExists(() => "   "));

        Assert.Contains("diretorio home", exception.Message);
    }

    [Fact]
    public void Load_WhenCustomValuesAreConfigured_ReturnsConfiguredRuntimeConfig()
    {
        var testRoot = BuildTestRoot();
        var userHome = Path.Combine(testRoot, "home");
        var configDirectory = Path.Combine(userHome, UserConfigFile.ConfigDirectoryName);
        var configPath = Path.Combine(configDirectory, UserConfigFile.ConfigFileName);

        try
        {
            Directory.CreateDirectory(configDirectory);
            File.WriteAllText(
                configPath,
                """
                ollama_host=http://localhost:8080
                default_model=qwen2.5-coder:7b
                prompt_timeout_seconds=45
                healthcheck_timeout_seconds=7
                models_timeout_seconds=12
                """);

            var config = UserConfigFile.Load(() => userHome);

            Assert.Equal(new Uri("http://localhost:8080/"), config.OllamaHost);
            Assert.Equal("qwen2.5-coder:7b", config.DefaultModel);
            Assert.Equal(TimeSpan.FromSeconds(45), config.PromptTimeout);
            Assert.Equal(TimeSpan.FromSeconds(7), config.HealthcheckTimeout);
            Assert.Equal(TimeSpan.FromSeconds(12), config.ModelsTimeout);
        }
        finally
        {
            DeleteTestRoot(testRoot);
        }
    }

    [Fact]
    public void Load_WhenValuesAreMissing_UsesDefaults()
    {
        var testRoot = BuildTestRoot();
        var userHome = Path.Combine(testRoot, "home");
        var configDirectory = Path.Combine(userHome, UserConfigFile.ConfigDirectoryName);
        var configPath = Path.Combine(configDirectory, UserConfigFile.ConfigFileName);

        try
        {
            Directory.CreateDirectory(configDirectory);
            File.WriteAllText(
                configPath,
                """
                default_model=llama3.2:latest
                """);

            var config = UserConfigFile.Load(() => userHome);

            Assert.Equal(new Uri("http://127.0.0.1:11434/"), config.OllamaHost);
            Assert.Equal("llama3.2:latest", config.DefaultModel);
            Assert.Equal(TimeSpan.FromSeconds(30), config.PromptTimeout);
            Assert.Equal(TimeSpan.FromSeconds(3), config.HealthcheckTimeout);
            Assert.Equal(TimeSpan.FromSeconds(5), config.ModelsTimeout);
        }
        finally
        {
            DeleteTestRoot(testRoot);
        }
    }

    [Fact]
    public void Load_WhenOllamaHostIsInvalid_ThrowsInvalidOperationException()
    {
        var testRoot = BuildTestRoot();
        var userHome = Path.Combine(testRoot, "home");
        var configDirectory = Path.Combine(userHome, UserConfigFile.ConfigDirectoryName);
        var configPath = Path.Combine(configDirectory, UserConfigFile.ConfigFileName);

        try
        {
            Directory.CreateDirectory(configDirectory);
            File.WriteAllText(
                configPath,
                """
                ollama_host=localhost:11434
                """);

            var exception = Assert.Throws<InvalidOperationException>(
                () => UserConfigFile.Load(() => userHome));

            Assert.Contains("ollama_host", exception.Message);
        }
        finally
        {
            DeleteTestRoot(testRoot);
        }
    }

    [Fact]
    public void Save_PersistsAllRuntimeConfigValues()
    {
        var testRoot = BuildTestRoot();
        var userHome = Path.Combine(testRoot, "home");
        var config = new UserRuntimeConfig(
            OllamaHost: new Uri("http://192.168.0.100:11434/", UriKind.Absolute),
            DefaultModel: "phi4-mini",
            PromptTimeout: TimeSpan.FromSeconds(61),
            HealthcheckTimeout: TimeSpan.FromSeconds(11),
            ModelsTimeout: TimeSpan.FromSeconds(9));

        try
        {
            UserConfigFile.Save(config, () => userHome);
            var savedConfig = UserConfigFile.Load(() => userHome);

            Assert.Equal(config, savedConfig);
        }
        finally
        {
            DeleteTestRoot(testRoot);
        }
    }

    [Fact]
    public void TryNormalizeSupportedKey_WhenKeyExists_ReturnsCanonicalKey()
    {
        var exists = UserConfigFile.TryNormalizeSupportedKey("DEFAULT_MODEL", out var normalizedKey);

        Assert.True(exists);
        Assert.Equal(UserConfigFile.DefaultModelKey, normalizedKey);
    }

    [Fact]
    public void TryNormalizeSupportedKey_WhenKeyIsBlank_ReturnsFalse()
    {
        var exists = UserConfigFile.TryNormalizeSupportedKey("   ", out var normalizedKey);

        Assert.False(exists);
        Assert.Null(normalizedKey);
    }

    [Fact]
    public void GetValue_WhenKeyExists_ReturnsNormalizedStringValue()
    {
        var config = new UserRuntimeConfig(
            OllamaHost: new Uri("http://localhost:8080", UriKind.Absolute),
            DefaultModel: "qwen2.5-coder:7b",
            PromptTimeout: TimeSpan.FromSeconds(41),
            HealthcheckTimeout: TimeSpan.FromSeconds(8),
            ModelsTimeout: TimeSpan.FromSeconds(13));

        var hostValue = UserConfigFile.GetValue(config, UserConfigFile.OllamaHostKey);
        var timeoutValue = UserConfigFile.GetValue(config, UserConfigFile.PromptTimeoutSecondsKey);

        Assert.Equal("http://localhost:8080/", hostValue);
        Assert.Equal("41", timeoutValue);
    }

    [Fact]
    public void GetValue_WhenKeyIsInvalid_ThrowsInvalidOperationException()
    {
        var exception = Assert.Throws<InvalidOperationException>(
            () => UserConfigFile.GetValue(UserRuntimeConfig.Default, "invalid_key"));

        Assert.Contains("nao e suportada", exception.Message);
    }

    [Fact]
    public void SetValue_WhenTimeoutIsValid_ReturnsUpdatedConfig()
    {
        var initial = UserRuntimeConfig.Default;

        var updated = UserConfigFile.SetValue(
            initial,
            UserConfigFile.ModelsTimeoutSecondsKey,
            "17");

        Assert.Equal(TimeSpan.FromSeconds(17), updated.ModelsTimeout);
    }

    [Fact]
    public void SetValue_WhenDefaultModelHasExtraSpaces_TrimsAndUpdatesValue()
    {
        var updated = UserConfigFile.SetValue(
            UserRuntimeConfig.Default,
            UserConfigFile.DefaultModelKey,
            "   qwen2.5-coder:7b   ");

        Assert.Equal("qwen2.5-coder:7b", updated.DefaultModel);
    }

    [Fact]
    public void SetValue_WhenValueIsBlank_ThrowsInvalidOperationException()
    {
        var exception = Assert.Throws<InvalidOperationException>(
            () => UserConfigFile.SetValue(
                UserRuntimeConfig.Default,
                UserConfigFile.DefaultModelKey,
                "   "));

        Assert.Contains("nao pode estar vazio", exception.Message);
    }

    [Fact]
    public void SetValue_WhenOllamaHostUsesInvalidScheme_ThrowsInvalidOperationException()
    {
        var exception = Assert.Throws<InvalidOperationException>(
            () => UserConfigFile.SetValue(
                UserRuntimeConfig.Default,
                UserConfigFile.OllamaHostKey,
                "ftp://localhost:11434"));

        Assert.Contains("deve usar os esquemas 'http' ou 'https'", exception.Message);
    }

    [Fact]
    public void SetValue_WhenKeyIsInvalid_ThrowsInvalidOperationException()
    {
        var exception = Assert.Throws<InvalidOperationException>(
            () => UserConfigFile.SetValue(UserRuntimeConfig.Default, "invalid_key", "value"));

        Assert.Contains("nao e suportada", exception.Message);
    }

    [Fact]
    public void Load_WhenConfigLineHasInvalidFormat_ThrowsInvalidOperationException()
    {
        var testRoot = BuildTestRoot();
        var userHome = Path.Combine(testRoot, "home");
        var configDirectory = Path.Combine(userHome, UserConfigFile.ConfigDirectoryName);
        var configPath = Path.Combine(configDirectory, UserConfigFile.ConfigFileName);

        try
        {
            Directory.CreateDirectory(configDirectory);
            File.WriteAllText(
                configPath,
                "default_model");

            var exception = Assert.Throws<InvalidOperationException>(
                () => UserConfigFile.Load(() => userHome));

            Assert.Contains("Use o formato chave=valor", exception.Message);
        }
        finally
        {
            DeleteTestRoot(testRoot);
        }
    }

    [Fact]
    public void Load_WhenTimeoutIsNotPositive_ThrowsInvalidOperationException()
    {
        var testRoot = BuildTestRoot();
        var userHome = Path.Combine(testRoot, "home");
        var configDirectory = Path.Combine(userHome, UserConfigFile.ConfigDirectoryName);
        var configPath = Path.Combine(configDirectory, UserConfigFile.ConfigFileName);

        try
        {
            Directory.CreateDirectory(configDirectory);
            File.WriteAllText(
                configPath,
                "prompt_timeout_seconds=0");

            var exception = Assert.Throws<InvalidOperationException>(
                () => UserConfigFile.Load(() => userHome));

            Assert.Contains("inteiro positivo em segundos", exception.Message);
        }
        finally
        {
            DeleteTestRoot(testRoot);
        }
    }

    private static string BuildTestRoot()
    {
        return Path.Combine(
            Path.GetTempPath(),
            "asxrun-config-tests",
            Guid.NewGuid().ToString("N"));
    }

    private static void DeleteTestRoot(string testRoot)
    {
        if (Directory.Exists(testRoot))
        {
            Directory.Delete(testRoot, recursive: true);
        }
    }
}
