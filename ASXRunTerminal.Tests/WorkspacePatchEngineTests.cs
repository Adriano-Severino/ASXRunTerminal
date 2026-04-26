using ASXRunTerminal.Core;

namespace ASXRunTerminal.Tests;

public sealed class WorkspacePatchEngineTests
{
    [Fact]
    public void WorkspacePatchRequest_ImplicitOperators_MapTupleArrayToRequest()
    {
        WorkspacePatchChange[] changes =
        [
            (WorkspacePatchChangeKind.Edit, "src/appsettings.json", "{\"name\":\"novo\"}", "{\"name\":\"antigo\"}")
        ];

        WorkspacePatchRequest request = changes;

        Assert.False(request.PreviewOnly);
        Assert.Single(request.Changes);
        Assert.Equal(WorkspacePatchChangeKind.Edit, request.Changes[0].Kind);
        Assert.Equal("src/appsettings.json", request.Changes[0].Path);
        Assert.Equal("{\"name\":\"novo\"}", request.Changes[0].Content);
        Assert.Equal("{\"name\":\"antigo\"}", request.Changes[0].ExpectedContent);
    }

    [Fact]
    public void Apply_WhenPreviewOnly_DoesNotPersistEdits_AndReturnsUnifiedDiff()
    {
        var root = CreateTemporaryDirectory();
        CreateFile(root, "src/program.cs", "linha 1\nlinha 2");
        var engine = new WorkspacePatchEngine(root);

        WorkspacePatchRequest request = new WorkspacePatchRequest(
            Changes:
            [
                (WorkspacePatchChangeKind.Edit, "src/program.cs", "linha 1\nlinha 2 atualizada")
            ],
            PreviewOnly: true);

        var result = engine.Apply(request);

        Assert.True(result.IsPreviewOnly);
        Assert.True(result.HasChanges);
        Assert.Equal(1, result.PlannedChangeCount);
        Assert.Equal(0, result.AppliedChangeCount);
        Assert.Equal(0, result.SkippedChangeCount);
        Assert.Equal("linha 1\nlinha 2", File.ReadAllText(Path.Combine(root, "src", "program.cs")));
        Assert.Contains("--- a/src/program.cs", result.UnifiedDiff, StringComparison.Ordinal);
        Assert.Contains("+++ b/src/program.cs", result.UnifiedDiff, StringComparison.Ordinal);
        Assert.Contains("-linha 2", result.UnifiedDiff, StringComparison.Ordinal);
        Assert.Contains("+linha 2 atualizada", result.UnifiedDiff, StringComparison.Ordinal);
    }

    [Fact]
    public void Apply_WhenNotPreviewOnly_AppliesCreateEditAndDelete()
    {
        var root = CreateTemporaryDirectory();
        CreateFile(root, "src/edit.txt", "versao antiga");
        CreateFile(root, "src/delete.txt", "remover");
        var engine = new WorkspacePatchEngine(root);

        WorkspacePatchChange[] changes =
        [
            (WorkspacePatchChangeKind.Edit, "src/edit.txt", "versao nova"),
            (WorkspacePatchChangeKind.Create, "src/create.txt", "arquivo criado"),
            (WorkspacePatchChangeKind.Delete, "src/delete.txt", null)
        ];
        WorkspacePatchRequest request = changes;

        var result = engine.Apply(request);

        Assert.False(result.IsPreviewOnly);
        Assert.Equal(3, result.PlannedChangeCount);
        Assert.Equal(3, result.AppliedChangeCount);
        Assert.Equal(0, result.SkippedChangeCount);
        Assert.Equal("versao nova", File.ReadAllText(Path.Combine(root, "src", "edit.txt")));
        Assert.Equal("arquivo criado", File.ReadAllText(Path.Combine(root, "src", "create.txt")));
        Assert.False(File.Exists(Path.Combine(root, "src", "delete.txt")));
    }

    [Fact]
    public void Apply_WhenEditDoesNotChangeContent_ReturnsSkippedNoOp()
    {
        var root = CreateTemporaryDirectory();
        CreateFile(root, "src/noop.txt", "conteudo estavel");
        var engine = new WorkspacePatchEngine(root);

        WorkspacePatchRequest request = new WorkspacePatchRequest(
            Changes:
            [
                (WorkspacePatchChangeKind.Edit, "src/noop.txt", "conteudo estavel")
            ],
            PreviewOnly: false);

        var result = engine.Apply(request);

        var fileResult = Assert.Single(result.Files);
        Assert.False(result.HasChanges);
        Assert.False(fileResult.HasChanges);
        Assert.Equal(0, result.PlannedChangeCount);
        Assert.Equal(0, result.AppliedChangeCount);
        Assert.Equal(1, result.SkippedChangeCount);
        Assert.Equal(string.Empty, fileResult.UnifiedDiff);
        Assert.Equal("conteudo estavel", File.ReadAllText(Path.Combine(root, "src", "noop.txt")));
    }

    [Fact]
    public void Apply_WhenRequestContainsDuplicatePaths_Throws()
    {
        var root = CreateTemporaryDirectory();
        CreateFile(root, "src/duplicado.txt", "conteudo");
        var engine = new WorkspacePatchEngine(root);

        WorkspacePatchRequest request = new WorkspacePatchRequest(
            Changes:
            [
                (WorkspacePatchChangeKind.Edit, "src/duplicado.txt", "novo"),
                (WorkspacePatchChangeKind.Delete, "src/duplicado.txt", null)
            ]);

        var exception = Assert.Throws<InvalidOperationException>(
            () => engine.Apply(request));

        Assert.Contains("multiplas mudancas", exception.Message, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public void Apply_WhenRequestContainsDuplicateCanonicalPath_Throws()
    {
        var root = CreateTemporaryDirectory();
        CreateFile(root, "src/canonical.txt", "conteudo");
        var engine = new WorkspacePatchEngine(root);

        WorkspacePatchRequest request = new WorkspacePatchRequest(
            Changes:
            [
                (WorkspacePatchChangeKind.Edit, "src/canonical.txt", "novo conteudo"),
                (WorkspacePatchChangeKind.Delete, "src/../src/canonical.txt", null)
            ]);

        var exception = Assert.Throws<InvalidOperationException>(
            () => engine.Apply(request));

        Assert.Contains("multiplas mudancas", exception.Message, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public void Apply_WhenExpectedContentDoesNotMatch_Throws()
    {
        var root = CreateTemporaryDirectory();
        CreateFile(root, "src/expected.txt", "estado atual");
        var engine = new WorkspacePatchEngine(root);

        WorkspacePatchRequest request = new WorkspacePatchRequest(
            Changes:
            [
                (WorkspacePatchChangeKind.Edit, "src/expected.txt", "novo estado", "estado esperado antigo")
            ]);

        var exception = Assert.Throws<InvalidOperationException>(
            () => engine.Apply(request));

        Assert.Contains("diverge", exception.Message, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public void Apply_WhenPathIsOutsideWorkspace_ThrowsUnauthorizedAccessException()
    {
        var root = CreateTemporaryDirectory();
        var engine = new WorkspacePatchEngine(root);
        var outsidePath = Path.Combine(
            Path.GetTempPath(),
            $"asxrun-outside-{Guid.NewGuid():N}",
            "outside.txt");

        WorkspacePatchRequest request = new WorkspacePatchRequest(
            Changes:
            [
                (WorkspacePatchChangeKind.Create, outsidePath, "nao permitido")
            ]);

        var exception = Assert.Throws<UnauthorizedAccessException>(
            () => engine.Apply(request));

        Assert.Contains("fora da raiz do workspace", exception.Message, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public void Apply_WhenPermissionPolicyDeniesEdit_ThrowsUnauthorizedAccessException()
    {
        var root = CreateTemporaryDirectory();
        CreateFile(root, "src/protected.txt", "conteudo protegido");
        var permissionPolicy = new WorkspaceFilePermissionPolicy(
            rules: new Dictionary<WorkspaceFilePermissionOperation, WorkspaceFilePermissionRule>
            {
                [WorkspaceFilePermissionOperation.Edit] = new WorkspaceFilePermissionRule(
                    AllowPatterns: [],
                    DenyPatterns: ["src/protected.txt"])
            });
        var engine = new WorkspacePatchEngine(root, permissionPolicy);

        WorkspacePatchRequest request = new WorkspacePatchRequest(
            Changes:
            [
                (WorkspacePatchChangeKind.Edit, "src/protected.txt", "conteudo alterado")
            ]);

        var exception = Assert.Throws<UnauthorizedAccessException>(
            () => engine.Apply(request));

        Assert.Contains("nao e permitida", exception.Message, StringComparison.OrdinalIgnoreCase);
        Assert.Equal("conteudo protegido", File.ReadAllText(Path.Combine(root, "src", "protected.txt")));
    }

    [Fact]
    public void Apply_DeleteInPreview_GeneratesDiffWithDevNull_WithoutDeletingFile()
    {
        var root = CreateTemporaryDirectory();
        CreateFile(root, "src/to-delete.txt", "linha unica");
        var engine = new WorkspacePatchEngine(root);

        WorkspacePatchRequest request = new WorkspacePatchRequest(
            Changes:
            [
                (WorkspacePatchChangeKind.Delete, "src/to-delete.txt", null)
            ],
            PreviewOnly: true);

        var result = engine.Apply(request);

        Assert.True(result.IsPreviewOnly);
        Assert.Equal(1, result.PlannedChangeCount);
        Assert.Equal(0, result.AppliedChangeCount);
        Assert.True(File.Exists(Path.Combine(root, "src", "to-delete.txt")));
        Assert.Contains("--- a/src/to-delete.txt", result.UnifiedDiff, StringComparison.Ordinal);
        Assert.Contains("+++ /dev/null", result.UnifiedDiff, StringComparison.Ordinal);
    }

    [Fact]
    public void Apply_WhenEditDiffExceedsLcsLimit_UsesCoarseDiffFallback()
    {
        var root = CreateTemporaryDirectory();
        var previousContent = BuildSequentialLines("before", 2000);
        var nextContent = BuildSequentialLines("after", 2000);
        CreateFile(root, "src/large-diff.txt", previousContent);
        var engine = new WorkspacePatchEngine(root);

        WorkspacePatchRequest request = new WorkspacePatchRequest(
            Changes:
            [
                (WorkspacePatchChangeKind.Edit, "src/large-diff.txt", nextContent)
            ],
            PreviewOnly: true);

        var result = engine.Apply(request);

        var fileResult = Assert.Single(result.Files);
        Assert.True(fileResult.HasChanges);
        Assert.Contains("@@ -1,2000 +1,2000 @@", fileResult.UnifiedDiff, StringComparison.Ordinal);
        Assert.Contains("-before-0001", fileResult.UnifiedDiff, StringComparison.Ordinal);
        Assert.Contains("+after-0001", fileResult.UnifiedDiff, StringComparison.Ordinal);
        Assert.DoesNotContain(" before-", fileResult.UnifiedDiff, StringComparison.Ordinal);
    }

    [Fact]
    public void Apply_WhenRequestMixesChangedAndNoOpEdits_AggregatesDiffForChangedFileOnly()
    {
        var root = CreateTemporaryDirectory();
        CreateFile(root, "src/changed.txt", "linha 1\nlinha 2");
        CreateFile(root, "src/noop.txt", "conteudo estavel");
        var engine = new WorkspacePatchEngine(root);

        WorkspacePatchRequest request = new WorkspacePatchRequest(
            Changes:
            [
                (WorkspacePatchChangeKind.Edit, "src/changed.txt", "linha 1\nlinha 2 alterada"),
                (WorkspacePatchChangeKind.Edit, "src/noop.txt", "conteudo estavel")
            ]);

        var result = engine.Apply(request);

        Assert.Equal(1, result.PlannedChangeCount);
        Assert.Equal(1, result.AppliedChangeCount);
        Assert.Equal(1, result.SkippedChangeCount);
        Assert.Equal(2, result.Files.Count);

        var changedFile = result.Files[0];
        var noOpFile = result.Files[1];
        Assert.Equal("src/changed.txt", changedFile.Path);
        Assert.True(changedFile.HasChanges);
        Assert.Equal("src/noop.txt", noOpFile.Path);
        Assert.False(noOpFile.HasChanges);

        Assert.Contains("src/changed.txt", result.UnifiedDiff, StringComparison.Ordinal);
        Assert.DoesNotContain("src/noop.txt", result.UnifiedDiff, StringComparison.Ordinal);
    }

    private static string CreateTemporaryDirectory()
    {
        var directoryPath = Path.Combine(
            Path.GetTempPath(),
            "asxrun-workspace-patch-engine-tests",
            Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(directoryPath);
        return directoryPath;
    }

    private static string CreateFile(
        string rootDirectory,
        string relativePath,
        string content)
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

    private static string BuildSequentialLines(string prefix, int lineCount)
    {
        return string.Join(
            '\n',
            Enumerable.Range(1, lineCount)
                .Select(lineNumber => $"{prefix}-{lineNumber:0000}"));
    }
}
