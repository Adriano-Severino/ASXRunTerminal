using System.Text.Json;
using ASXRunTerminal.Core;

namespace ASXRunTerminal.Config;

internal static class WorkspacePermissionPolicyFile
{
    internal const string WorkspacePermissionPolicyFileName = "workspace-permissions.json";

    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        PropertyNameCaseInsensitive = true
    };

    public static WorkspaceFilePermissionPolicy Load(string workspaceRootDirectory)
    {
        var resolvedWorkspaceRoot = ResolveWorkspaceRootDirectory(workspaceRootDirectory);
        var policyPath = GetPolicyPath(resolvedWorkspaceRoot);

        if (!File.Exists(policyPath))
        {
            return WorkspaceFilePermissionPolicy.AllowAll;
        }

        var rawContent = File.ReadAllText(policyPath);
        if (string.IsNullOrWhiteSpace(rawContent))
        {
            throw new InvalidOperationException(
                $"Arquivo de politica de workspace invalido em '{policyPath}'. O arquivo nao pode estar vazio.");
        }

        PersistedWorkspacePermissionPolicy? persistedPolicy;
        try
        {
            persistedPolicy = JsonSerializer.Deserialize<PersistedWorkspacePermissionPolicy>(
                rawContent,
                JsonOptions);
        }
        catch (JsonException ex)
        {
            throw new InvalidOperationException(
                $"Arquivo de politica de workspace invalido em '{policyPath}'.",
                ex);
        }

        if (persistedPolicy is null)
        {
            throw new InvalidOperationException(
                $"Arquivo de politica de workspace invalido em '{policyPath}'.");
        }

        try
        {
            return persistedPolicy.ToPolicy();
        }
        catch (InvalidOperationException ex)
        {
            throw new InvalidOperationException(
                $"Arquivo de politica de workspace invalido em '{policyPath}'. {ex.Message}",
                ex);
        }
    }

    internal static string GetPolicyPath(string workspaceRootDirectory)
    {
        var resolvedWorkspaceRoot = ResolveWorkspaceRootDirectory(workspaceRootDirectory);
        return Path.Combine(
            resolvedWorkspaceRoot,
            UserConfigFile.ConfigDirectoryName,
            WorkspacePermissionPolicyFileName);
    }

    private static string ResolveWorkspaceRootDirectory(string workspaceRootDirectory)
    {
        if (string.IsNullOrWhiteSpace(workspaceRootDirectory))
        {
            throw new InvalidOperationException(
                "Nao foi possivel resolver a raiz do workspace para carregar politicas de permissao.");
        }

        var resolvedWorkspaceRoot = Path.GetFullPath(workspaceRootDirectory.Trim());
        if (!Directory.Exists(resolvedWorkspaceRoot))
        {
            throw new DirectoryNotFoundException(
                $"Nao foi possivel carregar politicas de permissao. O diretorio '{resolvedWorkspaceRoot}' nao existe.");
        }

        return resolvedWorkspaceRoot;
    }

    private static WorkspacePermissionDefaultMode ParseDefaultMode(string? rawMode)
    {
        if (string.IsNullOrWhiteSpace(rawMode))
        {
            return WorkspacePermissionDefaultMode.Allow;
        }

        var normalizedMode = rawMode.Trim();
        if (string.Equals(normalizedMode, "allow", StringComparison.OrdinalIgnoreCase))
        {
            return WorkspacePermissionDefaultMode.Allow;
        }

        if (string.Equals(normalizedMode, "deny", StringComparison.OrdinalIgnoreCase))
        {
            return WorkspacePermissionDefaultMode.Deny;
        }

        throw new InvalidOperationException(
            "O campo 'defaultMode' deve ser 'allow' ou 'deny'.");
    }

    private static IReadOnlyList<string> NormalizePatterns(
        IReadOnlyList<string>? patterns,
        WorkspaceFilePermissionOperation operation,
        string ruleType)
    {
        if (patterns is null || patterns.Count == 0)
        {
            return [];
        }

        var normalizedPatterns = new List<string>(patterns.Count);
        foreach (var pattern in patterns)
        {
            if (string.IsNullOrWhiteSpace(pattern))
            {
                throw new InvalidOperationException(
                    $"A lista '{ruleType}' da operacao '{WorkspaceFilePermissionPolicy.ToOperationKey(operation)}' nao pode conter padroes vazios.");
            }

            normalizedPatterns.Add(pattern.Trim());
        }

        return normalizedPatterns;
    }

    private sealed record PersistedWorkspacePermissionPolicy(
        string? DefaultMode,
        PersistedWorkspacePermissionRule? Read,
        PersistedWorkspacePermissionRule? Create,
        PersistedWorkspacePermissionRule? Edit,
        PersistedWorkspacePermissionRule? Copy,
        PersistedWorkspacePermissionRule? Move,
        PersistedWorkspacePermissionRule? Delete)
    {
        public WorkspaceFilePermissionPolicy ToPolicy()
        {
            var defaultMode = ParseDefaultMode(DefaultMode);
            var rules = new Dictionary<WorkspaceFilePermissionOperation, WorkspaceFilePermissionRule>();

            AddOperationRule(rules, WorkspaceFilePermissionOperation.Read, Read);
            AddOperationRule(rules, WorkspaceFilePermissionOperation.Create, Create);
            AddOperationRule(rules, WorkspaceFilePermissionOperation.Edit, Edit);
            AddOperationRule(rules, WorkspaceFilePermissionOperation.Copy, Copy);
            AddOperationRule(rules, WorkspaceFilePermissionOperation.Move, Move);
            AddOperationRule(rules, WorkspaceFilePermissionOperation.Delete, Delete);

            if (rules.Count == 0 && defaultMode == WorkspacePermissionDefaultMode.Allow)
            {
                return WorkspaceFilePermissionPolicy.AllowAll;
            }

            return new WorkspaceFilePermissionPolicy(defaultMode, rules);
        }

        private static void AddOperationRule(
            IDictionary<WorkspaceFilePermissionOperation, WorkspaceFilePermissionRule> rules,
            WorkspaceFilePermissionOperation operation,
            PersistedWorkspacePermissionRule? persistedRule)
        {
            if (persistedRule is null)
            {
                return;
            }

            var allowPatterns = NormalizePatterns(
                persistedRule.Allow,
                operation,
                "allow");
            var denyPatterns = NormalizePatterns(
                persistedRule.Deny,
                operation,
                "deny");

            rules[operation] = new WorkspaceFilePermissionRule(
                AllowPatterns: allowPatterns,
                DenyPatterns: denyPatterns);
        }
    }

    private sealed record PersistedWorkspacePermissionRule(
        IReadOnlyList<string>? Allow,
        IReadOnlyList<string>? Deny);
}
