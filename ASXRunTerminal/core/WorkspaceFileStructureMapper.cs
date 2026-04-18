using System.Text;
using System.Text.RegularExpressions;

namespace ASXRunTerminal.Core;

internal enum WorkspaceEntryKind
{
    Directory = 0,
    File = 1
}

internal readonly record struct WorkspaceEntry(
    string RelativePath,
    WorkspaceEntryKind Kind,
    long SizeInBytes);

internal enum WorkspaceMapLimitKind
{
    None = 0,
    MaxEntries = 1,
    MaxDepth = 2
}

internal readonly record struct WorkspaceStructureMap(
    string RootDirectoryPath,
    IReadOnlyList<WorkspaceEntry> Entries,
    int VisitedDirectoryCount,
    int VisitedFileCount,
    int IgnoredEntryCount,
    WorkspaceMapLimitKind LimitKind)
{
    public bool IsTruncated => LimitKind != WorkspaceMapLimitKind.None;
}

internal readonly record struct WorkspaceStructureMapOptions(
    int MaxEntries = 5_000,
    int MaxDepth = 12,
    int MaxGitIgnoreFileSizeInBytes = 262_144,
    int MaxGitIgnoreRulesPerFile = 2_048);

internal static class WorkspaceFileStructureMapper
{
    private const string GitDirectoryName = ".git";
    private const string GitIgnoreFileName = ".gitignore";
    private static readonly StringComparer PathComparer = GetPathComparer();
    private static readonly StringComparison PathComparison = GetPathComparison();
    private static readonly RegexOptions GitIgnoreRegexOptions =
        OperatingSystem.IsWindows()
            ? RegexOptions.Compiled | RegexOptions.CultureInvariant | RegexOptions.IgnoreCase
            : RegexOptions.Compiled | RegexOptions.CultureInvariant;

    public static WorkspaceStructureMap Map(
        string workspaceRootDirectory,
        WorkspaceStructureMapOptions? options = null)
    {
        var resolvedOptions = options ?? GetDefaultOptions();
        ValidateOptions(resolvedOptions);

        var resolvedRootDirectory = ResolveRootDirectory(workspaceRootDirectory);
        var discoveredEntries = new List<WorkspaceEntry>();
        var pendingDirectories = new Stack<DirectoryTraversalState>();
        pendingDirectories.Push(new DirectoryTraversalState(
            AbsolutePath: resolvedRootDirectory,
            RelativePath: string.Empty,
            Depth: 0,
            Rules: []));

        var visitedDirectoryCount = 0;
        var visitedFileCount = 0;
        var ignoredEntryCount = 0;
        var limitKind = WorkspaceMapLimitKind.None;

        while (pendingDirectories.Count > 0)
        {
            var currentDirectory = pendingDirectories.Pop();
            visitedDirectoryCount++;

            if (currentDirectory.Depth > 0)
            {
                if (!TryAddEntry(
                        discoveredEntries,
                        new WorkspaceEntry(
                            RelativePath: currentDirectory.RelativePath,
                            Kind: WorkspaceEntryKind.Directory,
                            SizeInBytes: 0),
                        resolvedOptions.MaxEntries))
                {
                    limitKind = WorkspaceMapLimitKind.MaxEntries;
                    break;
                }
            }

            var currentRules = currentDirectory.Rules;
            var directoryRules = LoadDirectoryGitIgnoreRules(
                currentDirectory.AbsolutePath,
                currentDirectory.RelativePath,
                resolvedOptions);
            if (directoryRules.Count > 0)
            {
                currentRules = MergeRules(currentRules, directoryRules);
            }

            var childEntries = EnumerateChildEntries(currentDirectory.AbsolutePath);
            if (childEntries.Count == 0)
            {
                continue;
            }

            childEntries.Sort((left, right) => PathComparer.Compare(left.Name, right.Name));

            for (var index = childEntries.Count - 1; index >= 0; index--)
            {
                var childEntry = childEntries[index];
                var relativePath = BuildChildRelativePath(currentDirectory.RelativePath, childEntry.Name);

                if (childEntry.IsDirectory
                    && string.Equals(childEntry.Name, GitDirectoryName, StringComparison.OrdinalIgnoreCase))
                {
                    ignoredEntryCount++;
                    continue;
                }

                if (childEntry.IsReparsePoint)
                {
                    ignoredEntryCount++;
                    continue;
                }

                if (IsIgnoredByGitIgnore(relativePath, childEntry.IsDirectory, currentRules))
                {
                    ignoredEntryCount++;
                    continue;
                }

                if (childEntry.IsDirectory)
                {
                    var childDepth = currentDirectory.Depth + 1;
                    if (childDepth > resolvedOptions.MaxDepth)
                    {
                        ignoredEntryCount++;
                        if (limitKind == WorkspaceMapLimitKind.None)
                        {
                            limitKind = WorkspaceMapLimitKind.MaxDepth;
                        }

                        continue;
                    }

                    pendingDirectories.Push(new DirectoryTraversalState(
                        AbsolutePath: childEntry.AbsolutePath,
                        RelativePath: relativePath,
                        Depth: childDepth,
                        Rules: currentRules));
                    continue;
                }

                visitedFileCount++;
                if (!TryAddEntry(
                        discoveredEntries,
                        new WorkspaceEntry(
                            RelativePath: relativePath,
                            Kind: WorkspaceEntryKind.File,
                            SizeInBytes: childEntry.FileSizeInBytes),
                        resolvedOptions.MaxEntries))
                {
                    limitKind = WorkspaceMapLimitKind.MaxEntries;
                    pendingDirectories.Clear();
                    break;
                }
            }
        }

        discoveredEntries.Sort((left, right) => PathComparer.Compare(left.RelativePath, right.RelativePath));

        return new WorkspaceStructureMap(
            RootDirectoryPath: resolvedRootDirectory,
            Entries: discoveredEntries,
            VisitedDirectoryCount: visitedDirectoryCount,
            VisitedFileCount: visitedFileCount,
            IgnoredEntryCount: ignoredEntryCount,
            LimitKind: limitKind);
    }

    private static WorkspaceStructureMapOptions GetDefaultOptions()
    {
        return new WorkspaceStructureMapOptions(
            MaxEntries: 5_000,
            MaxDepth: 12,
            MaxGitIgnoreFileSizeInBytes: 262_144,
            MaxGitIgnoreRulesPerFile: 2_048);
    }

    private static void ValidateOptions(WorkspaceStructureMapOptions options)
    {
        if (options.MaxEntries <= 0)
        {
            throw new ArgumentOutOfRangeException(
                nameof(options),
                "O limite MaxEntries deve ser maior que zero.");
        }

        if (options.MaxDepth < 0)
        {
            throw new ArgumentOutOfRangeException(
                nameof(options),
                "O limite MaxDepth nao pode ser negativo.");
        }

        if (options.MaxGitIgnoreFileSizeInBytes <= 0)
        {
            throw new ArgumentOutOfRangeException(
                nameof(options),
                "O limite MaxGitIgnoreFileSizeInBytes deve ser maior que zero.");
        }

        if (options.MaxGitIgnoreRulesPerFile <= 0)
        {
            throw new ArgumentOutOfRangeException(
                nameof(options),
                "O limite MaxGitIgnoreRulesPerFile deve ser maior que zero.");
        }
    }

    private static string ResolveRootDirectory(string workspaceRootDirectory)
    {
        if (string.IsNullOrWhiteSpace(workspaceRootDirectory))
        {
            throw new InvalidOperationException(
                "Nao foi possivel resolver o diretorio raiz para mapear a estrutura de arquivos.");
        }

        var resolvedRootDirectory = Path.GetFullPath(workspaceRootDirectory.Trim());
        if (!Directory.Exists(resolvedRootDirectory))
        {
            throw new DirectoryNotFoundException(
                $"Nao foi possivel mapear a estrutura. O diretorio '{resolvedRootDirectory}' nao existe.");
        }

        return resolvedRootDirectory;
    }

    private static IReadOnlyList<GitIgnoreRule> LoadDirectoryGitIgnoreRules(
        string directoryAbsolutePath,
        string directoryRelativePath,
        WorkspaceStructureMapOptions options)
    {
        var gitIgnorePath = Path.Combine(directoryAbsolutePath, GitIgnoreFileName);
        if (!File.Exists(gitIgnorePath))
        {
            return [];
        }

        try
        {
            var gitIgnoreInfo = new FileInfo(gitIgnorePath);
            if (gitIgnoreInfo.Length > options.MaxGitIgnoreFileSizeInBytes)
            {
                return [];
            }

            var lines = File.ReadAllLines(gitIgnorePath);
            if (lines.Length == 0)
            {
                return [];
            }

            var rules = new List<GitIgnoreRule>(
                Math.Min(lines.Length, options.MaxGitIgnoreRulesPerFile));

            foreach (var rawLine in lines)
            {
                if (rules.Count >= options.MaxGitIgnoreRulesPerFile)
                {
                    break;
                }

                if (TryParseGitIgnoreRule(
                        directoryRelativePath,
                        rawLine,
                        out var rule))
                {
                    rules.Add(rule);
                }
            }

            return rules;
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException)
        {
            return [];
        }
    }

    private static bool TryParseGitIgnoreRule(
        string directoryRelativePath,
        string rawLine,
        out GitIgnoreRule rule)
    {
        var trimmedLine = rawLine.Trim();
        if (trimmedLine.Length == 0)
        {
            rule = default;
            return false;
        }

        if (trimmedLine.StartsWith("#", StringComparison.Ordinal)
            && !trimmedLine.StartsWith("\\#", StringComparison.Ordinal))
        {
            rule = default;
            return false;
        }

        if (trimmedLine.StartsWith("\\#", StringComparison.Ordinal)
            || trimmedLine.StartsWith("\\!", StringComparison.Ordinal))
        {
            trimmedLine = trimmedLine[1..];
        }

        var isNegated = false;
        if (trimmedLine.StartsWith('!'))
        {
            isNegated = true;
            trimmedLine = trimmedLine[1..];
        }

        trimmedLine = trimmedLine.Trim();
        if (trimmedLine.Length == 0)
        {
            rule = default;
            return false;
        }

        var isDirectoryOnly = trimmedLine.EndsWith('/');
        if (isDirectoryOnly)
        {
            trimmedLine = trimmedLine.TrimEnd('/');
        }

        if (trimmedLine.Length == 0)
        {
            rule = default;
            return false;
        }

        var isAnchoredToDirectory = trimmedLine.StartsWith('/');
        if (isAnchoredToDirectory)
        {
            trimmedLine = trimmedLine.TrimStart('/');
        }

        var normalizedPattern = NormalizeRelativePath(trimmedLine);
        if (normalizedPattern.Length == 0)
        {
            rule = default;
            return false;
        }

        var matchFileNameOnly = !normalizedPattern.Contains('/');
        var patternRegex = BuildGitIgnoreRuleRegex(
            normalizedPattern,
            isAnchoredToDirectory,
            matchFileNameOnly);

        rule = new GitIgnoreRule(
            BaseRelativePath: NormalizeRelativePath(directoryRelativePath),
            PatternRegex: patternRegex,
            IsNegated: isNegated,
            IsDirectoryOnly: isDirectoryOnly,
            MatchFileNameOnly: matchFileNameOnly);
        return true;
    }

    private static Regex BuildGitIgnoreRuleRegex(
        string pattern,
        bool isAnchoredToDirectory,
        bool matchFileNameOnly)
    {
        var globRegexPattern = ConvertGlobPatternToRegex(pattern);
        var fullRegexPattern = isAnchoredToDirectory || matchFileNameOnly
            ? $"^{globRegexPattern}$"
            : $"(?:^|.*/){globRegexPattern}$";

        return new Regex(
            fullRegexPattern,
            GitIgnoreRegexOptions,
            TimeSpan.FromMilliseconds(100));
    }

    private static string ConvertGlobPatternToRegex(string pattern)
    {
        var regexBuilder = new StringBuilder(pattern.Length * 2);
        for (var index = 0; index < pattern.Length; index++)
        {
            var current = pattern[index];
            if (current == '*')
            {
                var hasDoubleAsterisk = index + 1 < pattern.Length && pattern[index + 1] == '*';
                if (hasDoubleAsterisk)
                {
                    regexBuilder.Append(".*");
                    index++;
                    continue;
                }

                regexBuilder.Append("[^/]*");
                continue;
            }

            if (current == '?')
            {
                regexBuilder.Append("[^/]");
                continue;
            }

            if (current == '/')
            {
                regexBuilder.Append('/');
                continue;
            }

            regexBuilder.Append(Regex.Escape(current.ToString()));
        }

        return regexBuilder.ToString();
    }

    private static bool IsIgnoredByGitIgnore(
        string relativePath,
        bool isDirectory,
        IReadOnlyList<GitIgnoreRule> rules)
    {
        if (rules.Count == 0)
        {
            return false;
        }

        var normalizedRelativePath = NormalizeRelativePath(relativePath);
        var ignored = false;

        foreach (var rule in rules)
        {
            if (rule.IsDirectoryOnly && !isDirectory)
            {
                continue;
            }

            if (!TryGetRelativePathWithinBase(
                    normalizedRelativePath,
                    rule.BaseRelativePath,
                    out var ruleRelativePath))
            {
                continue;
            }

            var candidatePath = rule.MatchFileNameOnly
                ? GetLeafPathSegment(ruleRelativePath)
                : ruleRelativePath;

            if (candidatePath.Length == 0)
            {
                continue;
            }

            if (!rule.PatternRegex.IsMatch(candidatePath))
            {
                continue;
            }

            ignored = !rule.IsNegated;
        }

        return ignored;
    }

    private static bool TryGetRelativePathWithinBase(
        string normalizedRelativePath,
        string baseRelativePath,
        out string relativePathWithinBase)
    {
        if (baseRelativePath.Length == 0)
        {
            relativePathWithinBase = normalizedRelativePath;
            return true;
        }

        if (!normalizedRelativePath.StartsWith(baseRelativePath, PathComparison))
        {
            relativePathWithinBase = string.Empty;
            return false;
        }

        if (normalizedRelativePath.Length == baseRelativePath.Length)
        {
            relativePathWithinBase = string.Empty;
            return true;
        }

        if (normalizedRelativePath[baseRelativePath.Length] != '/')
        {
            relativePathWithinBase = string.Empty;
            return false;
        }

        relativePathWithinBase = normalizedRelativePath[(baseRelativePath.Length + 1)..];
        return true;
    }

    private static string GetLeafPathSegment(string relativePath)
    {
        var separatorIndex = relativePath.LastIndexOf('/');
        if (separatorIndex < 0)
        {
            return relativePath;
        }

        if (separatorIndex + 1 >= relativePath.Length)
        {
            return string.Empty;
        }

        return relativePath[(separatorIndex + 1)..];
    }

    private static List<FileSystemEntryCandidate> EnumerateChildEntries(string absoluteDirectoryPath)
    {
        try
        {
            var discoveredEntries = new List<FileSystemEntryCandidate>();

            foreach (var absolutePath in Directory.EnumerateFileSystemEntries(absoluteDirectoryPath))
            {
                var name = Path.GetFileName(absolutePath);
                if (string.IsNullOrWhiteSpace(name))
                {
                    continue;
                }

                FileAttributes attributes;
                try
                {
                    attributes = File.GetAttributes(absolutePath);
                }
                catch (Exception ex) when (ex is IOException or UnauthorizedAccessException)
                {
                    continue;
                }

                var isDirectory = (attributes & FileAttributes.Directory) != 0;
                var isReparsePoint = (attributes & FileAttributes.ReparsePoint) != 0;
                var fileSizeInBytes = 0L;

                if (!isDirectory)
                {
                    try
                    {
                        fileSizeInBytes = new FileInfo(absolutePath).Length;
                    }
                    catch (Exception ex) when (ex is IOException or UnauthorizedAccessException)
                    {
                        fileSizeInBytes = 0;
                    }
                }

                discoveredEntries.Add(new FileSystemEntryCandidate(
                    AbsolutePath: absolutePath,
                    Name: name,
                    IsDirectory: isDirectory,
                    IsReparsePoint: isReparsePoint,
                    FileSizeInBytes: fileSizeInBytes));
            }

            return discoveredEntries;
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException)
        {
            return [];
        }
    }

    private static IReadOnlyList<GitIgnoreRule> MergeRules(
        IReadOnlyList<GitIgnoreRule> existingRules,
        IReadOnlyList<GitIgnoreRule> newRules)
    {
        var mergedRules = new List<GitIgnoreRule>(existingRules.Count + newRules.Count);
        mergedRules.AddRange(existingRules);
        mergedRules.AddRange(newRules);
        return mergedRules;
    }

    private static bool TryAddEntry(
        List<WorkspaceEntry> entries,
        WorkspaceEntry entry,
        int maxEntries)
    {
        if (entries.Count >= maxEntries)
        {
            return false;
        }

        entries.Add(entry);
        return true;
    }

    private static string BuildChildRelativePath(string parentRelativePath, string childName)
    {
        var normalizedChildName = NormalizeRelativePath(childName);
        if (parentRelativePath.Length == 0)
        {
            return normalizedChildName;
        }

        return $"{parentRelativePath}/{normalizedChildName}";
    }

    private static string NormalizeRelativePath(string path)
    {
        return path
            .Replace(Path.DirectorySeparatorChar, '/')
            .Replace(Path.AltDirectorySeparatorChar, '/')
            .Trim('/');
    }

    private static StringComparison GetPathComparison()
    {
        return OperatingSystem.IsWindows()
            ? StringComparison.OrdinalIgnoreCase
            : StringComparison.Ordinal;
    }

    private static StringComparer GetPathComparer()
    {
        return OperatingSystem.IsWindows()
            ? StringComparer.OrdinalIgnoreCase
            : StringComparer.Ordinal;
    }

    private readonly record struct FileSystemEntryCandidate(
        string AbsolutePath,
        string Name,
        bool IsDirectory,
        bool IsReparsePoint,
        long FileSizeInBytes);

    private readonly record struct DirectoryTraversalState(
        string AbsolutePath,
        string RelativePath,
        int Depth,
        IReadOnlyList<GitIgnoreRule> Rules);

    private readonly record struct GitIgnoreRule(
        string BaseRelativePath,
        Regex PatternRegex,
        bool IsNegated,
        bool IsDirectoryOnly,
        bool MatchFileNameOnly);
}
