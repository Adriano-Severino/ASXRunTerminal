using ASXRunTerminal.Config;
using ASXRunTerminal.Infra;
using Microsoft.Extensions.Logging;

namespace ASXRunTerminal.Core;

/// <summary>
/// Validates application configuration at startup.
/// </summary>
internal sealed class ConfigurationValidator
{
    private readonly ILogger<ConfigurationValidator> _logger;
    private readonly IOllamaHttpClient _ollamaHttpClient;
    private readonly UserRuntimeConfig _config;

    public ConfigurationValidator(
        IOllamaHttpClient ollamaHttpClient,
        ILogger<ConfigurationValidator>? logger = null)
    {
        _ollamaHttpClient = ollamaHttpClient ?? throw new ArgumentNullException(nameof(ollamaHttpClient));
        _config = UserConfigFile.Load();
        _logger = logger ?? Microsoft.Extensions.Logging.Abstractions.NullLogger<ConfigurationValidator>.Instance;
    }

    /// <summary>
    /// Performs comprehensive configuration validation.
    /// </summary>
    public async Task<ConfigurationValidationResult> ValidateAsync(CancellationToken cancellationToken = default)
    {
        _logger.LogInformation("Starting configuration validation");

        var result = new ConfigurationValidationResult();
        var stopwatch = System.Diagnostics.Stopwatch.StartNew();

        // Validate Ollama connectivity
        result.OllamaHealthCheck = await ValidateOllamaConnectivityAsync(cancellationToken);

        // Validate MCP server configurations
        result.McpServerValidation = await ValidateMcpServersAsync(cancellationToken);

        // Validate workspace permissions
        result.WorkspacePermissionValidation = ValidateWorkspacePermissions();

        // Validate configuration file integrity
        result.ConfigFileValidation = ValidateConfigFileIntegrity();

        stopwatch.Stop();
        result.ValidationDurationMs = stopwatch.ElapsedMilliseconds;
        result.IsValid = result.OllamaHealthCheck.IsValid &&
                         result.McpServerValidation.IsValid &&
                         result.WorkspacePermissionValidation.IsValid &&
                         result.ConfigFileValidation.IsValid;

        _logger.LogInformation("Configuration validation completed in {DurationMs}ms. Valid: {IsValid}",
            result.ValidationDurationMs, result.IsValid);

        return result;
    }

    /// <summary>
    /// Validates Ollama connectivity and availability.
    /// </summary>
    private async Task<OllamaHealthValidation> ValidateOllamaConnectivityAsync(CancellationToken cancellationToken)
    {
        _logger.LogDebug("Validating Ollama connectivity");

        var result = new OllamaHealthValidation();

        try
        {
            var healthCheck = await _ollamaHttpClient.CheckHealthAsync(cancellationToken);

            result.IsValid = healthCheck.IsHealthy;
            result.ErrorMessage = healthCheck.Error;
            result.Version = healthCheck.Version;

            if (result.IsValid)
            {
                _logger.LogInformation("Ollama health check passed. Version: {Version}", result.Version);
            }
            else
            {
                _logger.LogWarning("Ollama health check failed: {ErrorMessage}", result.ErrorMessage);
            }
        }
        catch (Exception ex)
        {
            result.IsValid = false;
            result.ErrorMessage = ex.Message;
            _logger.LogError(ex, "Ollama health check failed with exception");
        }

        return result;
    }

    /// <summary>
    /// Validates MCP server configurations.
    /// </summary>
    private async Task<McpServerValidation> ValidateMcpServersAsync(CancellationToken cancellationToken)
    {
        _logger.LogDebug("Validating MCP server configurations");

        var result = new McpServerValidation();
        var servers = McpServerCatalogFile.Load();

        if (servers.Count == 0)
        {
            _logger.LogDebug("No MCP servers configured");
            result.IsValid = true;
            return result;
        }

        var validationResults = new List<McpServerTestResult>();

        foreach (var server in servers)
        {
            try
            {
                _logger.LogDebug("Testing MCP server: {ServerName}", server.Name);

                var testResult = await TestMcpServerAsync(server, cancellationToken);
                validationResults.Add(testResult);

                if (!testResult.IsSuccess)
                {
                    result.IsValid = false;
                    result.FailedServers.Add(server.Name, testResult.Detail ?? "Unknown error");
                    _logger.LogWarning("MCP server {ServerName} test failed: {ErrorMessage}", server.Name, testResult.Detail);
                }
                else
                {
                    result.SuccessfulServers.Add(server.Name);
                    _logger.LogDebug("MCP server {ServerName} test passed", server.Name);
                }
            }
            catch (Exception ex)
            {
                result.IsValid = false;
                result.FailedServers.Add(server.Name, ex.Message);
                _logger.LogError(ex, "MCP server {ServerName} test failed with exception", server.Name);
            }
        }

        result.TotalServers = servers.Count;
        result.SuccessfulServerCount = result.SuccessfulServers.Count;

        _logger.LogInformation("MCP server validation completed: {SuccessCount}/{TotalCount} passed",
            result.SuccessfulServerCount, result.TotalServers);

        return result;
    }

    /// <summary>
    /// Tests a single MCP server.
    /// </summary>
    private async Task<McpServerTestResult> TestMcpServerAsync(McpServerDefinition server, CancellationToken cancellationToken)
    {
        // This would normally use the actual MCP client to test connectivity
        // For now, we'll simulate a basic validation
        await Task.Delay(100, cancellationToken);

        if (server.IsStdio && server.ProcessOptions == null)
        {
            return McpServerTestResult.Failure("Stdio server requires process options");
        }

        if (!server.IsStdio && server.RemoteOptions == null)
        {
            return McpServerTestResult.Failure("Remote server requires remote options");
        }

        return McpServerTestResult.Success("Server configuration validated");
    }

    /// <summary>
    /// Validates workspace permissions.
    /// </summary>
    private WorkspacePermissionValidation ValidateWorkspacePermissions()
    {
        _logger.LogDebug("Validating workspace permissions");

        var result = new WorkspacePermissionValidation();

        try
        {
            var currentDirectory = Directory.GetCurrentDirectory();
            result.CurrentDirectory = currentDirectory;

            // Check if we can read the current directory
            var files = Directory.GetFiles(currentDirectory, "*", SearchOption.TopDirectoryOnly);
            result.CanReadCurrentDirectory = true;
            result.FileCount = files.Length;

            // Check if we can write to a temp location
            var tempPath = Path.Combine(currentDirectory, ".asxrun_temp_test");
            try
            {
                File.WriteAllText(tempPath, "test");
                File.Delete(tempPath);
                result.CanWriteToWorkspace = true;
            }
            catch
            {
                result.CanWriteToWorkspace = false;
                result.ErrorMessage = "Cannot write to workspace directory";
            }

            result.IsValid = result.CanReadCurrentDirectory && result.CanWriteToWorkspace;

            if (result.IsValid)
            {
                _logger.LogDebug("Workspace permissions validated successfully");
            }
            else
            {
                _logger.LogWarning("Workspace permission validation failed: {ErrorMessage}", result.ErrorMessage);
            }
        }
        catch (Exception ex)
        {
            result.IsValid = false;
            result.ErrorMessage = ex.Message;
            _logger.LogError(ex, "Workspace permission validation failed with exception");
        }

        return result;
    }

    /// <summary>
    /// Validates configuration file integrity.
    /// </summary>
    private ConfigFileValidation ValidateConfigFileIntegrity()
    {
        _logger.LogDebug("Validating configuration file integrity");

        var result = new ConfigFileValidation();

        try
        {
            var config = UserConfigFile.Load();
            result.ConfigExists = true;
            result.Theme = config.Theme.ToString();
            result.Model = config.DefaultModel;

            // Validate critical configuration values
            if (string.IsNullOrEmpty(config.DefaultModel))
            {
                result.IsValid = false;
                result.ErrorMessage = "Default model is not configured";
                _logger.LogWarning("Configuration validation failed: Default model is not configured");
            }
            else
            {
                result.IsValid = true;
                _logger.LogDebug("Configuration file integrity validated");
            }
        }
        catch (Exception ex)
        {
            result.IsValid = false;
            result.ErrorMessage = ex.Message;
            _logger.LogError(ex, "Configuration file validation failed with exception");
        }

        return result;
    }
}

/// <summary>
/// Result of configuration validation.
/// </summary>
internal sealed class ConfigurationValidationResult
{
    public bool IsValid { get; set; }
    public long ValidationDurationMs { get; set; }
    public OllamaHealthValidation OllamaHealthCheck { get; set; } = new();
    public McpServerValidation McpServerValidation { get; set; } = new();
    public WorkspacePermissionValidation WorkspacePermissionValidation { get; set; } = new();
    public ConfigFileValidation ConfigFileValidation { get; set; } = new();

    public List<string> GetValidationErrors()
    {
        var errors = new List<string>();

        if (!OllamaHealthCheck.IsValid)
        {
            errors.Add($"Ollama health check failed: {OllamaHealthCheck.ErrorMessage}");
        }

        if (!McpServerValidation.IsValid)
        {
            errors.Add($"MCP server validation failed: {McpServerValidation.FailedServers.Count} servers failed");
        }

        if (!WorkspacePermissionValidation.IsValid)
        {
            errors.Add($"Workspace permission validation failed: {WorkspacePermissionValidation.ErrorMessage}");
        }

        if (!ConfigFileValidation.IsValid)
        {
            errors.Add($"Configuration file validation failed: {ConfigFileValidation.ErrorMessage}");
        }

        return errors;
    }
}

/// <summary>
/// Ollama health validation result.
/// </summary>
internal sealed class OllamaHealthValidation
{
    public bool IsValid { get; set; }
    public string? ErrorMessage { get; set; }
    public string? Version { get; set; }
}

/// <summary>
/// MCP server validation result.
/// </summary>
internal sealed class McpServerValidation
{
    public bool IsValid { get; set; } = true;
    public int TotalServers { get; set; }
    public int SuccessfulServerCount { get; set; }
    public List<string> SuccessfulServers { get; set; } = new();
    public Dictionary<string, string> FailedServers { get; set; } = new();
}

/// <summary>
/// Workspace permission validation result.
/// </summary>
internal sealed class WorkspacePermissionValidation
{
    public bool IsValid { get; set; }
    public string? CurrentDirectory { get; set; }
    public bool CanReadCurrentDirectory { get; set; }
    public bool CanWriteToWorkspace { get; set; }
    public int FileCount { get; set; }
    public string? ErrorMessage { get; set; }
}

/// <summary>
/// Configuration file validation result.
/// </summary>
internal sealed class ConfigFileValidation
{
    public bool IsValid { get; set; }
    public bool ConfigExists { get; set; }
    public string? Theme { get; set; }
    public string? Model { get; set; }
    public string? ErrorMessage { get; set; }
}
