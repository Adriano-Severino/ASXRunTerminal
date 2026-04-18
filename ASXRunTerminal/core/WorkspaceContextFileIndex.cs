namespace ASXRunTerminal.Core;

internal readonly record struct WorkspaceContextQuery(
    string? RelativePathPrefix = null,
    string? FileName = null,
    string? Extension = null,
    WorkspaceEntryKind? Kind = null,
    int Limit = 200);

internal readonly record struct WorkspaceContextIndexRefreshResult(
    int AddedEntryCount,
    int UpdatedEntryCount,
    int RemovedEntryCount,
    int UnchangedEntryCount)
{
    public bool HasChanges => AddedEntryCount > 0 || UpdatedEntryCount > 0 || RemovedEntryCount > 0;
}

internal sealed class WorkspaceContextFileIndex
{
    private static readonly StringComparer PathComparer = GetPathComparer();
    private static readonly StringComparison PathComparison = GetPathComparison();
    private readonly object _syncLock = new();
    private readonly string _workspaceRootDirectory;
    private readonly WorkspaceStructureMapOptions _options;
    private readonly Func<string, WorkspaceStructureMapOptions, WorkspaceStructureMap> _mapResolver;
    private readonly Func<DateTime> _utcNowResolver;
    private readonly SortedDictionary<string, WorkspaceEntry> _entriesByPath;
    private readonly Dictionary<string, SortedSet<string>> _pathsByFileName;
    private readonly Dictionary<string, SortedSet<string>> _filePathsByExtension;

    private WorkspaceStructureMap _currentMap;
    private int _version;
    private DateTime _lastIndexedAtUtc;

    public WorkspaceContextFileIndex(
        string workspaceRootDirectory,
        WorkspaceStructureMapOptions? options = null,
        Func<string, WorkspaceStructureMapOptions, WorkspaceStructureMap>? mapResolver = null,
        Func<DateTime>? utcNowResolver = null)
    {
        _workspaceRootDirectory = ResolveRootDirectory(workspaceRootDirectory);
        _options = options ?? GetDefaultOptions();
        _mapResolver = mapResolver
            ?? ((rootDirectoryPath, resolvedOptions) => WorkspaceFileStructureMapper.Map(rootDirectoryPath, resolvedOptions));
        _utcNowResolver = utcNowResolver ?? (() => DateTime.UtcNow);
        _entriesByPath = new SortedDictionary<string, WorkspaceEntry>(PathComparer);
        _pathsByFileName = new Dictionary<string, SortedSet<string>>(StringComparer.OrdinalIgnoreCase);
        _filePathsByExtension = new Dictionary<string, SortedSet<string>>(StringComparer.OrdinalIgnoreCase);

        Refresh();
    }

    public string WorkspaceRootDirectoryPath => _workspaceRootDirectory;
    public WorkspaceStructureMapOptions Options => _options;

    public int Version
    {
        get
        {
            lock (_syncLock)
            {
                return _version;
            }
        }
    }

    public DateTime LastIndexedAtUtc
    {
        get
        {
            lock (_syncLock)
            {
                return _lastIndexedAtUtc;
            }
        }
    }

    public int EntryCount
    {
        get
        {
            lock (_syncLock)
            {
                return _entriesByPath.Count;
            }
        }
    }

    public WorkspaceStructureMap CurrentMap
    {
        get
        {
            lock (_syncLock)
            {
                return _currentMap;
            }
        }
    }

    public WorkspaceContextIndexRefreshResult Refresh()
    {
        var latestMap = _mapResolver(_workspaceRootDirectory, _options);
        return Refresh(latestMap);
    }

    public IReadOnlyList<WorkspaceEntry> Query(WorkspaceContextQuery? query = null)
    {
        var resolvedQuery = query ?? new WorkspaceContextQuery();
        if (resolvedQuery.Limit <= 0)
        {
            throw new ArgumentOutOfRangeException(
                nameof(query),
                "O limite de resultados deve ser maior que zero.");
        }

        var normalizedPrefix = NormalizeOptionalPath(resolvedQuery.RelativePathPrefix);
        var normalizedFileName = NormalizeOptionalToken(resolvedQuery.FileName);
        var normalizedExtension = NormalizeOptionalExtension(resolvedQuery.Extension);

        lock (_syncLock)
        {
            var pathFilterByFileName = ResolveOptionalPathFilter(
                _pathsByFileName,
                normalizedFileName);
            var pathFilterByExtension = ResolveOptionalPathFilter(
                _filePathsByExtension,
                normalizedExtension);
            var candidatePaths = ResolveCandidatePaths(pathFilterByFileName, pathFilterByExtension);
            var resolvedEntries = new List<WorkspaceEntry>(Math.Min(resolvedQuery.Limit, _entriesByPath.Count));

            foreach (var candidatePath in candidatePaths)
            {
                if (pathFilterByFileName is not null && !pathFilterByFileName.Contains(candidatePath))
                {
                    continue;
                }

                if (pathFilterByExtension is not null && !pathFilterByExtension.Contains(candidatePath))
                {
                    continue;
                }

                if (!_entriesByPath.TryGetValue(candidatePath, out var entry))
                {
                    continue;
                }

                if (resolvedQuery.Kind is WorkspaceEntryKind kind && entry.Kind != kind)
                {
                    continue;
                }

                if (normalizedPrefix is not null && !PathHasPrefix(candidatePath, normalizedPrefix))
                {
                    continue;
                }

                resolvedEntries.Add(entry);
                if (resolvedEntries.Count >= resolvedQuery.Limit)
                {
                    break;
                }
            }

            return resolvedEntries;
        }
    }

    internal WorkspaceContextIndexRefreshResult Refresh(WorkspaceStructureMap latestMap)
    {
        lock (_syncLock)
        {
            return ApplyMap(latestMap);
        }
    }

    private WorkspaceContextIndexRefreshResult ApplyMap(WorkspaceStructureMap latestMap)
    {
        var mapRoot = ResolveRootDirectory(latestMap.RootDirectoryPath);
        if (!string.Equals(mapRoot, _workspaceRootDirectory, PathComparison))
        {
            throw new InvalidOperationException(
                $"O mapa informado pertence ao diretorio '{mapRoot}', mas o indice foi criado para '{_workspaceRootDirectory}'.");
        }

        var incomingEntriesByPath = new Dictionary<string, WorkspaceEntry>(PathComparer);
        foreach (var entry in latestMap.Entries)
        {
            var normalizedPath = NormalizeRelativePath(entry.RelativePath);
            var normalizedEntry = entry with { RelativePath = normalizedPath };
            incomingEntriesByPath[normalizedPath] = normalizedEntry;
        }

        var previousEntriesByPath = new Dictionary<string, WorkspaceEntry>(_entriesByPath, PathComparer);
        var removedEntries = new List<WorkspaceEntry>();
        foreach (var previousEntry in previousEntriesByPath.Values)
        {
            if (!incomingEntriesByPath.ContainsKey(previousEntry.RelativePath))
            {
                removedEntries.Add(previousEntry);
            }
        }

        foreach (var removedEntry in removedEntries)
        {
            RemoveEntryFromSecondaryIndexes(removedEntry);
            _entriesByPath.Remove(removedEntry.RelativePath);
        }

        var addedEntryCount = 0;
        var updatedEntryCount = 0;
        var unchangedEntryCount = 0;

        foreach (var (relativePath, incomingEntry) in incomingEntriesByPath)
        {
            if (!previousEntriesByPath.TryGetValue(relativePath, out var previousEntry))
            {
                AddOrUpdateEntryInIndexes(incomingEntry);
                addedEntryCount++;
                continue;
            }

            if (previousEntry.Equals(incomingEntry))
            {
                unchangedEntryCount++;
                continue;
            }

            RemoveEntryFromSecondaryIndexes(previousEntry);
            AddOrUpdateEntryInIndexes(incomingEntry);
            updatedEntryCount++;
        }

        var removedEntryCount = removedEntries.Count;
        if (addedEntryCount > 0 || updatedEntryCount > 0 || removedEntryCount > 0)
        {
            _version++;
        }

        _lastIndexedAtUtc = _utcNowResolver();
        _currentMap = latestMap with { Entries = _entriesByPath.Values.ToArray() };

        return new WorkspaceContextIndexRefreshResult(
            AddedEntryCount: addedEntryCount,
            UpdatedEntryCount: updatedEntryCount,
            RemovedEntryCount: removedEntryCount,
            UnchangedEntryCount: unchangedEntryCount);
    }

    private void AddOrUpdateEntryInIndexes(WorkspaceEntry entry)
    {
        _entriesByPath[entry.RelativePath] = entry;

        var leafName = GetLeafPathSegment(entry.RelativePath);
        if (leafName.Length > 0)
        {
            AddPathToSecondaryIndex(_pathsByFileName, leafName, entry.RelativePath);
        }

        if (entry.Kind == WorkspaceEntryKind.File)
        {
            var extension = Path.GetExtension(entry.RelativePath);
            if (!string.IsNullOrWhiteSpace(extension))
            {
                AddPathToSecondaryIndex(_filePathsByExtension, extension, entry.RelativePath);
            }
        }
    }

    private void RemoveEntryFromSecondaryIndexes(WorkspaceEntry entry)
    {
        var leafName = GetLeafPathSegment(entry.RelativePath);
        if (leafName.Length > 0)
        {
            RemovePathFromSecondaryIndex(_pathsByFileName, leafName, entry.RelativePath);
        }

        if (entry.Kind == WorkspaceEntryKind.File)
        {
            var extension = Path.GetExtension(entry.RelativePath);
            if (!string.IsNullOrWhiteSpace(extension))
            {
                RemovePathFromSecondaryIndex(_filePathsByExtension, extension, entry.RelativePath);
            }
        }
    }

    private static SortedSet<string>? ResolveOptionalPathFilter(
        IReadOnlyDictionary<string, SortedSet<string>> index,
        string? key)
    {
        if (key is null)
        {
            return null;
        }

        return index.TryGetValue(key, out var indexedPaths)
            ? indexedPaths
            : [];
    }

    private IEnumerable<string> ResolveCandidatePaths(
        SortedSet<string>? pathFilterByFileName,
        SortedSet<string>? pathFilterByExtension)
    {
        if (pathFilterByFileName is not null && pathFilterByExtension is not null)
        {
            return pathFilterByFileName.Count <= pathFilterByExtension.Count
                ? pathFilterByFileName
                : pathFilterByExtension;
        }

        if (pathFilterByFileName is not null)
        {
            return pathFilterByFileName;
        }

        if (pathFilterByExtension is not null)
        {
            return pathFilterByExtension;
        }

        return _entriesByPath.Keys;
    }

    private static void AddPathToSecondaryIndex(
        IDictionary<string, SortedSet<string>> index,
        string key,
        string relativePath)
    {
        if (!index.TryGetValue(key, out var indexedPaths))
        {
            indexedPaths = new SortedSet<string>(PathComparer);
            index[key] = indexedPaths;
        }

        indexedPaths.Add(relativePath);
    }

    private static void RemovePathFromSecondaryIndex(
        IDictionary<string, SortedSet<string>> index,
        string key,
        string relativePath)
    {
        if (!index.TryGetValue(key, out var indexedPaths))
        {
            return;
        }

        indexedPaths.Remove(relativePath);
        if (indexedPaths.Count == 0)
        {
            index.Remove(key);
        }
    }

    private static bool PathHasPrefix(string candidatePath, string normalizedPrefix)
    {
        if (string.Equals(candidatePath, normalizedPrefix, PathComparison))
        {
            return true;
        }

        if (!candidatePath.StartsWith(normalizedPrefix, PathComparison))
        {
            return false;
        }

        return candidatePath.Length > normalizedPrefix.Length
            && candidatePath[normalizedPrefix.Length] == '/';
    }

    private static string ResolveRootDirectory(string workspaceRootDirectory)
    {
        if (string.IsNullOrWhiteSpace(workspaceRootDirectory))
        {
            throw new InvalidOperationException(
                "Nao foi possivel resolver o diretorio raiz para indexar o workspace.");
        }

        var resolvedRootDirectory = Path.GetFullPath(workspaceRootDirectory.Trim());
        if (!Directory.Exists(resolvedRootDirectory))
        {
            throw new DirectoryNotFoundException(
                $"Nao foi possivel indexar o workspace. O diretorio '{resolvedRootDirectory}' nao existe.");
        }

        return resolvedRootDirectory;
    }

    private static string? NormalizeOptionalPath(string? path)
    {
        if (string.IsNullOrWhiteSpace(path))
        {
            return null;
        }

        var normalizedPath = NormalizeRelativePath(path.Trim());
        return normalizedPath.Length == 0
            ? null
            : normalizedPath;
    }

    private static string? NormalizeOptionalToken(string? token)
    {
        if (string.IsNullOrWhiteSpace(token))
        {
            return null;
        }

        return token.Trim();
    }

    private static string? NormalizeOptionalExtension(string? extension)
    {
        if (string.IsNullOrWhiteSpace(extension))
        {
            return null;
        }

        var trimmedExtension = extension.Trim();
        return trimmedExtension.StartsWith('.')
            ? trimmedExtension
            : $".{trimmedExtension}";
    }

    private static string NormalizeRelativePath(string path)
    {
        return path
            .Replace(Path.DirectorySeparatorChar, '/')
            .Replace(Path.AltDirectorySeparatorChar, '/')
            .Trim('/');
    }

    private static string GetLeafPathSegment(string relativePath)
    {
        if (string.IsNullOrWhiteSpace(relativePath))
        {
            return string.Empty;
        }

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

    private static WorkspaceStructureMapOptions GetDefaultOptions()
    {
        return new WorkspaceStructureMapOptions(
            MaxEntries: 5_000,
            MaxDepth: 12,
            MaxGitIgnoreFileSizeInBytes: 262_144,
            MaxGitIgnoreRulesPerFile: 2_048);
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

internal static class WorkspaceContextFileIndexCatalog
{
    private static readonly object CacheLock = new();
    private static readonly StringComparer PathComparer = GetPathComparer();
    private static readonly StringComparison PathComparison = GetPathComparison();
    private static readonly Dictionary<WorkspaceContextIndexCacheKey, WorkspaceContextFileIndex> CachedIndexes =
        new(new WorkspaceContextIndexCacheKeyComparer());

    public static WorkspaceContextFileIndex GetOrCreate(
        string workspaceRootDirectory,
        WorkspaceStructureMapOptions? options = null)
    {
        var resolvedRootDirectory = ResolveRootDirectory(workspaceRootDirectory);
        var resolvedOptions = options ?? GetDefaultOptions();
        var cacheKey = new WorkspaceContextIndexCacheKey(
            RootDirectoryPath: resolvedRootDirectory,
            Options: resolvedOptions);

        lock (CacheLock)
        {
            if (CachedIndexes.TryGetValue(cacheKey, out var cachedIndex))
            {
                return cachedIndex;
            }

            var createdIndex = new WorkspaceContextFileIndex(resolvedRootDirectory, resolvedOptions);
            CachedIndexes[cacheKey] = createdIndex;
            return createdIndex;
        }
    }

    public static WorkspaceContextIndexRefreshResult Refresh(
        string workspaceRootDirectory,
        WorkspaceStructureMapOptions? options = null)
    {
        return GetOrCreate(workspaceRootDirectory, options).Refresh();
    }

    public static bool Invalidate(
        string workspaceRootDirectory,
        WorkspaceStructureMapOptions? options = null)
    {
        var resolvedRootDirectory = ResolveRootDirectory(workspaceRootDirectory);
        var resolvedOptions = options ?? GetDefaultOptions();
        var cacheKey = new WorkspaceContextIndexCacheKey(
            RootDirectoryPath: resolvedRootDirectory,
            Options: resolvedOptions);

        lock (CacheLock)
        {
            return CachedIndexes.Remove(cacheKey);
        }
    }

    public static void ClearCache()
    {
        lock (CacheLock)
        {
            CachedIndexes.Clear();
        }
    }

    private static string ResolveRootDirectory(string workspaceRootDirectory)
    {
        if (string.IsNullOrWhiteSpace(workspaceRootDirectory))
        {
            throw new InvalidOperationException(
                "Nao foi possivel resolver o diretorio raiz para acessar o cache de contexto.");
        }

        var resolvedRootDirectory = Path.GetFullPath(workspaceRootDirectory.Trim());
        if (!Directory.Exists(resolvedRootDirectory))
        {
            throw new DirectoryNotFoundException(
                $"Nao foi possivel acessar o cache de contexto. O diretorio '{resolvedRootDirectory}' nao existe.");
        }

        return resolvedRootDirectory;
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

    private static WorkspaceStructureMapOptions GetDefaultOptions()
    {
        return new WorkspaceStructureMapOptions(
            MaxEntries: 5_000,
            MaxDepth: 12,
            MaxGitIgnoreFileSizeInBytes: 262_144,
            MaxGitIgnoreRulesPerFile: 2_048);
    }

    private readonly record struct WorkspaceContextIndexCacheKey(
        string RootDirectoryPath,
        WorkspaceStructureMapOptions Options);

    private sealed class WorkspaceContextIndexCacheKeyComparer
        : IEqualityComparer<WorkspaceContextIndexCacheKey>
    {
        public bool Equals(WorkspaceContextIndexCacheKey x, WorkspaceContextIndexCacheKey y)
        {
            return string.Equals(x.RootDirectoryPath, y.RootDirectoryPath, PathComparison)
                && x.Options.Equals(y.Options);
        }

        public int GetHashCode(WorkspaceContextIndexCacheKey key)
        {
            var hashCode = new HashCode();
            hashCode.Add(key.RootDirectoryPath, PathComparer);
            hashCode.Add(key.Options);
            return hashCode.ToHashCode();
        }
    }
}
