namespace ASXRunTerminal.Core;

internal readonly record struct AgentStableStateFile(
    string RelativePath,
    byte[] Content)
{
    public static implicit operator AgentStableStateFile(
        (string RelativePath, byte[] Content) tuple)
    {
        return new AgentStableStateFile(
            RelativePath: tuple.RelativePath,
            Content: tuple.Content);
    }
}

internal readonly record struct AgentStableStateSnapshot(
    string WorkspaceRootDirectory,
    DateTimeOffset CreatedAtUtc,
    IReadOnlyList<AgentStableStateFile> Files)
{
    public int FileCount => Files.Count;
}

internal readonly record struct AgentStableStateRestoreResult(
    int RestoredFileCount,
    int RemovedFileCount,
    int RemovedDirectoryCount,
    IReadOnlyList<string> ChangedPaths)
{
    public int TotalChangeCount =>
        RestoredFileCount + RemovedFileCount + RemovedDirectoryCount;

    public bool HasChanges => TotalChangeCount > 0;
}

internal sealed class AgentStableStateStore
{
    private static readonly StringComparer PathComparer = GetPathComparer();
    private static readonly StringComparison PathComparison = GetPathComparison();

    public AgentStableStateSnapshot Capture(string workspaceRootDirectory)
    {
        var rootDirectory = ResolveRootDirectory(workspaceRootDirectory);
        var workspaceMap = WorkspaceFileStructureMapper.Map(rootDirectory);
        EnsureCompleteWorkspaceMap(workspaceMap);

        var files = new List<AgentStableStateFile>();
        foreach (var entry in workspaceMap.Entries
            .Where(static entry => entry.Kind == WorkspaceEntryKind.File)
            .OrderBy(static entry => entry.RelativePath, PathComparer))
        {
            var relativePath = NormalizeRelativePath(entry.RelativePath);
            var resolvedPath = ResolveWorkspacePath(rootDirectory, relativePath);
            if (!File.Exists(resolvedPath))
            {
                throw new FileNotFoundException(
                    $"Nao foi possivel capturar o arquivo '{relativePath}' porque ele nao existe mais.",
                    resolvedPath);
            }

            files.Add((relativePath, File.ReadAllBytes(resolvedPath)));
        }

        return new AgentStableStateSnapshot(
            WorkspaceRootDirectory: rootDirectory,
            CreatedAtUtc: DateTimeOffset.UtcNow,
            Files: files);
    }

    public AgentStableStateRestoreResult Restore(AgentStableStateSnapshot snapshot)
    {
        var rootDirectory = ResolveRootDirectory(snapshot.WorkspaceRootDirectory);
        var snapshotFiles = BuildSnapshotFileMap(snapshot);
        var snapshotDirectories = BuildSnapshotDirectorySet(snapshotFiles.Keys);
        var workspaceMap = WorkspaceFileStructureMapper.Map(rootDirectory);
        EnsureCompleteWorkspaceMap(workspaceMap);

        var changedPaths = new SortedSet<string>(PathComparer);
        var restoredFileCount = 0;
        var removedFileCount = 0;
        var removedDirectoryCount = 0;

        foreach (var entry in workspaceMap.Entries
            .Where(static entry => entry.Kind == WorkspaceEntryKind.File)
            .OrderByDescending(static entry => entry.RelativePath.Length))
        {
            var relativePath = NormalizeRelativePath(entry.RelativePath);
            if (snapshotFiles.ContainsKey(relativePath))
            {
                continue;
            }

            var resolvedPath = ResolveWorkspacePath(rootDirectory, relativePath);
            if (!File.Exists(resolvedPath))
            {
                continue;
            }

            File.Delete(resolvedPath);
            removedFileCount++;
            changedPaths.Add(relativePath);
        }

        foreach (var (relativePath, stableFile) in snapshotFiles.OrderBy(static item => item.Key, PathComparer))
        {
            var resolvedPath = ResolveWorkspacePath(rootDirectory, relativePath);
            if (Directory.Exists(resolvedPath))
            {
                Directory.Delete(resolvedPath, recursive: true);
            }

            var shouldRestore = !File.Exists(resolvedPath)
                || !File.ReadAllBytes(resolvedPath).SequenceEqual(stableFile.Content);
            if (!shouldRestore)
            {
                continue;
            }

            var parentDirectory = Path.GetDirectoryName(resolvedPath);
            if (!string.IsNullOrWhiteSpace(parentDirectory))
            {
                Directory.CreateDirectory(parentDirectory);
            }

            File.WriteAllBytes(resolvedPath, stableFile.Content);
            restoredFileCount++;
            changedPaths.Add(relativePath);
        }

        foreach (var entry in workspaceMap.Entries
            .Where(static entry => entry.Kind == WorkspaceEntryKind.Directory)
            .Select(static entry => NormalizeRelativePath(entry.RelativePath))
            .OrderByDescending(static relativePath => relativePath.Length))
        {
            if (snapshotDirectories.Contains(entry))
            {
                continue;
            }

            var resolvedPath = ResolveWorkspacePath(rootDirectory, entry);
            if (!Directory.Exists(resolvedPath)
                || Directory.EnumerateFileSystemEntries(resolvedPath).Any())
            {
                continue;
            }

            Directory.Delete(resolvedPath);
            removedDirectoryCount++;
            changedPaths.Add(entry);
        }

        return new AgentStableStateRestoreResult(
            RestoredFileCount: restoredFileCount,
            RemovedFileCount: removedFileCount,
            RemovedDirectoryCount: removedDirectoryCount,
            ChangedPaths: changedPaths.ToArray());
    }

    private static Dictionary<string, AgentStableStateFile> BuildSnapshotFileMap(
        AgentStableStateSnapshot snapshot)
    {
        if (snapshot.Files is null)
        {
            throw new InvalidOperationException(
                "O snapshot de estado estavel nao contem arquivos.");
        }

        var files = new Dictionary<string, AgentStableStateFile>(PathComparer);
        foreach (var file in snapshot.Files)
        {
            var relativePath = NormalizeRelativePath(file.RelativePath);
            if (string.IsNullOrWhiteSpace(relativePath))
            {
                throw new InvalidOperationException(
                    "O snapshot de estado estavel contem um caminho de arquivo vazio.");
            }

            files[relativePath] = new AgentStableStateFile(
                RelativePath: relativePath,
                Content: file.Content ?? throw new InvalidOperationException(
                    $"O snapshot de estado estavel nao contem conteudo para '{relativePath}'."));
        }

        return files;
    }

    private static SortedSet<string> BuildSnapshotDirectorySet(IEnumerable<string> relativePaths)
    {
        var directories = new SortedSet<string>(PathComparer);
        foreach (var relativePath in relativePaths)
        {
            var currentPath = NormalizeRelativePath(relativePath);
            while (true)
            {
                var separatorIndex = currentPath.LastIndexOf('/');
                if (separatorIndex <= 0)
                {
                    break;
                }

                currentPath = currentPath[..separatorIndex];
                directories.Add(currentPath);
            }
        }

        return directories;
    }

    private static void EnsureCompleteWorkspaceMap(WorkspaceStructureMap workspaceMap)
    {
        if (!workspaceMap.IsTruncated)
        {
            return;
        }

        var limit = workspaceMap.LimitKind switch
        {
            WorkspaceMapLimitKind.MaxDepth => "profundidade maxima",
            WorkspaceMapLimitKind.MaxEntries => "quantidade maxima de entradas",
            _ => "limite de mapeamento"
        };
        throw new InvalidOperationException(
            $"Nao foi possivel mapear o workspace completo para rollback automatico: limite atingido por {limit}.");
    }

    private static string ResolveRootDirectory(string workspaceRootDirectory)
    {
        if (string.IsNullOrWhiteSpace(workspaceRootDirectory))
        {
            throw new ArgumentException(
                "O diretorio raiz do workspace nao pode estar vazio.",
                nameof(workspaceRootDirectory));
        }

        var resolvedRoot = Path.GetFullPath(workspaceRootDirectory);
        if (!Directory.Exists(resolvedRoot))
        {
            throw new DirectoryNotFoundException(
                $"O diretorio raiz do workspace '{resolvedRoot}' nao foi encontrado.");
        }

        var pathRoot = Path.GetPathRoot(resolvedRoot);
        return string.Equals(resolvedRoot, pathRoot, PathComparison)
            ? resolvedRoot
            : resolvedRoot.TrimEnd(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar);
    }

    private static string ResolveWorkspacePath(
        string rootDirectory,
        string relativePath)
    {
        var normalizedPath = NormalizeRelativePath(relativePath);
        if (string.IsNullOrWhiteSpace(normalizedPath))
        {
            throw new InvalidOperationException(
                "O caminho relativo do snapshot de rollback nao pode estar vazio.");
        }

        var candidatePath = Path.Combine(
            rootDirectory,
            normalizedPath.Replace('/', Path.DirectorySeparatorChar));
        var resolvedPath = Path.GetFullPath(candidatePath);
        var rootWithSeparator = Path.EndsInDirectorySeparator(rootDirectory)
            ? rootDirectory
            : $"{rootDirectory}{Path.DirectorySeparatorChar}";

        if (!resolvedPath.StartsWith(rootWithSeparator, PathComparison))
        {
            throw new UnauthorizedAccessException(
                $"O caminho '{relativePath}' esta fora da raiz do workspace '{rootDirectory}'.");
        }

        return resolvedPath;
    }

    private static string NormalizeRelativePath(string path)
    {
        return path
            .Replace(Path.DirectorySeparatorChar, '/')
            .Replace(Path.AltDirectorySeparatorChar, '/')
            .Trim('/');
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
}
