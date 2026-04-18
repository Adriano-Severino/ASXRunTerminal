using ASXRunTerminal.Config;
using ASXRunTerminal.Core;

namespace ASXRunTerminal.Tests;

public sealed class WorkspacePatchAuditFileTests
{
    [Fact]
    public void EnsureExists_CreatesAuditFileInsideUserHome()
    {
        var testRoot = BuildTestRoot();
        var userHome = Path.Combine(testRoot, "home");

        try
        {
            var auditPath = WorkspacePatchAuditFile.EnsureExists(() => userHome);

            Assert.Equal(
                Path.Combine(userHome, UserConfigFile.ConfigDirectoryName, WorkspacePatchAuditFile.AuditFileName),
                auditPath);
            Assert.True(File.Exists(auditPath));
        }
        finally
        {
            DeleteTestRoot(testRoot);
        }
    }

    [Fact]
    public void AppendAndLoad_WhenEntriesExist_ReturnsMostRecentFirstWithSessionMetadata()
    {
        var testRoot = BuildTestRoot();
        var userHome = Path.Combine(testRoot, "home");
        var oldest = CreateAuditEntry(
            sessionId: "sessao-a",
            sessionSequence: 1,
            timestampUtc: new DateTimeOffset(2026, 4, 10, 11, 0, 0, TimeSpan.Zero),
            isPreviewOnly: true);
        var newest = CreateAuditEntry(
            sessionId: "sessao-a",
            sessionSequence: 2,
            timestampUtc: new DateTimeOffset(2026, 4, 10, 11, 1, 0, TimeSpan.Zero),
            isPreviewOnly: false);

        try
        {
            WorkspacePatchAuditFile.Append(oldest, () => userHome);
            WorkspacePatchAuditFile.Append(newest, () => userHome);

            var loaded = WorkspacePatchAuditFile.Load(() => userHome);

            Assert.Equal(2, loaded.Count);
            Assert.Equal(newest.TimestampUtc, loaded[0].TimestampUtc);
            Assert.Equal(newest.SessionId, loaded[0].SessionId);
            Assert.Equal(newest.SessionSequence, loaded[0].SessionSequence);
            Assert.Equal(newest.Command, loaded[0].Command);
            Assert.Equal(newest.WorkspaceRootDirectory, loaded[0].WorkspaceRootDirectory);
            Assert.Equal(newest.PatchRequestFilePath, loaded[0].PatchRequestFilePath);
            Assert.Equal(newest.IsPreviewOnly, loaded[0].IsPreviewOnly);
            Assert.Equal(newest.PlannedChangeCount, loaded[0].PlannedChangeCount);
            Assert.Equal(newest.AppliedChangeCount, loaded[0].AppliedChangeCount);
            Assert.Equal(newest.SkippedChangeCount, loaded[0].SkippedChangeCount);
            Assert.Equal(newest.UnifiedDiff, loaded[0].UnifiedDiff);
            Assert.Single(loaded[0].Files);
            Assert.Equal(WorkspacePatchChangeKind.Edit, loaded[0].Files[0].Kind);
            Assert.Equal("src/Program.cs", loaded[0].Files[0].Path);
            Assert.Equal("C:/workspace/src/Program.cs", loaded[0].Files[0].ResolvedPath);
            Assert.True(loaded[0].Files[0].HasChanges);

            Assert.Equal(oldest.TimestampUtc, loaded[1].TimestampUtc);
            Assert.Equal(oldest.SessionId, loaded[1].SessionId);
            Assert.Equal(oldest.SessionSequence, loaded[1].SessionSequence);
        }
        finally
        {
            DeleteTestRoot(testRoot);
        }
    }

    [Fact]
    public void Load_WhenAnyLineContainsInvalidKind_ThrowsInvalidOperationException()
    {
        var testRoot = BuildTestRoot();
        var userHome = Path.Combine(testRoot, "home");
        var auditDirectory = Path.Combine(userHome, UserConfigFile.ConfigDirectoryName);
        var auditPath = Path.Combine(auditDirectory, WorkspacePatchAuditFile.AuditFileName);

        try
        {
            Directory.CreateDirectory(auditDirectory);
            File.WriteAllText(
                auditPath,
                """
                {"TimestampUtc":"2026-04-10T11:00:00+00:00","SessionId":"sessao-a","SessionSequence":1,"Command":"patch","WorkspaceRootDirectory":"C:/workspace","PatchRequestFilePath":"C:/workspace/patch.json","IsPreviewOnly":false,"PlannedChangeCount":1,"AppliedChangeCount":1,"SkippedChangeCount":0,"UnifiedDiff":"--- a/src/Program.cs\n+++ b/src/Program.cs\n@@ -1,1 +1,1 @@\n-linha 1\n+linha 2","Files":[{"Kind":"rename","Path":"src/Program.cs","ResolvedPath":"C:/workspace/src/Program.cs","HasChanges":true,"UnifiedDiff":"--- a/src/Program.cs\n+++ b/src/Program.cs\n@@ -1,1 +1,1 @@\n-linha 1\n+linha 2"}]}
                """);

            var exception = Assert.Throws<InvalidOperationException>(
                () => WorkspacePatchAuditFile.Load(() => userHome));

            Assert.Contains("Linha de auditoria invalida", exception.Message);
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
            sessionId: "sessao-b",
            sessionSequence: 1,
            timestampUtc: new DateTimeOffset(2026, 4, 10, 12, 0, 0, TimeSpan.Zero),
            isPreviewOnly: false);

        try
        {
            WorkspacePatchAuditFile.Append(entry, () => userHome);
            WorkspacePatchAuditFile.Clear(() => userHome);

            var loaded = WorkspacePatchAuditFile.Load(() => userHome);
            var auditPath = WorkspacePatchAuditFile.GetAuditPath(() => userHome);

            Assert.Empty(loaded);
            Assert.True(File.Exists(auditPath));
            Assert.Equal(string.Empty, File.ReadAllText(auditPath));
        }
        finally
        {
            DeleteTestRoot(testRoot);
        }
    }

    private static WorkspacePatchAuditEntry CreateAuditEntry(
        string sessionId,
        long sessionSequence,
        DateTimeOffset timestampUtc,
        bool isPreviewOnly)
    {
        const string unifiedDiff =
            """
            --- a/src/Program.cs
            +++ b/src/Program.cs
            @@ -1,1 +1,1 @@
            -linha 1
            +linha 2
            """;

        return new WorkspacePatchAuditEntry(
            TimestampUtc: timestampUtc,
            SessionId: sessionId,
            SessionSequence: sessionSequence,
            Command: "patch",
            WorkspaceRootDirectory: "C:/workspace",
            PatchRequestFilePath: "C:/workspace/patch.json",
            IsPreviewOnly: isPreviewOnly,
            PlannedChangeCount: 1,
            AppliedChangeCount: isPreviewOnly ? 0 : 1,
            SkippedChangeCount: 0,
            UnifiedDiff: unifiedDiff,
            Files:
            [
                new WorkspacePatchAuditChangeEntry(
                    Kind: WorkspacePatchChangeKind.Edit,
                    Path: "src/Program.cs",
                    ResolvedPath: "C:/workspace/src/Program.cs",
                    HasChanges: true,
                    UnifiedDiff: unifiedDiff)
            ]);
    }

    private static string BuildTestRoot()
    {
        return Path.Combine(
            Path.GetTempPath(),
            "asxrun-patch-audit-tests",
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
