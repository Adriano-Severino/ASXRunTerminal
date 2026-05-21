using ASXRunTerminal.Core;

namespace ASXRunTerminal.Tests;

public sealed class AgentStableStateStoreTests
{
    [Fact]
    public void Restore_WhenWorkspaceChanged_RevertsToCapturedState()
    {
        var root = CreateTemporaryDirectory();
        var store = new AgentStableStateStore();

        try
        {
            CreateFile(root, ".gitignore", "ignored/");
            CreateFile(root, "src/Feature.cs", "stable");
            CreateFile(root, "src/Deleted.cs", "keep");

            var snapshot = store.Capture(root);

            CreateFile(root, "src/Feature.cs", "degraded");
            File.Delete(Path.Combine(root, "src", "Deleted.cs"));
            CreateFile(root, "src/new/NewFile.cs", "new");
            CreateFile(root, "ignored/generated.txt", "ignored");

            var result = store.Restore(snapshot);

            Assert.True(result.HasChanges);
            Assert.Equal("stable", File.ReadAllText(Path.Combine(root, "src", "Feature.cs")));
            Assert.Equal("keep", File.ReadAllText(Path.Combine(root, "src", "Deleted.cs")));
            Assert.False(File.Exists(Path.Combine(root, "src", "new", "NewFile.cs")));
            Assert.False(Directory.Exists(Path.Combine(root, "src", "new")));
            Assert.True(File.Exists(Path.Combine(root, "ignored", "generated.txt")));
            Assert.Contains("src/Feature.cs", result.ChangedPaths);
            Assert.Contains("src/Deleted.cs", result.ChangedPaths);
            Assert.Contains("src/new/NewFile.cs", result.ChangedPaths);
        }
        finally
        {
            Directory.Delete(root, recursive: true);
        }
    }

    private static void CreateFile(
        string root,
        string relativePath,
        string content)
    {
        var path = Path.Combine(
            root,
            relativePath.Replace('/', Path.DirectorySeparatorChar));
        var directory = Path.GetDirectoryName(path);
        if (!string.IsNullOrWhiteSpace(directory))
        {
            Directory.CreateDirectory(directory);
        }

        File.WriteAllText(path, content);
    }

    private static string CreateTemporaryDirectory()
    {
        var directoryPath = Path.Combine(
            Path.GetTempPath(),
            $"asxrun-stable-state-tests-{Guid.NewGuid():N}");
        Directory.CreateDirectory(directoryPath);
        return directoryPath;
    }
}
