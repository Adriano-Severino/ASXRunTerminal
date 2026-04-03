using ASXRunTerminal.Config;
using ASXRunTerminal.Core;

namespace ASXRunTerminal.Tests;

public sealed class UserHistoryFileTests
{
    [Fact]
    public void EnsureExists_CreatesHistoryFileInsideUserHome()
    {
        var testRoot = BuildTestRoot();
        var userHome = Path.Combine(testRoot, "home");

        try
        {
            var historyPath = UserHistoryFile.EnsureExists(() => userHome);

            Assert.Equal(
                Path.Combine(userHome, UserConfigFile.ConfigDirectoryName, UserHistoryFile.HistoryFileName),
                historyPath);
            Assert.True(File.Exists(historyPath));
        }
        finally
        {
            DeleteTestRoot(testRoot);
        }
    }

    [Fact]
    public void AppendAndLoad_WhenEntriesExist_ReturnsMostRecentFirst()
    {
        var testRoot = BuildTestRoot();
        var userHome = Path.Combine(testRoot, "home");
        var oldest = new PromptHistoryEntry(
            TimestampUtc: new DateTimeOffset(2026, 3, 27, 12, 0, 0, TimeSpan.Zero),
            Command: "ask",
            Prompt: "primeiro prompt",
            Response: "primeira resposta",
            Model: "qwen3.5:4b");
        var newest = new PromptHistoryEntry(
            TimestampUtc: new DateTimeOffset(2026, 3, 28, 13, 30, 0, TimeSpan.Zero),
            Command: "chat",
            Prompt: "segundo prompt",
            Response: "segunda resposta",
            Model: null);

        try
        {
            UserHistoryFile.Append(oldest, () => userHome);
            UserHistoryFile.Append(newest, () => userHome);

            var loaded = UserHistoryFile.Load(() => userHome);

            Assert.Equal(2, loaded.Count);
            Assert.Equal(newest, loaded[0]);
            Assert.Equal(oldest, loaded[1]);
        }
        finally
        {
            DeleteTestRoot(testRoot);
        }
    }

    [Fact]
    public void Load_WhenAnyLineIsInvalid_ThrowsInvalidOperationException()
    {
        var testRoot = BuildTestRoot();
        var userHome = Path.Combine(testRoot, "home");
        var historyDirectory = Path.Combine(userHome, UserConfigFile.ConfigDirectoryName);
        var historyPath = Path.Combine(historyDirectory, UserHistoryFile.HistoryFileName);

        try
        {
            Directory.CreateDirectory(historyDirectory);
            File.WriteAllText(
                historyPath,
                """
                {"TimestampUtc":"2026-03-28T10:00:00+00:00","Command":"ask","Prompt":"ok","Response":"ok","Model":"qwen3.5:4b"}
                nao-e-json
                """);

            var exception = Assert.Throws<InvalidOperationException>(
                () => UserHistoryFile.Load(() => userHome));

            Assert.Contains("Linha de historico invalida", exception.Message);
        }
        finally
        {
            DeleteTestRoot(testRoot);
        }
    }

    [Fact]
    public void Clear_WhenHistoryHasEntries_RemovesAllEntries()
    {
        var testRoot = BuildTestRoot();
        var userHome = Path.Combine(testRoot, "home");
        var entry = new PromptHistoryEntry(
            TimestampUtc: new DateTimeOffset(2026, 3, 28, 15, 0, 0, TimeSpan.Zero),
            Command: "ask",
            Prompt: "limpar historico",
            Response: "ok",
            Model: "qwen3.5:4b");

        try
        {
            UserHistoryFile.Append(entry, () => userHome);
            UserHistoryFile.Clear(() => userHome);

            var loaded = UserHistoryFile.Load(() => userHome);
            var historyPath = UserHistoryFile.GetHistoryPath(() => userHome);

            Assert.Empty(loaded);
            Assert.True(File.Exists(historyPath));
            Assert.Equal(string.Empty, File.ReadAllText(historyPath));
        }
        finally
        {
            DeleteTestRoot(testRoot);
        }
    }

    private static string BuildTestRoot()
    {
        return Path.Combine(
            Path.GetTempPath(),
            "asxrun-history-tests",
            Guid.NewGuid().ToString("N"));
    }

    private static void DeleteTestRoot(string testRoot)
    {
        if (Directory.Exists(testRoot))
        {
            Directory.Delete(testRoot, recursive: true);
        }
    }
}
