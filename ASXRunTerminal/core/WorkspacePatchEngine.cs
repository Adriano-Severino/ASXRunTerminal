using System.Text;

namespace ASXRunTerminal.Core;

internal enum WorkspacePatchChangeKind
{
    Create = 1,
    Edit = 2,
    Delete = 3
}

internal readonly record struct WorkspacePatchChange(
    WorkspacePatchChangeKind Kind,
    string Path,
    string? Content = null,
    string? ExpectedContent = null)
{
    public static implicit operator WorkspacePatchChange(
        (WorkspacePatchChangeKind Kind, string Path, string? Content) tuple)
    {
        return new WorkspacePatchChange(
            Kind: tuple.Kind,
            Path: tuple.Path,
            Content: tuple.Content,
            ExpectedContent: null);
    }

    public static implicit operator WorkspacePatchChange(
        (WorkspacePatchChangeKind Kind, string Path, string? Content, string? ExpectedContent) tuple)
    {
        return new WorkspacePatchChange(
            Kind: tuple.Kind,
            Path: tuple.Path,
            Content: tuple.Content,
            ExpectedContent: tuple.ExpectedContent);
    }
}

internal readonly record struct WorkspacePatchRequest(
    IReadOnlyList<WorkspacePatchChange> Changes,
    bool PreviewOnly = false)
{
    public static implicit operator WorkspacePatchRequest(WorkspacePatchChange change)
    {
        return new WorkspacePatchRequest(
            Changes: [change],
            PreviewOnly: false);
    }

    public static implicit operator WorkspacePatchRequest(WorkspacePatchChange[] changes)
    {
        return new WorkspacePatchRequest(
            Changes: changes,
            PreviewOnly: false);
    }
}

internal readonly record struct WorkspacePatchFileResult(
    WorkspacePatchChangeKind Kind,
    string Path,
    string ResolvedPath,
    bool HasChanges,
    string UnifiedDiff);

internal readonly record struct WorkspacePatchResult(
    IReadOnlyList<WorkspacePatchFileResult> Files,
    bool IsPreviewOnly,
    int PlannedChangeCount,
    int AppliedChangeCount,
    int SkippedChangeCount)
{
    public bool HasChanges => PlannedChangeCount > 0;

    public string UnifiedDiff
    {
        get
        {
            var nonEmptyDiffs = Files
                .Select(static file => file.UnifiedDiff)
                .Where(static diff => !string.IsNullOrWhiteSpace(diff))
                .ToArray();

            return nonEmptyDiffs.Length == 0
                ? string.Empty
                : string.Join(Environment.NewLine + Environment.NewLine, nonEmptyDiffs);
        }
    }
}

internal sealed class WorkspacePatchEngine
{
    private const long MaxLcsCellCount = 4_000_000;
    private static readonly StringComparer PathComparer = GetPathComparer();
    private static readonly StringComparison PathComparison = GetPathComparison();

    private readonly WorkspaceFileOperations _fileOperations;
    private readonly string _workspaceRootDirectory;
    private readonly string _workspaceRootDirectoryWithSeparator;

    public WorkspacePatchEngine(string workspaceRootDirectory)
        : this(new WorkspaceFileOperations(workspaceRootDirectory))
    {
    }

    internal WorkspacePatchEngine(WorkspaceFileOperations fileOperations)
    {
        _fileOperations = fileOperations
            ?? throw new ArgumentNullException(nameof(fileOperations));
        _workspaceRootDirectory = _fileOperations.WorkspaceRootDirectoryPath;
        _workspaceRootDirectoryWithSeparator = EnsureTrailingDirectorySeparator(_workspaceRootDirectory);
    }

    public WorkspacePatchResult Apply(WorkspacePatchRequest request)
    {
        var changes = request.Changes
            ?? throw new ArgumentNullException(
                nameof(request),
                "A requisicao de patch deve informar a colecao de mudancas.");

        if (changes.Count == 0)
        {
            throw new InvalidOperationException(
                "A requisicao de patch deve conter ao menos uma mudanca.");
        }

        var plannedChanges = BuildPlannedChanges(changes);
        var plannedChangeCount = plannedChanges.Count(static plannedChange => plannedChange.HasChanges);
        var skippedChangeCount = plannedChanges.Count - plannedChangeCount;
        var appliedChangeCount = 0;

        if (!request.PreviewOnly)
        {
            foreach (var plannedChange in plannedChanges)
            {
                if (!plannedChange.HasChanges)
                {
                    continue;
                }

                ApplyPlannedChange(plannedChange);
                appliedChangeCount++;
            }
        }

        return new WorkspacePatchResult(
            Files: plannedChanges
                .Select(static plannedChange => new WorkspacePatchFileResult(
                    Kind: plannedChange.Change.Kind,
                    Path: plannedChange.RelativePath,
                    ResolvedPath: plannedChange.ResolvedPath,
                    HasChanges: plannedChange.HasChanges,
                    UnifiedDiff: plannedChange.UnifiedDiff))
                .ToArray(),
            IsPreviewOnly: request.PreviewOnly,
            PlannedChangeCount: plannedChangeCount,
            AppliedChangeCount: appliedChangeCount,
            SkippedChangeCount: skippedChangeCount);
    }

    private IReadOnlyList<WorkspacePatchPlannedChange> BuildPlannedChanges(
        IReadOnlyList<WorkspacePatchChange> changes)
    {
        var plannedChanges = new List<WorkspacePatchPlannedChange>(changes.Count);
        var touchedPaths = new HashSet<string>(PathComparer);

        foreach (var change in changes)
        {
            var resolvedPath = ResolveWorkspaceFilePath(change.Path);
            if (!touchedPaths.Add(resolvedPath))
            {
                throw new InvalidOperationException(
                    $"A requisicao de patch contem multiplas mudancas para o arquivo '{resolvedPath}'.");
            }

            var relativePath = NormalizeRelativePath(
                Path.GetRelativePath(_workspaceRootDirectory, resolvedPath));

            plannedChanges.Add(BuildPlannedChange(change, relativePath, resolvedPath));
        }

        return plannedChanges;
    }

    private WorkspacePatchPlannedChange BuildPlannedChange(
        WorkspacePatchChange change,
        string relativePath,
        string resolvedPath)
    {
        switch (change.Kind)
        {
            case WorkspacePatchChangeKind.Create:
                return BuildCreateChange(change, relativePath, resolvedPath);
            case WorkspacePatchChangeKind.Edit:
                return BuildEditChange(change, relativePath, resolvedPath);
            case WorkspacePatchChangeKind.Delete:
                return BuildDeleteChange(change, relativePath, resolvedPath);
            default:
                throw new InvalidOperationException(
                    $"O tipo de mudanca '{change.Kind}' nao e suportado.");
        }
    }

    private WorkspacePatchPlannedChange BuildCreateChange(
        WorkspacePatchChange change,
        string relativePath,
        string resolvedPath)
    {
        if (Directory.Exists(resolvedPath))
        {
            throw new InvalidOperationException(
                $"Nao foi possivel aplicar patch em '{resolvedPath}'. O caminho aponta para um diretorio.");
        }

        if (File.Exists(resolvedPath))
        {
            throw new InvalidOperationException(
                $"Nao foi possivel aplicar patch em '{resolvedPath}'. O arquivo ja existe.");
        }

        var newContent = EnsureContent(change);
        var unifiedDiff = BuildUnifiedDiff(
            relativePath,
            change.Kind,
            previousContent: string.Empty,
            nextContent: newContent);

        return new WorkspacePatchPlannedChange(
            Change: change,
            RelativePath: relativePath,
            ResolvedPath: resolvedPath,
            ContentToApply: newContent,
            HasChanges: true,
            UnifiedDiff: unifiedDiff);
    }

    private WorkspacePatchPlannedChange BuildEditChange(
        WorkspacePatchChange change,
        string relativePath,
        string resolvedPath)
    {
        if (Directory.Exists(resolvedPath))
        {
            throw new InvalidOperationException(
                $"Nao foi possivel aplicar patch em '{resolvedPath}'. O caminho aponta para um diretorio.");
        }

        if (!File.Exists(resolvedPath))
        {
            throw new FileNotFoundException(
                $"Nao foi possivel aplicar patch em '{resolvedPath}'. O arquivo nao existe.",
                resolvedPath);
        }

        var previousContent = File.ReadAllText(resolvedPath);
        ValidateExpectedContent(
            change,
            previousContent,
            resolvedPath);

        var nextContent = EnsureContent(change);
        var hasChanges = !string.Equals(previousContent, nextContent, StringComparison.Ordinal);
        var unifiedDiff = hasChanges
            ? BuildUnifiedDiff(
                relativePath,
                change.Kind,
                previousContent,
                nextContent)
            : string.Empty;

        return new WorkspacePatchPlannedChange(
            Change: change,
            RelativePath: relativePath,
            ResolvedPath: resolvedPath,
            ContentToApply: nextContent,
            HasChanges: hasChanges,
            UnifiedDiff: unifiedDiff);
    }

    private WorkspacePatchPlannedChange BuildDeleteChange(
        WorkspacePatchChange change,
        string relativePath,
        string resolvedPath)
    {
        if (Directory.Exists(resolvedPath))
        {
            throw new InvalidOperationException(
                $"Nao foi possivel aplicar patch em '{resolvedPath}'. O caminho aponta para um diretorio.");
        }

        if (!File.Exists(resolvedPath))
        {
            throw new FileNotFoundException(
                $"Nao foi possivel aplicar patch em '{resolvedPath}'. O arquivo nao existe.",
                resolvedPath);
        }

        var previousContent = File.ReadAllText(resolvedPath);
        ValidateExpectedContent(
            change,
            previousContent,
            resolvedPath);

        var unifiedDiff = BuildUnifiedDiff(
            relativePath,
            change.Kind,
            previousContent,
            nextContent: string.Empty);

        return new WorkspacePatchPlannedChange(
            Change: change,
            RelativePath: relativePath,
            ResolvedPath: resolvedPath,
            ContentToApply: null,
            HasChanges: true,
            UnifiedDiff: unifiedDiff);
    }

    private void ApplyPlannedChange(WorkspacePatchPlannedChange plannedChange)
    {
        switch (plannedChange.Change.Kind)
        {
            case WorkspacePatchChangeKind.Create:
                _fileOperations.Create(
                    plannedChange.RelativePath,
                    plannedChange.ContentToApply
                    ?? throw new InvalidOperationException(
                        $"A mudanca de criacao para '{plannedChange.RelativePath}' nao possui conteudo."));
                return;
            case WorkspacePatchChangeKind.Edit:
                _fileOperations.Edit(
                    plannedChange.RelativePath,
                    plannedChange.ContentToApply
                    ?? throw new InvalidOperationException(
                        $"A mudanca de edicao para '{plannedChange.RelativePath}' nao possui conteudo."));
                return;
            case WorkspacePatchChangeKind.Delete:
                _fileOperations.Delete(plannedChange.RelativePath);
                return;
            default:
                throw new InvalidOperationException(
                    $"O tipo de mudanca '{plannedChange.Change.Kind}' nao e suportado.");
        }
    }

    private static string EnsureContent(WorkspacePatchChange change)
    {
        if (change.Content is not null)
        {
            return change.Content;
        }

        throw new InvalidOperationException(
            $"A mudanca '{change.Kind}' para '{change.Path}' exige o conteudo final do arquivo.");
    }

    private static void ValidateExpectedContent(
        WorkspacePatchChange change,
        string currentContent,
        string resolvedPath)
    {
        if (change.ExpectedContent is null)
        {
            return;
        }

        if (!string.Equals(change.ExpectedContent, currentContent, StringComparison.Ordinal))
        {
            throw new InvalidOperationException(
                $"Nao foi possivel aplicar patch em '{resolvedPath}'. O conteudo atual diverge do conteudo esperado.");
        }
    }

    private static string BuildUnifiedDiff(
        string relativePath,
        WorkspacePatchChangeKind kind,
        string previousContent,
        string nextContent)
    {
        var normalizedRelativePath = NormalizeRelativePath(relativePath);
        var (beforeLabel, afterLabel) = GetDiffLabels(kind, normalizedRelativePath);
        var beforeLines = SplitLines(previousContent);
        var afterLines = SplitLines(nextContent);
        var diffLines = BuildDiffLines(beforeLines, afterLines);

        var hasChangedLines = diffLines.Any(static diffLine => diffLine.Kind != WorkspacePatchDiffLineKind.Unchanged);
        if (!hasChangedLines && kind == WorkspacePatchChangeKind.Edit)
        {
            return string.Empty;
        }

        var builder = new StringBuilder();
        builder.AppendLine($"--- {beforeLabel}");
        builder.AppendLine($"+++ {afterLabel}");

        if (hasChangedLines)
        {
            var beforeStart = beforeLines.Count == 0 ? 0 : 1;
            var afterStart = afterLines.Count == 0 ? 0 : 1;
            builder.AppendLine($"@@ -{beforeStart},{beforeLines.Count} +{afterStart},{afterLines.Count} @@");

            foreach (var diffLine in diffLines)
            {
                var prefix = diffLine.Kind switch
                {
                    WorkspacePatchDiffLineKind.Unchanged => ' ',
                    WorkspacePatchDiffLineKind.Added => '+',
                    WorkspacePatchDiffLineKind.Removed => '-',
                    _ => throw new InvalidOperationException(
                        $"Tipo de linha diff invalido: {diffLine.Kind}.")
                };

                builder.Append(prefix);
                builder.AppendLine(diffLine.Content);
            }
        }

        return builder.ToString().TrimEnd('\r', '\n');
    }

    private static (string BeforeLabel, string AfterLabel) GetDiffLabels(
        WorkspacePatchChangeKind kind,
        string relativePath)
    {
        return kind switch
        {
            WorkspacePatchChangeKind.Create => ("/dev/null", $"b/{relativePath}"),
            WorkspacePatchChangeKind.Delete => ($"a/{relativePath}", "/dev/null"),
            WorkspacePatchChangeKind.Edit => ($"a/{relativePath}", $"b/{relativePath}"),
            _ => throw new InvalidOperationException(
                $"O tipo de mudanca '{kind}' nao e suportado para gerar diff.")
        };
    }

    private static IReadOnlyList<WorkspacePatchDiffLine> BuildDiffLines(
        IReadOnlyList<string> beforeLines,
        IReadOnlyList<string> afterLines)
    {
        var matrixCellCount = (long)(beforeLines.Count + 1) * (afterLines.Count + 1);
        return matrixCellCount > MaxLcsCellCount
            ? BuildCoarseDiffLines(beforeLines, afterLines)
            : BuildLcsDiffLines(beforeLines, afterLines);
    }

    private static IReadOnlyList<WorkspacePatchDiffLine> BuildCoarseDiffLines(
        IReadOnlyList<string> beforeLines,
        IReadOnlyList<string> afterLines)
    {
        var diffLines = new List<WorkspacePatchDiffLine>(beforeLines.Count + afterLines.Count);
        diffLines.AddRange(beforeLines.Select(static line => new WorkspacePatchDiffLine(
            WorkspacePatchDiffLineKind.Removed,
            line)));
        diffLines.AddRange(afterLines.Select(static line => new WorkspacePatchDiffLine(
            WorkspacePatchDiffLineKind.Added,
            line)));
        return diffLines;
    }

    private static IReadOnlyList<WorkspacePatchDiffLine> BuildLcsDiffLines(
        IReadOnlyList<string> beforeLines,
        IReadOnlyList<string> afterLines)
    {
        var beforeCount = beforeLines.Count;
        var afterCount = afterLines.Count;
        var lcs = new int[beforeCount + 1, afterCount + 1];

        for (var beforeIndex = beforeCount - 1; beforeIndex >= 0; beforeIndex--)
        {
            for (var afterIndex = afterCount - 1; afterIndex >= 0; afterIndex--)
            {
                lcs[beforeIndex, afterIndex] = string.Equals(
                    beforeLines[beforeIndex],
                    afterLines[afterIndex],
                    StringComparison.Ordinal)
                    ? lcs[beforeIndex + 1, afterIndex + 1] + 1
                    : Math.Max(
                        lcs[beforeIndex + 1, afterIndex],
                        lcs[beforeIndex, afterIndex + 1]);
            }
        }

        var diffLines = new List<WorkspacePatchDiffLine>(beforeCount + afterCount);
        var currentBeforeIndex = 0;
        var currentAfterIndex = 0;

        while (currentBeforeIndex < beforeCount && currentAfterIndex < afterCount)
        {
            if (string.Equals(
                    beforeLines[currentBeforeIndex],
                    afterLines[currentAfterIndex],
                    StringComparison.Ordinal))
            {
                diffLines.Add(new WorkspacePatchDiffLine(
                    WorkspacePatchDiffLineKind.Unchanged,
                    beforeLines[currentBeforeIndex]));
                currentBeforeIndex++;
                currentAfterIndex++;
                continue;
            }

            if (lcs[currentBeforeIndex + 1, currentAfterIndex] >= lcs[currentBeforeIndex, currentAfterIndex + 1])
            {
                diffLines.Add(new WorkspacePatchDiffLine(
                    WorkspacePatchDiffLineKind.Removed,
                    beforeLines[currentBeforeIndex]));
                currentBeforeIndex++;
                continue;
            }

            diffLines.Add(new WorkspacePatchDiffLine(
                WorkspacePatchDiffLineKind.Added,
                afterLines[currentAfterIndex]));
            currentAfterIndex++;
        }

        while (currentBeforeIndex < beforeCount)
        {
            diffLines.Add(new WorkspacePatchDiffLine(
                WorkspacePatchDiffLineKind.Removed,
                beforeLines[currentBeforeIndex]));
            currentBeforeIndex++;
        }

        while (currentAfterIndex < afterCount)
        {
            diffLines.Add(new WorkspacePatchDiffLine(
                WorkspacePatchDiffLineKind.Added,
                afterLines[currentAfterIndex]));
            currentAfterIndex++;
        }

        return diffLines;
    }

    private string ResolveWorkspaceFilePath(string? path)
    {
        if (string.IsNullOrWhiteSpace(path))
        {
            throw new ArgumentException(
                "O argumento 'path' e obrigatorio para cada mudanca de patch.",
                nameof(path));
        }

        var trimmedPath = path.Trim();
        if (Path.EndsInDirectorySeparator(trimmedPath))
        {
            throw new InvalidOperationException(
                $"A mudanca de patch para '{trimmedPath}' deve apontar para um arquivo.");
        }

        var candidatePath = Path.IsPathRooted(trimmedPath)
            ? trimmedPath
            : Path.Combine(_workspaceRootDirectory, trimmedPath);
        var resolvedPath = Path.GetFullPath(candidatePath);

        if (!IsPathInsideWorkspaceRoot(resolvedPath))
        {
            throw new UnauthorizedAccessException(
                $"O caminho '{trimmedPath}' esta fora da raiz do workspace '{_workspaceRootDirectory}'.");
        }

        if (string.Equals(resolvedPath, _workspaceRootDirectory, PathComparison))
        {
            throw new InvalidOperationException(
                "Operacoes de patch na raiz do workspace exigem um caminho de arquivo especifico.");
        }

        return resolvedPath;
    }

    private bool IsPathInsideWorkspaceRoot(string resolvedPath)
    {
        if (string.Equals(resolvedPath, _workspaceRootDirectory, PathComparison))
        {
            return true;
        }

        return resolvedPath.StartsWith(_workspaceRootDirectoryWithSeparator, PathComparison);
    }

    private static IReadOnlyList<string> SplitLines(string content)
    {
        if (content.Length == 0)
        {
            return [];
        }

        var normalizedContent = content
            .Replace("\r\n", "\n")
            .Replace('\r', '\n');
        return normalizedContent.Split('\n');
    }

    private static string NormalizeRelativePath(string path)
    {
        return path
            .Replace(Path.DirectorySeparatorChar, '/')
            .Replace(Path.AltDirectorySeparatorChar, '/')
            .Trim('/');
    }

    private static string EnsureTrailingDirectorySeparator(string path)
    {
        return Path.EndsInDirectorySeparator(path)
            ? path
            : $"{path}{Path.DirectorySeparatorChar}";
    }

    private static StringComparer GetPathComparer()
    {
        return OperatingSystem.IsWindows()
            ? StringComparer.OrdinalIgnoreCase
            : StringComparer.Ordinal;
    }

    private static StringComparison GetPathComparison()
    {
        return OperatingSystem.IsWindows()
            ? StringComparison.OrdinalIgnoreCase
            : StringComparison.Ordinal;
    }

    private readonly record struct WorkspacePatchPlannedChange(
        WorkspacePatchChange Change,
        string RelativePath,
        string ResolvedPath,
        string? ContentToApply,
        bool HasChanges,
        string UnifiedDiff);

    private readonly record struct WorkspacePatchDiffLine(
        WorkspacePatchDiffLineKind Kind,
        string Content);

    private enum WorkspacePatchDiffLineKind
    {
        Unchanged = 0,
        Added = 1,
        Removed = 2
    }
}
