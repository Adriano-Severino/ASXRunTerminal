using ASXRunTerminal.Core;

namespace ASXRunTerminal.Tests;

public sealed class WorkspaceFileStructureMapperTests
{
    [Fact]
    public void Map_WhenGitIgnoreDefinesIgnoreAndNegationRules_RespectsBoth()
    {
        var root = CreateTemporaryDirectory();

        CreateFile(
            root,
            ".gitignore",
            """
            bin/
            *.log
            !logs/keep.log
            """);
        CreateFile(root, "src/Program.cs");
        CreateFile(root, "bin/artifact.dll");
        CreateFile(root, "logs/error.log");
        CreateFile(root, "logs/keep.log");

        var map = WorkspaceFileStructureMapper.Map(root);

        Assert.Contains(map.Entries, entry => entry.RelativePath == ".gitignore");
        Assert.Contains(map.Entries, entry => entry.RelativePath == "src");
        Assert.Contains(map.Entries, entry => entry.RelativePath == "src/Program.cs");
        Assert.Contains(map.Entries, entry => entry.RelativePath == "logs");
        Assert.Contains(map.Entries, entry => entry.RelativePath == "logs/keep.log");
        Assert.DoesNotContain(map.Entries, entry => entry.RelativePath == "bin");
        Assert.DoesNotContain(map.Entries, entry => entry.RelativePath == "bin/artifact.dll");
        Assert.DoesNotContain(map.Entries, entry => entry.RelativePath == "logs/error.log");
        Assert.False(map.IsTruncated);
    }

    [Fact]
    public void Map_WhenNestedGitIgnoreExists_AppliesRulesFromNestedDirectory()
    {
        var root = CreateTemporaryDirectory();

        CreateFile(root, "src/.gitignore", "generated/");
        CreateFile(root, "src/generated/ApiClient.g.cs");
        CreateFile(root, "src/domain/Order.cs");

        var map = WorkspaceFileStructureMapper.Map(root);

        Assert.Contains(map.Entries, entry => entry.RelativePath == "src/domain/Order.cs");
        Assert.DoesNotContain(map.Entries, entry => entry.RelativePath == "src/generated");
        Assert.DoesNotContain(map.Entries, entry => entry.RelativePath == "src/generated/ApiClient.g.cs");
    }

    [Fact]
    public void Map_WhenMaxEntriesIsReached_ReturnsTruncatedMap()
    {
        var root = CreateTemporaryDirectory();

        CreateFile(root, "a.txt");
        CreateFile(root, "b.txt");
        CreateFile(root, "c.txt");
        CreateFile(root, "d.txt");

        var map = WorkspaceFileStructureMapper.Map(
            root,
            new WorkspaceStructureMapOptions(
                MaxEntries: 2,
                MaxDepth: 12,
                MaxGitIgnoreFileSizeInBytes: 262_144,
                MaxGitIgnoreRulesPerFile: 2_048));

        Assert.Equal(2, map.Entries.Count);
        Assert.True(map.IsTruncated);
        Assert.Equal(WorkspaceMapLimitKind.MaxEntries, map.LimitKind);
    }

    [Fact]
    public void Map_WhenMaxDepthIsReached_SkipsDeeperDirectories()
    {
        var root = CreateTemporaryDirectory();

        CreateFile(root, "level-1/keep.txt");
        CreateFile(root, "level-1/level-2/too-deep.txt");

        var map = WorkspaceFileStructureMapper.Map(
            root,
            new WorkspaceStructureMapOptions(
                MaxEntries: 5_000,
                MaxDepth: 1,
                MaxGitIgnoreFileSizeInBytes: 262_144,
                MaxGitIgnoreRulesPerFile: 2_048));

        Assert.Contains(map.Entries, entry => entry.RelativePath == "level-1");
        Assert.Contains(map.Entries, entry => entry.RelativePath == "level-1/keep.txt");
        Assert.DoesNotContain(map.Entries, entry => entry.RelativePath == "level-1/level-2");
        Assert.DoesNotContain(map.Entries, entry => entry.RelativePath == "level-1/level-2/too-deep.txt");
        Assert.True(map.IsTruncated);
        Assert.Equal(WorkspaceMapLimitKind.MaxDepth, map.LimitKind);
    }

    [Fact]
    public void Map_AlwaysIgnoresGitDirectory()
    {
        var root = CreateTemporaryDirectory();

        CreateFile(root, ".git/config");
        CreateFile(root, "src/app.cs");

        var map = WorkspaceFileStructureMapper.Map(root);

        Assert.Contains(map.Entries, entry => entry.RelativePath == "src/app.cs");
        Assert.DoesNotContain(map.Entries, entry => entry.RelativePath == ".git");
        Assert.DoesNotContain(map.Entries, entry => entry.RelativePath == ".git/config");
    }

    [Fact]
    public void Map_WhenRootDirectoryIsMissing_Throws()
    {
        var missingDirectory = Path.Combine(
            Path.GetTempPath(),
            "asxrun-workspace-structure-map-tests",
            Guid.NewGuid().ToString("N"),
            "missing");

        var exception = Assert.Throws<DirectoryNotFoundException>(
            () => WorkspaceFileStructureMapper.Map(missingDirectory));

        Assert.Contains("nao existe", exception.Message, StringComparison.OrdinalIgnoreCase);
    }

    private static string CreateTemporaryDirectory()
    {
        var directoryPath = Path.Combine(
            Path.GetTempPath(),
            "asxrun-workspace-structure-map-tests",
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
