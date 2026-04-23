using System.Text;
using System.Text.RegularExpressions;

namespace ASXRunTerminal.Core;

internal enum WorkspacePermissionDefaultMode
{
    Allow = 1,
    Deny = 2
}

internal enum WorkspaceFilePermissionOperation
{
    Read = 1,
    Create = 2,
    Edit = 3,
    Copy = 4,
    Move = 5,
    Delete = 6
}

internal readonly record struct WorkspaceFilePermissionRule(
    IReadOnlyList<string> AllowPatterns,
    IReadOnlyList<string> DenyPatterns);

internal sealed class WorkspaceFilePermissionPolicy
{
    private static readonly StringComparison PathComparison = GetPathComparison();
    private static readonly RegexOptions RegexOptions = GetRegexOptions();

    private readonly WorkspacePermissionDefaultMode _defaultMode;
    private readonly IReadOnlyDictionary<WorkspaceFilePermissionOperation, CompiledPermissionRule> _rules;

    public static WorkspaceFilePermissionPolicy AllowAll { get; } = new(
        WorkspacePermissionDefaultMode.Allow);

    public WorkspaceFilePermissionPolicy(
        WorkspacePermissionDefaultMode defaultMode = WorkspacePermissionDefaultMode.Allow,
        IReadOnlyDictionary<WorkspaceFilePermissionOperation, WorkspaceFilePermissionRule>? rules = null)
    {
        _defaultMode = defaultMode;
        _rules = CompileRules(rules);
    }

    public void EnsureAllowed(
        WorkspaceFilePermissionOperation operation,
        string workspaceRootDirectory,
        string resolvedPath)
    {
        var normalizedWorkspaceRoot = ResolveWorkspaceRootDirectory(workspaceRootDirectory);
        var normalizedResolvedPath = ResolvePath(resolvedPath, nameof(resolvedPath));
        var relativePath = ResolveRelativePath(normalizedWorkspaceRoot, normalizedResolvedPath);

        if (IsAllowed(operation, relativePath))
        {
            return;
        }

        throw new UnauthorizedAccessException(
            $"A operacao '{ToOperationKey(operation)}' nao e permitida para o caminho '{relativePath}' no workspace '{normalizedWorkspaceRoot}'.");
    }

    internal static bool TryParseOperation(
        string? rawValue,
        out WorkspaceFilePermissionOperation operation)
    {
        if (string.Equals(rawValue, "read", StringComparison.OrdinalIgnoreCase))
        {
            operation = WorkspaceFilePermissionOperation.Read;
            return true;
        }

        if (string.Equals(rawValue, "create", StringComparison.OrdinalIgnoreCase))
        {
            operation = WorkspaceFilePermissionOperation.Create;
            return true;
        }

        if (string.Equals(rawValue, "edit", StringComparison.OrdinalIgnoreCase))
        {
            operation = WorkspaceFilePermissionOperation.Edit;
            return true;
        }

        if (string.Equals(rawValue, "copy", StringComparison.OrdinalIgnoreCase))
        {
            operation = WorkspaceFilePermissionOperation.Copy;
            return true;
        }

        if (string.Equals(rawValue, "move", StringComparison.OrdinalIgnoreCase))
        {
            operation = WorkspaceFilePermissionOperation.Move;
            return true;
        }

        if (string.Equals(rawValue, "delete", StringComparison.OrdinalIgnoreCase))
        {
            operation = WorkspaceFilePermissionOperation.Delete;
            return true;
        }

        operation = default;
        return false;
    }

    internal static string ToOperationKey(WorkspaceFilePermissionOperation operation)
    {
        return operation switch
        {
            WorkspaceFilePermissionOperation.Read => "read",
            WorkspaceFilePermissionOperation.Create => "create",
            WorkspaceFilePermissionOperation.Edit => "edit",
            WorkspaceFilePermissionOperation.Copy => "copy",
            WorkspaceFilePermissionOperation.Move => "move",
            WorkspaceFilePermissionOperation.Delete => "delete",
            _ => throw new InvalidOperationException(
                $"A operacao de arquivo '{operation}' nao e suportada.")
        };
    }

    private bool IsAllowed(
        WorkspaceFilePermissionOperation operation,
        string relativePath)
    {
        if (!_rules.TryGetValue(operation, out var rule))
        {
            return _defaultMode == WorkspacePermissionDefaultMode.Allow;
        }

        if (rule.DenyPatterns.Any(matcher => matcher.IsMatch(relativePath)))
        {
            return false;
        }

        if (rule.AllowPatterns.Count == 0)
        {
            return _defaultMode == WorkspacePermissionDefaultMode.Allow;
        }

        return rule.AllowPatterns.Any(matcher => matcher.IsMatch(relativePath));
    }

    private static IReadOnlyDictionary<WorkspaceFilePermissionOperation, CompiledPermissionRule> CompileRules(
        IReadOnlyDictionary<WorkspaceFilePermissionOperation, WorkspaceFilePermissionRule>? rules)
    {
        if (rules is null || rules.Count == 0)
        {
            return new Dictionary<WorkspaceFilePermissionOperation, CompiledPermissionRule>();
        }

        var compiledRules = new Dictionary<WorkspaceFilePermissionOperation, CompiledPermissionRule>(rules.Count);
        foreach (var pair in rules)
        {
            var operation = pair.Key;
            var rule = pair.Value;
            var allowPatterns = CompilePatterns(rule.AllowPatterns, operation, "allow");
            var denyPatterns = CompilePatterns(rule.DenyPatterns, operation, "deny");

            compiledRules[operation] = new CompiledPermissionRule(
                AllowPatterns: allowPatterns,
                DenyPatterns: denyPatterns);
        }

        return compiledRules;
    }

    private static IReadOnlyList<PathPatternMatcher> CompilePatterns(
        IReadOnlyList<string>? patterns,
        WorkspaceFilePermissionOperation operation,
        string ruleType)
    {
        if (patterns is null || patterns.Count == 0)
        {
            return [];
        }

        var compiledPatterns = new List<PathPatternMatcher>(patterns.Count);
        foreach (var pattern in patterns)
        {
            if (string.IsNullOrWhiteSpace(pattern))
            {
                throw new InvalidOperationException(
                    $"A lista '{ruleType}' da operacao '{ToOperationKey(operation)}' nao pode conter padroes vazios.");
            }

            var normalizedPattern = NormalizePattern(pattern);
            var regexPattern = BuildRegexPattern(normalizedPattern);
            var regex = new Regex(regexPattern, RegexOptions);
            compiledPatterns.Add(new PathPatternMatcher(normalizedPattern, regex));
        }

        return compiledPatterns;
    }

    private static string ResolveWorkspaceRootDirectory(string workspaceRootDirectory)
    {
        if (string.IsNullOrWhiteSpace(workspaceRootDirectory))
        {
            throw new InvalidOperationException(
                "Nao foi possivel resolver a raiz do workspace para validar permissoes.");
        }

        return Path.GetFullPath(workspaceRootDirectory.Trim());
    }

    private static string ResolvePath(string path, string argumentName)
    {
        if (string.IsNullOrWhiteSpace(path))
        {
            throw new ArgumentException(
                $"O argumento '{argumentName}' e obrigatorio.",
                argumentName);
        }

        return Path.GetFullPath(path.Trim());
    }

    private static string ResolveRelativePath(string workspaceRootDirectory, string resolvedPath)
    {
        if (string.Equals(workspaceRootDirectory, resolvedPath, PathComparison))
        {
            return ".";
        }

        var workspaceRootWithSeparator = EnsureTrailingDirectorySeparator(workspaceRootDirectory);
        if (resolvedPath.StartsWith(workspaceRootWithSeparator, PathComparison))
        {
            return NormalizeRelativePath(resolvedPath[workspaceRootWithSeparator.Length..]);
        }

        return NormalizeRelativePath(Path.GetRelativePath(workspaceRootDirectory, resolvedPath));
    }

    private static string NormalizePattern(string pattern)
    {
        var normalizedPattern = NormalizeRelativePath(pattern);
        if (string.Equals(normalizedPattern, ".", StringComparison.Ordinal))
        {
            throw new InvalidOperationException(
                "Os padroes de permissao devem apontar para arquivos ou diretorios dentro do workspace.");
        }

        return normalizedPattern;
    }

    private static string NormalizeRelativePath(string path)
    {
        var normalizedPath = path.Trim()
            .Replace(Path.DirectorySeparatorChar, '/')
            .Replace(Path.AltDirectorySeparatorChar, '/');

        while (normalizedPath.StartsWith("./", StringComparison.Ordinal))
        {
            normalizedPath = normalizedPath[2..];
        }

        while (normalizedPath.Contains("//", StringComparison.Ordinal))
        {
            normalizedPath = normalizedPath.Replace("//", "/", StringComparison.Ordinal);
        }

        normalizedPath = normalizedPath.Trim('/');
        return normalizedPath.Length == 0
            ? "."
            : normalizedPath;
    }

    private static string BuildRegexPattern(string normalizedPattern)
    {
        if (string.Equals(normalizedPattern, "**", StringComparison.Ordinal))
        {
            return "^.*$";
        }

        if (normalizedPattern.EndsWith("/**", StringComparison.Ordinal))
        {
            var prefix = normalizedPattern[..^3];
            if (prefix.Length == 0)
            {
                return "^.*$";
            }

            return $"^{ConvertGlobToRegexFragment(prefix)}(?:/.*)?$";
        }

        return $"^{ConvertGlobToRegexFragment(normalizedPattern)}$";
    }

    private static string ConvertGlobToRegexFragment(string pattern)
    {
        var builder = new StringBuilder(pattern.Length * 2);

        for (var index = 0; index < pattern.Length; index++)
        {
            var current = pattern[index];
            if (current == '*')
            {
                var isDoubleWildcard = index + 1 < pattern.Length && pattern[index + 1] == '*';
                if (isDoubleWildcard)
                {
                    builder.Append(".*");
                    index++;
                    continue;
                }

                builder.Append("[^/]*");
                continue;
            }

            if (current == '?')
            {
                builder.Append("[^/]");
                continue;
            }

            builder.Append(Regex.Escape(current.ToString()));
        }

        return builder.ToString();
    }

    private static string EnsureTrailingDirectorySeparator(string path)
    {
        if (Path.EndsInDirectorySeparator(path))
        {
            return path;
        }

        return $"{path}{Path.DirectorySeparatorChar}";
    }

    private static StringComparison GetPathComparison()
    {
        return OperatingSystem.IsWindows()
            ? StringComparison.OrdinalIgnoreCase
            : StringComparison.Ordinal;
    }

    private static RegexOptions GetRegexOptions()
    {
        var options = RegexOptions.CultureInvariant;
        if (OperatingSystem.IsWindows())
        {
            options |= RegexOptions.IgnoreCase;
        }

        return options;
    }

    private readonly record struct CompiledPermissionRule(
        IReadOnlyList<PathPatternMatcher> AllowPatterns,
        IReadOnlyList<PathPatternMatcher> DenyPatterns);

    private readonly record struct PathPatternMatcher(
        string Pattern,
        Regex Regex)
    {
        public bool IsMatch(string path)
        {
            return Regex.IsMatch(path);
        }
    }
}
