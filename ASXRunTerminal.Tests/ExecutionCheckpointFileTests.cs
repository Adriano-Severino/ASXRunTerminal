using ASXRunTerminal.Config;
using ASXRunTerminal.Core;

namespace ASXRunTerminal.Tests;

public sealed class ExecutionCheckpointFileTests
{
    [Fact]
    public void EnsureExists_CreatesCheckpointFileInsideUserHome()
    {
        var testRoot = BuildTestRoot();
        var userHome = Path.Combine(testRoot, "home");

        try
        {
            var checkpointPath = ExecutionCheckpointFile.EnsureExists(() => userHome);

            Assert.Equal(
                Path.Combine(userHome, UserConfigFile.ConfigDirectoryName, ExecutionCheckpointFile.CheckpointFileName),
                checkpointPath);
            Assert.True(File.Exists(checkpointPath));
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
        var oldest = CreateCheckpoint(
            sessionId: "sessao-a",
            command: "ask",
            stage: "processing",
            status: ExecutionCheckpointStatus.InProgress,
            prompt: "gerar testes",
            model: "qwen3.5:4b",
            skillName: null,
            timestampUtc: new DateTimeOffset(2026, 4, 20, 10, 0, 0, TimeSpan.Zero));
        var newest = CreateCheckpoint(
            sessionId: "sessao-a",
            command: "ask",
            stage: "error",
            status: ExecutionCheckpointStatus.Failed,
            prompt: "gerar testes",
            model: "qwen3.5:4b",
            skillName: null,
            timestampUtc: new DateTimeOffset(2026, 4, 20, 10, 0, 2, TimeSpan.Zero));

        try
        {
            ExecutionCheckpointFile.Append(oldest, () => userHome);
            ExecutionCheckpointFile.Append(newest, () => userHome);

            var loaded = ExecutionCheckpointFile.Load(() => userHome);

            Assert.Equal(2, loaded.Count);
            Assert.Equal(newest.TimestampUtc, loaded[0].TimestampUtc);
            Assert.Equal(newest.SessionId, loaded[0].SessionId);
            Assert.Equal(newest.Command, loaded[0].Command);
            Assert.Equal(newest.Stage, loaded[0].Stage);
            Assert.Equal(newest.Status, loaded[0].Status);
            Assert.Equal(newest.Prompt, loaded[0].Prompt);
            Assert.Equal(newest.Model, loaded[0].Model);
            Assert.Equal(newest.SkillName, loaded[0].SkillName);
            Assert.Equal(newest.Detail, loaded[0].Detail);

            Assert.Equal(oldest.TimestampUtc, loaded[1].TimestampUtc);
            Assert.Equal(oldest.Status, loaded[1].Status);
        }
        finally
        {
            DeleteTestRoot(testRoot);
        }
    }

    [Fact]
    public void Load_WhenAnyLineContainsInvalidStatus_ThrowsInvalidOperationException()
    {
        var testRoot = BuildTestRoot();
        var userHome = Path.Combine(testRoot, "home");
        var checkpointDirectory = Path.Combine(userHome, UserConfigFile.ConfigDirectoryName);
        var checkpointPath = Path.Combine(checkpointDirectory, ExecutionCheckpointFile.CheckpointFileName);

        try
        {
            Directory.CreateDirectory(checkpointDirectory);
            File.WriteAllText(
                checkpointPath,
                """
                {"TimestampUtc":"2026-04-20T10:00:00+00:00","SessionId":"sessao-a","Command":"ask","Stage":"error","Status":"unknown","Prompt":"gerar testes","Model":"qwen3.5:4b","SkillName":null,"Detail":"falha"}
                """);

            var exception = Assert.Throws<InvalidOperationException>(
                () => ExecutionCheckpointFile.Load(() => userHome));

            Assert.Contains("Linha de checkpoint invalida", exception.Message);
        }
        finally
        {
            DeleteTestRoot(testRoot);
        }
    }

    [Fact]
    public void Clear_WhenCheckpointHasEntries_RemovesAllEntries()
    {
        var testRoot = BuildTestRoot();
        var userHome = Path.Combine(testRoot, "home");
        var checkpoint = CreateCheckpoint(
            sessionId: "sessao-b",
            command: "skill",
            stage: "processing",
            status: ExecutionCheckpointStatus.InProgress,
            prompt: "revisar controller",
            model: "qwen3.5:4b",
            skillName: "code-review",
            timestampUtc: new DateTimeOffset(2026, 4, 20, 11, 0, 0, TimeSpan.Zero));

        try
        {
            ExecutionCheckpointFile.Append(checkpoint, () => userHome);
            ExecutionCheckpointFile.Clear(() => userHome);

            var loaded = ExecutionCheckpointFile.Load(() => userHome);
            var checkpointPath = ExecutionCheckpointFile.GetCheckpointPath(() => userHome);

            Assert.Empty(loaded);
            Assert.True(File.Exists(checkpointPath));
            Assert.Equal(string.Empty, File.ReadAllText(checkpointPath));
        }
        finally
        {
            DeleteTestRoot(testRoot);
        }
    }

    private static ExecutionSessionCheckpoint CreateCheckpoint(
        string sessionId,
        string command,
        string stage,
        ExecutionCheckpointStatus status,
        string prompt,
        string? model,
        string? skillName,
        DateTimeOffset timestampUtc)
    {
        return new ExecutionSessionCheckpoint(
            TimestampUtc: timestampUtc,
            SessionId: sessionId,
            Command: command,
            Stage: stage,
            Status: status,
            Prompt: prompt,
            Model: model,
            SkillName: skillName,
            Detail: "detalhe");
    }

    private static string BuildTestRoot()
    {
        return Path.Combine(
            Path.GetTempPath(),
            "asxrun-checkpoint-tests",
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
