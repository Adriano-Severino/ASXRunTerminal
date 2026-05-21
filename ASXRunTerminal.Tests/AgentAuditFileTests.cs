using ASXRunTerminal.Config;
using ASXRunTerminal.Core;

namespace ASXRunTerminal.Tests;

public sealed class AgentAuditFileTests
{
    [Fact]
    public void EnsureExists_CreatesAuditFileInsideUserHome()
    {
        var testRoot = BuildTestRoot();
        var userHome = Path.Combine(testRoot, "home");

        try
        {
            var auditPath = AgentAuditFile.EnsureExists(() => userHome);

            Assert.Equal(
                Path.Combine(userHome, UserConfigFile.ConfigDirectoryName, AgentAuditFile.AuditFileName),
                auditPath);
            Assert.True(File.Exists(auditPath));
        }
        finally
        {
            DeleteTestRoot(testRoot);
        }
    }

    [Fact]
    public void AppendAndLoad_WhenEntriesExist_ReturnsMostRecentFirstWithEventMetadata()
    {
        var testRoot = BuildTestRoot();
        var userHome = Path.Combine(testRoot, "home");
        var oldest = CreateAuditEntry(
            sessionId: "sessao-agent-a",
            sessionSequence: 1,
            timestampUtc: new DateTimeOffset(2026, 4, 10, 11, 0, 0, TimeSpan.Zero),
            status: "started");
        var newest = CreateAuditEntry(
            sessionId: "sessao-agent-a",
            sessionSequence: 2,
            timestampUtc: new DateTimeOffset(2026, 4, 10, 11, 1, 0, TimeSpan.Zero),
            status: "passed");

        try
        {
            AgentAuditFile.Append(oldest, () => userHome);
            AgentAuditFile.Append(newest, () => userHome);

            var loaded = AgentAuditFile.Load(() => userHome);

            Assert.Equal(2, loaded.Count);
            Assert.Equal(newest.TimestampUtc, loaded[0].TimestampUtc);
            Assert.Equal(newest.SessionId, loaded[0].SessionId);
            Assert.Equal(newest.SessionSequence, loaded[0].SessionSequence);
            Assert.Equal(newest.Command, loaded[0].Command);
            Assert.Equal(newest.WorkspaceRootDirectory, loaded[0].WorkspaceRootDirectory);
            Assert.Equal(newest.Objective, loaded[0].Objective);
            Assert.Equal(newest.Model, loaded[0].Model);
            Assert.Equal(newest.AutonomyLevel, loaded[0].AutonomyLevel);
            Assert.Equal(newest.HasExplicitSensitiveOperationApproval, loaded[0].HasExplicitSensitiveOperationApproval);
            Assert.Equal(newest.Iteration, loaded[0].Iteration);
            Assert.Equal(newest.Phase, loaded[0].Phase);
            Assert.Equal(newest.EventType, loaded[0].EventType);
            Assert.Equal(newest.Status, loaded[0].Status);
            Assert.Equal(newest.Summary, loaded[0].Summary);
            Assert.Equal(newest.Detail, loaded[0].Detail);
            Assert.Equal(newest.ToolName, loaded[0].ToolName);
            Assert.Equal(newest.CommandLine, loaded[0].CommandLine);
            Assert.Equal(newest.ExitCode, loaded[0].ExitCode);
            Assert.Equal(newest.IsSuccess, loaded[0].IsSuccess);
            Assert.Equal(newest.Duration, loaded[0].Duration);
            Assert.False(loaded[0].IsTimedOut);
            Assert.False(loaded[0].IsCancelled);
            Assert.Single(loaded[0].Changes);
            Assert.Equal("src/Program.cs", loaded[0].Changes[0].Path);
            Assert.Equal("edit", loaded[0].Changes[0].Kind);
            Assert.False(loaded[0].Changes[0].IsDestructive);

            Assert.Equal(oldest.TimestampUtc, loaded[1].TimestampUtc);
            Assert.Equal(oldest.SessionSequence, loaded[1].SessionSequence);
        }
        finally
        {
            DeleteTestRoot(testRoot);
        }
    }

    [Fact]
    public void Load_WhenAnyLineHasInvalidEventMetadata_ThrowsInvalidOperationException()
    {
        var testRoot = BuildTestRoot();
        var userHome = Path.Combine(testRoot, "home");
        var auditDirectory = Path.Combine(userHome, UserConfigFile.ConfigDirectoryName);
        var auditPath = Path.Combine(auditDirectory, AgentAuditFile.AuditFileName);

        try
        {
            Directory.CreateDirectory(auditDirectory);
            File.WriteAllText(
                auditPath,
                """
                {"TimestampUtc":"2026-04-10T11:00:00+00:00","SessionId":"sessao-agent-a","SessionSequence":1,"Command":"agent","WorkspaceRootDirectory":"C:/workspace","Objective":"corrigir teste","Model":null,"AutonomyLevel":"autonomo","HasExplicitSensitiveOperationApproval":false,"Iteration":1,"Phase":"","EventType":"decision","Status":"done","Summary":"Verify concluido.","Detail":"","ToolName":null,"CommandLine":null,"ExitCode":null,"IsSuccess":null,"DurationMilliseconds":null,"IsTimedOut":false,"IsCancelled":false,"Changes":[]}
                """);

            var exception = Assert.Throws<InvalidOperationException>(
                () => AgentAuditFile.Load(() => userHome));

            Assert.Contains("Linha de auditoria do agente invalida", exception.Message);
        }
        finally
        {
            DeleteTestRoot(testRoot);
        }
    }

    [Fact]
    public void Clear_WhenAuditHasEntries_RemovesAllEntries()
    {
        var testRoot = BuildTestRoot();
        var userHome = Path.Combine(testRoot, "home");
        var entry = CreateAuditEntry(
            sessionId: "sessao-agent-b",
            sessionSequence: 1,
            timestampUtc: new DateTimeOffset(2026, 4, 10, 12, 0, 0, TimeSpan.Zero),
            status: "started");

        try
        {
            AgentAuditFile.Append(entry, () => userHome);
            AgentAuditFile.Clear(() => userHome);

            var loaded = AgentAuditFile.Load(() => userHome);
            var auditPath = AgentAuditFile.GetAuditPath(() => userHome);

            Assert.Empty(loaded);
            Assert.True(File.Exists(auditPath));
            Assert.Equal(string.Empty, File.ReadAllText(auditPath));
        }
        finally
        {
            DeleteTestRoot(testRoot);
        }
    }

    private static AgentAuditEntry CreateAuditEntry(
        string sessionId,
        long sessionSequence,
        DateTimeOffset timestampUtc,
        string status)
    {
        return new AgentAuditEntry(
            TimestampUtc: timestampUtc,
            SessionId: sessionId,
            SessionSequence: sessionSequence,
            Command: "agent",
            WorkspaceRootDirectory: "C:/workspace",
            Objective: "corrigir teste",
            Model: "qwen2.5-coder:7b",
            AutonomyLevel: "autonomo",
            HasExplicitSensitiveOperationApproval: false,
            Iteration: 1,
            Phase: "validation",
            EventType: "command",
            Status: status,
            Summary: "Comando de validacao executado.",
            Detail: "stdout=ok",
            ToolName: "shell",
            CommandLine: "dotnet test ASXRunTerminal.slnx --nologo",
            ExitCode: 0,
            IsSuccess: true,
            Duration: TimeSpan.FromMilliseconds(25),
            IsTimedOut: false,
            IsCancelled: false,
            Changes:
            [
                new AgentAuditChangeEntry(
                    Path: "src/Program.cs",
                    Kind: "edit",
                    IsDestructive: false)
            ]);
    }

    private static string BuildTestRoot()
    {
        return Path.Combine(
            Path.GetTempPath(),
            "asxrun-agent-audit-tests",
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
