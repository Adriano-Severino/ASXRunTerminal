using ASXRunTerminal.Core;

namespace ASXRunTerminal.Tests;

public sealed class WorkspaceContextFileIndexTests
{
    [Fact]
    public void Refresh_WhenWorkspaceChanges_AppliesIncrementalDiffAndUpdatesQueries()
    {
        var root = CreateTemporaryDirectory();

        CreateFile(root, "src/Program.cs", "internal static class Program { }");
        CreateFile(root, "docs/README.md", "# docs");

        var index = new WorkspaceContextFileIndex(root);
        Assert.Equal(1, index.Version);

        CreateFile(
            root,
            "src/Program.cs",
            "internal static class Program { private static void Main() { } }");
        CreateFile(root, "src/NewFeature.cs", "internal sealed class NewFeature { }");
        File.Delete(Path.Combine(root, "docs", "README.md"));

        var refreshResult = index.Refresh();

        Assert.True(refreshResult.HasChanges);
        Assert.Equal(1, refreshResult.AddedEntryCount);
        Assert.Equal(1, refreshResult.UpdatedEntryCount);
        Assert.Equal(1, refreshResult.RemovedEntryCount);
        Assert.Equal(2, index.Version);

        var csharpFiles = index.Query(
            new WorkspaceContextQuery(
                Extension: "CS",
                Kind: WorkspaceEntryKind.File,
                Limit: 10));

        Assert.Equal(2, csharpFiles.Count);
        Assert.Contains(csharpFiles, entry => entry.RelativePath == "src/Program.cs");
        Assert.Contains(csharpFiles, entry => entry.RelativePath == "src/NewFeature.cs");

        var removedReadme = index.Query(
            new WorkspaceContextQuery(
                FileName: "readme.md",
                Limit: 10));

        Assert.Empty(removedReadme);
    }

    [Fact]
    public void Refresh_WhenWorkspaceIsUnchanged_DoesNotIncrementVersion()
    {
        var root = CreateTemporaryDirectory();

        CreateFile(root, "src/api/OrderController.cs");
        CreateFile(root, "src/domain/Order.cs");
        CreateFile(root, "README.md");

        var index = new WorkspaceContextFileIndex(root);
        var versionBeforeRefresh = index.Version;

        var refreshResult = index.Refresh();

        Assert.False(refreshResult.HasChanges);
        Assert.Equal(0, refreshResult.AddedEntryCount);
        Assert.Equal(0, refreshResult.UpdatedEntryCount);
        Assert.Equal(0, refreshResult.RemovedEntryCount);
        Assert.Equal(index.EntryCount, refreshResult.UnchangedEntryCount);
        Assert.Equal(versionBeforeRefresh, index.Version);
    }

    [Fact]
    public void Query_WhenPrefixAndLimitAreProvided_FiltersAndRespectsLimit()
    {
        var root = CreateTemporaryDirectory();

        CreateFile(root, "src/api/OrdersApi.cs");
        CreateFile(root, "src/domain/Order.cs");
        CreateFile(root, "tests/OrdersApiTests.cs");
        CreateFile(root, "notes/todo.txt");

        var index = new WorkspaceContextFileIndex(root);

        var prefixedQuery = index.Query(
            new WorkspaceContextQuery(
                RelativePathPrefix: @"src\domain",
                Extension: ".cs",
                Kind: WorkspaceEntryKind.File,
                Limit: 5));

        var domainEntry = Assert.Single(prefixedQuery);
        Assert.Equal("src/domain/Order.cs", domainEntry.RelativePath);

        var limitedQuery = index.Query(
            new WorkspaceContextQuery(
                Extension: ".cs",
                Kind: WorkspaceEntryKind.File,
                Limit: 1));

        Assert.Single(limitedQuery);
    }

    [Fact]
    public void Catalog_GetOrCreate_ReusesCachedInstanceForSameWorkspaceAndOptions()
    {
        WorkspaceContextFileIndexCatalog.ClearCache();
        var root = CreateTemporaryDirectory();
        CreateFile(root, "src/App.cs");

        try
        {
            var cachedIndex = WorkspaceContextFileIndexCatalog.GetOrCreate(root);
            var cachedIndexFromEquivalentPath = WorkspaceContextFileIndexCatalog.GetOrCreate(
                Path.Combine(root, "."));

            var constrainedOptions = new WorkspaceStructureMapOptions(
                MaxEntries: 10,
                MaxDepth: 12,
                MaxGitIgnoreFileSizeInBytes: 262_144,
                MaxGitIgnoreRulesPerFile: 2_048);
            var cachedIndexWithDifferentOptions = WorkspaceContextFileIndexCatalog.GetOrCreate(
                root,
                constrainedOptions);

            Assert.Same(cachedIndex, cachedIndexFromEquivalentPath);
            Assert.NotSame(cachedIndex, cachedIndexWithDifferentOptions);
        }
        finally
        {
            WorkspaceContextFileIndexCatalog.ClearCache();
        }
    }

    [Fact]
    public void Catalog_Invalidate_WhenEntryExists_RemovesCachedIndex()
    {
        WorkspaceContextFileIndexCatalog.ClearCache();
        var root = CreateTemporaryDirectory();
        CreateFile(root, "src/App.cs");

        try
        {
            var previousIndex = WorkspaceContextFileIndexCatalog.GetOrCreate(root);

            var removed = WorkspaceContextFileIndexCatalog.Invalidate(root);
            var recreatedIndex = WorkspaceContextFileIndexCatalog.GetOrCreate(root);

            Assert.True(removed);
            Assert.NotSame(previousIndex, recreatedIndex);
        }
        finally
        {
            WorkspaceContextFileIndexCatalog.ClearCache();
        }
    }

    private static string CreateTemporaryDirectory()
    {
        var directoryPath = Path.Combine(
            Path.GetTempPath(),
            "asxrun-workspace-context-index-tests",
            Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(directoryPath);
        return directoryPath;
    }

    private static string CreateFile(
        string rootDirectory,
        string relativePath,
        string content = "content")
    {
        var filePath = Path.Combine(
            rootDirectory,
            relativePath
                .Replace('/', Path.DirectorySeparatorChar)
                .Replace('\\', Path.DirectorySeparatorChar));
        var fileDirectory = Path.GetDirectoryName(filePath)
            ?? throw new InvalidOperationException("O diretorio do arquivo nao pode ser nulo.");

        Directory.CreateDirectory(fileDirectory);
        File.WriteAllText(filePath, content);
        return filePath;
    }
}
