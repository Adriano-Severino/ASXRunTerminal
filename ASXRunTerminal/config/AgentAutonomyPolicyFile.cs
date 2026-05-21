using System.Text.Json;
using System.Text.Json.Serialization;
using ASXRunTerminal.Core;

namespace ASXRunTerminal.Config;

internal static class AgentAutonomyPolicyFile
{
    internal const string AgentAutonomyPolicyFileName = "agent-governance.json";

    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        PropertyNameCaseInsensitive = true
    };

    public static AgentAutonomyPolicy Load(string workspaceRootDirectory)
    {
        var resolvedWorkspaceRoot = ResolveWorkspaceRootDirectory(workspaceRootDirectory);
        var policyPath = GetPolicyPath(resolvedWorkspaceRoot);
        if (!File.Exists(policyPath))
        {
            return AgentAutonomyPolicy.Default;
        }

        var rawContent = File.ReadAllText(policyPath);
        if (string.IsNullOrWhiteSpace(rawContent))
        {
            throw new InvalidOperationException(
                $"Arquivo de governanca do agente invalido em '{policyPath}'. O arquivo nao pode estar vazio.");
        }

        PersistedAgentAutonomyPolicy? persistedPolicy;
        try
        {
            persistedPolicy = JsonSerializer.Deserialize<PersistedAgentAutonomyPolicy>(
                rawContent,
                JsonOptions);
        }
        catch (JsonException ex)
        {
            throw new InvalidOperationException(
                $"Arquivo de governanca do agente invalido em '{policyPath}'.",
                ex);
        }

        if (persistedPolicy is null)
        {
            throw new InvalidOperationException(
                $"Arquivo de governanca do agente invalido em '{policyPath}'.");
        }

        try
        {
            AgentAutonomyPolicy policy = persistedPolicy;
            return policy;
        }
        catch (InvalidOperationException ex)
        {
            throw new InvalidOperationException(
                $"Arquivo de governanca do agente invalido em '{policyPath}'. {ex.Message}",
                ex);
        }
    }

    internal static string GetPolicyPath(string workspaceRootDirectory)
    {
        var resolvedWorkspaceRoot = ResolveWorkspaceRootDirectory(workspaceRootDirectory);
        return Path.Combine(
            resolvedWorkspaceRoot,
            UserConfigFile.ConfigDirectoryName,
            AgentAutonomyPolicyFileName);
    }

    private static string ResolveWorkspaceRootDirectory(string workspaceRootDirectory)
    {
        if (string.IsNullOrWhiteSpace(workspaceRootDirectory))
        {
            throw new InvalidOperationException(
                "Nao foi possivel resolver a raiz do workspace para carregar a governanca do agente.");
        }

        var resolvedWorkspaceRoot = Path.GetFullPath(workspaceRootDirectory.Trim());
        if (!Directory.Exists(resolvedWorkspaceRoot))
        {
            throw new DirectoryNotFoundException(
                $"Nao foi possivel carregar a governanca do agente. O diretorio '{resolvedWorkspaceRoot}' nao existe.");
        }

        return resolvedWorkspaceRoot;
    }

    private sealed record PersistedAgentAutonomyPolicy(
        string? AutonomyLevel,
        [property: JsonPropertyName("autonomy_level")] string? AutonomyLevelSnakeCase,
        string? Level)
    {
        public static implicit operator AgentAutonomyPolicy(PersistedAgentAutonomyPolicy persistedPolicy)
        {
            var rawLevel = persistedPolicy.AutonomyLevel
                ?? persistedPolicy.AutonomyLevelSnakeCase
                ?? persistedPolicy.Level;
            if (string.IsNullOrWhiteSpace(rawLevel))
            {
                return AgentAutonomyPolicy.Default;
            }

            AgentAutonomyLevelName levelName = new(rawLevel.Trim());
            AgentAutonomyLevel level = levelName;
            return new AgentAutonomyPolicy(level);
        }
    }
}
