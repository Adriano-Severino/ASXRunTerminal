using System.Text.Json;
using ASXRunTerminal.Core;

namespace ASXRunTerminal.Config;

internal static class ShellCommandPermissionPolicyFile
{
    internal const string ShellCommandPermissionPolicyFileName = "shell-command-policy.json";

    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        PropertyNameCaseInsensitive = true
    };

    public static ShellCommandPermissionPolicy Load(string workspaceRootDirectory)
    {
        var resolvedWorkspaceRoot = ResolveWorkspaceRootDirectory(workspaceRootDirectory);
        var policyPath = GetPolicyPath(resolvedWorkspaceRoot);
        if (!File.Exists(policyPath))
        {
            return ShellCommandPermissionPolicy.Default;
        }

        var rawContent = File.ReadAllText(policyPath);
        if (string.IsNullOrWhiteSpace(rawContent))
        {
            throw new InvalidOperationException(
                $"Arquivo de politica de comandos de shell invalido em '{policyPath}'. O arquivo nao pode estar vazio.");
        }

        PersistedShellCommandPermissionPolicy? persistedPolicy;
        try
        {
            persistedPolicy = JsonSerializer.Deserialize<PersistedShellCommandPermissionPolicy>(
                rawContent,
                JsonOptions);
        }
        catch (JsonException ex)
        {
            throw new InvalidOperationException(
                $"Arquivo de politica de comandos de shell invalido em '{policyPath}'.",
                ex);
        }

        if (persistedPolicy is null)
        {
            throw new InvalidOperationException(
                $"Arquivo de politica de comandos de shell invalido em '{policyPath}'.");
        }

        try
        {
            return persistedPolicy.ToPolicy();
        }
        catch (InvalidOperationException ex)
        {
            throw new InvalidOperationException(
                $"Arquivo de politica de comandos de shell invalido em '{policyPath}'. {ex.Message}",
                ex);
        }
    }

    internal static string GetPolicyPath(string workspaceRootDirectory)
    {
        var resolvedWorkspaceRoot = ResolveWorkspaceRootDirectory(workspaceRootDirectory);
        return Path.Combine(
            resolvedWorkspaceRoot,
            UserConfigFile.ConfigDirectoryName,
            ShellCommandPermissionPolicyFileName);
    }

    private static string ResolveWorkspaceRootDirectory(string workspaceRootDirectory)
    {
        if (string.IsNullOrWhiteSpace(workspaceRootDirectory))
        {
            throw new InvalidOperationException(
                "Nao foi possivel resolver a raiz do workspace para carregar politicas de comandos de shell.");
        }

        var resolvedWorkspaceRoot = Path.GetFullPath(workspaceRootDirectory.Trim());
        if (!Directory.Exists(resolvedWorkspaceRoot))
        {
            throw new DirectoryNotFoundException(
                $"Nao foi possivel carregar politicas de comandos de shell. O diretorio '{resolvedWorkspaceRoot}' nao existe.");
        }

        return resolvedWorkspaceRoot;
    }

    private static IReadOnlyList<string> NormalizeCommands(
        IReadOnlyList<string>? commands,
        string ruleType)
    {
        if (commands is null || commands.Count == 0)
        {
            return [];
        }

        var normalizedCommands = new List<string>(commands.Count);
        foreach (var command in commands)
        {
            if (string.IsNullOrWhiteSpace(command))
            {
                throw new InvalidOperationException(
                    $"A lista '{ruleType}' nao pode conter comandos vazios.");
            }

            normalizedCommands.Add(command.Trim());
        }

        return normalizedCommands;
    }

    private sealed record PersistedShellCommandPermissionPolicy(
        IReadOnlyList<string>? Allow,
        IReadOnlyList<string>? Deny)
    {
        public ShellCommandPermissionPolicy ToPolicy()
        {
            var allowCommands = NormalizeCommands(Allow, "allow");
            var denyCommands = NormalizeCommands(Deny, "deny");

            if (allowCommands.Count == 0 && denyCommands.Count == 0)
            {
                return ShellCommandPermissionPolicy.Default;
            }

            return new ShellCommandPermissionPolicy(
                allowedCommands: allowCommands,
                blockedCommands: denyCommands);
        }
    }
}
