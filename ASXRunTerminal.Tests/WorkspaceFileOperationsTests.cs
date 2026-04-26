using ASXRunTerminal.Core;

namespace ASXRunTerminal.Tests;

public sealed class WorkspaceFileOperationsTests
{
    [Fact]
    public void Constructor_WhenWorkspaceRootIsMissing_Throws()
    {
        var missingWorkspaceRoot = Path.Combine(
            Path.GetTempPath(),
            "asxrun-workspace-file-operations-tests",
            Guid.NewGuid().ToString("N"),
            "missing");

        var exception = Assert.Throws<DirectoryNotFoundException>(
            () => new WorkspaceFileOperations(missingWorkspaceRoot));

        Assert.Contains("nao existe", exception.Message, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public void Read_WhenFileExists_ReturnsContent()
    {
        var root = CreateTemporaryDirectory();
        CreateFile(root, "docs/readme.md", "conteudo inicial");
        var operations = new WorkspaceFileOperations(root);

        var content = operations.Read("docs/readme.md");

        Assert.Equal("conteudo inicial", content);
    }

    [Fact]
    public void Read_WhenPathIsOutsideWorkspace_ThrowsUnauthorizedAccessException()
    {
        var root = CreateTemporaryDirectory();
        var operations = new WorkspaceFileOperations(root);
        var outsidePath = Path.Combine(
            Path.GetTempPath(),
            $"asxrun-outside-{Guid.NewGuid():N}",
            "outside.txt");

        var exception = Assert.Throws<UnauthorizedAccessException>(
            () => operations.Read(outsidePath));

        Assert.Contains("fora da raiz do workspace", exception.Message, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public void Create_WhenFileDoesNotExist_CreatesFile()
    {
        var root = CreateTemporaryDirectory();
        var operations = new WorkspaceFileOperations(root);

        operations.Create("src/new-file.txt", "novo conteudo");

        var createdFilePath = Path.Combine(root, "src", "new-file.txt");
        Assert.True(File.Exists(createdFilePath));
        Assert.Equal("novo conteudo", File.ReadAllText(createdFilePath));
    }

    [Fact]
    public void Create_WhenPathLooksLikeDirectory_Throws()
    {
        var root = CreateTemporaryDirectory();
        var operations = new WorkspaceFileOperations(root);

        var exception = Assert.Throws<InvalidOperationException>(
            () => operations.Create("src/", "nao deve criar"));

        Assert.Contains("arquivo", exception.Message, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public void Create_WhenPermissionPolicyDeniesPath_ThrowsUnauthorizedAccessException()
    {
        var root = CreateTemporaryDirectory();
        var policy = new WorkspaceFilePermissionPolicy(
            defaultMode: WorkspacePermissionDefaultMode.Deny,
            rules: new Dictionary<WorkspaceFilePermissionOperation, WorkspaceFilePermissionRule>
            {
                [WorkspaceFilePermissionOperation.Create] = new WorkspaceFilePermissionRule(
                    AllowPatterns: ["src/**"],
                    DenyPatterns: [])
            });
        var operations = new WorkspaceFileOperations(
            root,
            permissionPolicy: policy);

        var exception = Assert.Throws<UnauthorizedAccessException>(
            () => operations.Create("docs/new-file.txt", "conteudo"));

        Assert.Contains("nao e permitida", exception.Message, StringComparison.OrdinalIgnoreCase);
        Assert.False(File.Exists(Path.Combine(root, "docs", "new-file.txt")));
    }

    [Fact]
    public void Edit_WhenFileExists_OverwritesContent()
    {
        var root = CreateTemporaryDirectory();
        CreateFile(root, "src/program.cs", "conteudo antigo");
        var operations = new WorkspaceFileOperations(root);

        operations.Edit("src/program.cs", "conteudo novo");

        Assert.Equal("conteudo novo", File.ReadAllText(Path.Combine(root, "src", "program.cs")));
    }

    [Fact]
    public void Edit_WhenFileDoesNotExist_Throws()
    {
        var root = CreateTemporaryDirectory();
        var operations = new WorkspaceFileOperations(root);

        var exception = Assert.Throws<FileNotFoundException>(
            () => operations.Edit("src/missing.cs", "conteudo"));

        Assert.Contains("nao existe", exception.Message, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public void Copy_WhenSourceIsFile_CopiesToDestination()
    {
        var root = CreateTemporaryDirectory();
        CreateFile(root, "src/input.txt", "copy me");
        var operations = new WorkspaceFileOperations(root);

        operations.Copy("src/input.txt", "dest/output.txt");

        Assert.True(File.Exists(Path.Combine(root, "src", "input.txt")));
        Assert.Equal("copy me", File.ReadAllText(Path.Combine(root, "dest", "output.txt")));
    }

    [Fact]
    public void Copy_WhenDirectoryDestinationExistsAndOverwriteTrue_ReplacesDestination()
    {
        var root = CreateTemporaryDirectory();
        CreateFile(root, "source/new.txt", "novo");
        CreateFile(root, "source/nested/keep.txt", "nested");
        CreateFile(root, "destination/old.txt", "velho");
        var operations = new WorkspaceFileOperations(root);

        operations.Copy("source", "destination", overwrite: true);

        Assert.False(File.Exists(Path.Combine(root, "destination", "old.txt")));
        Assert.Equal("novo", File.ReadAllText(Path.Combine(root, "destination", "new.txt")));
        Assert.Equal("nested", File.ReadAllText(Path.Combine(root, "destination", "nested", "keep.txt")));
    }

    [Fact]
    public void Copy_WhenDestinationIsInsideSourceDirectory_Throws()
    {
        var root = CreateTemporaryDirectory();
        CreateFile(root, "source/file.txt", "content");
        var operations = new WorkspaceFileOperations(root);

        var exception = Assert.Throws<InvalidOperationException>(
            () => operations.Copy("source", "source/nested/copy"));

        Assert.Contains("nao podem se sobrepor", exception.Message, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public void Move_WhenSourceAndDestinationDirectoriesOverlap_Throws()
    {
        var root = CreateTemporaryDirectory();
        CreateFile(root, "source/nested/file.txt", "content");
        var operations = new WorkspaceFileOperations(root);

        var exception = Assert.Throws<InvalidOperationException>(
            () => operations.Move("source/nested", "source", overwrite: true));

        Assert.Contains("nao podem se sobrepor", exception.Message, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public void Move_WhenSourceIsFile_MovesAndRemovesOriginal()
    {
        var root = CreateTemporaryDirectory();
        CreateFile(root, "src/file.txt", "move me");
        var operations = new WorkspaceFileOperations(root);

        operations.Move("src/file.txt", "dest/file.txt");

        Assert.False(File.Exists(Path.Combine(root, "src", "file.txt")));
        Assert.Equal("move me", File.ReadAllText(Path.Combine(root, "dest", "file.txt")));
    }

    [Fact]
    public void Move_WhenDestinationAlreadyExistsAndOverwriteIsFalse_Throws()
    {
        var root = CreateTemporaryDirectory();
        CreateFile(root, "src/file.txt", "origem");
        CreateFile(root, "dest/file.txt", "destino");
        var operations = new WorkspaceFileOperations(root);

        var exception = Assert.Throws<InvalidOperationException>(
            () => operations.Move("src/file.txt", "dest/file.txt"));

        Assert.Contains("destino ja existe", exception.Message, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public void Move_WhenDirectoryOverwriteConfirmationIsRejected_Throws_AndKeepsDirectories()
    {
        var root = CreateTemporaryDirectory();
        CreateFile(root, "src/nested/file.txt", "origem");
        CreateFile(root, "dest/legacy.txt", "destino");
        WorkspaceDestructiveOperation? receivedOperation = null;
        var operations = new WorkspaceFileOperations(
            root,
            operation =>
            {
                receivedOperation = operation;
                return false;
            });

        var exception = Assert.Throws<InvalidOperationException>(
            () => operations.Move("src", "dest", overwrite: true));

        Assert.Contains("Operacao destrutiva cancelada", exception.Message, StringComparison.Ordinal);
        Assert.True(Directory.Exists(Path.Combine(root, "src")));
        Assert.True(File.Exists(Path.Combine(root, "dest", "legacy.txt")));
        Assert.True(receivedOperation.HasValue);
        Assert.Equal(WorkspaceDestructiveOperationKind.MoveOverwriteDirectory, receivedOperation.Value.Kind);
        Assert.True(receivedOperation.Value.IsRecursive);
    }

    [Fact]
    public void Move_WhenDirectoryOverwriteConfirmationIsAccepted_ReplacesDestinationAndCapturesMetadata()
    {
        var root = CreateTemporaryDirectory();
        CreateFile(root, "src/nested/file.txt", "origem");
        CreateFile(root, "dest/legacy.txt", "destino");
        WorkspaceDestructiveOperation? receivedOperation = null;
        var operations = new WorkspaceFileOperations(
            root,
            operation =>
            {
                receivedOperation = operation;
                return true;
            });

        operations.Move("src", "dest", overwrite: true);

        var sourcePath = Path.Combine(root, "src");
        var destinationPath = Path.Combine(root, "dest");
        Assert.False(Directory.Exists(sourcePath));
        Assert.False(File.Exists(Path.Combine(destinationPath, "legacy.txt")));
        Assert.Equal("origem", File.ReadAllText(Path.Combine(destinationPath, "nested", "file.txt")));
        Assert.True(receivedOperation.HasValue);
        Assert.Equal(WorkspaceDestructiveOperationKind.MoveOverwriteDirectory, receivedOperation.Value.Kind);
        Assert.True(receivedOperation.Value.IsRecursive);
        Assert.Equal(destinationPath, receivedOperation.Value.TargetPath);
        Assert.Equal(sourcePath, receivedOperation.Value.SourcePath);
        Assert.Equal(destinationPath, receivedOperation.Value.DestinationPath);
    }

    [Fact]
    public void Delete_WhenPathIsFile_DeletesFile()
    {
        var root = CreateTemporaryDirectory();
        CreateFile(root, "tmp/delete-me.txt", "bye");
        var operations = new WorkspaceFileOperations(root);

        operations.Delete("tmp/delete-me.txt");

        Assert.False(File.Exists(Path.Combine(root, "tmp", "delete-me.txt")));
    }

    [Fact]
    public void Delete_WhenConfirmationIsRejected_Throws_AndKeepsFile()
    {
        var root = CreateTemporaryDirectory();
        CreateFile(root, "tmp/delete-me.txt", "bye");
        WorkspaceDestructiveOperation? receivedOperation = null;
        var operations = new WorkspaceFileOperations(
            root,
            operation =>
            {
                receivedOperation = operation;
                return false;
            });

        var exception = Assert.Throws<InvalidOperationException>(
            () => operations.Delete("tmp/delete-me.txt"));

        Assert.Contains("Operacao destrutiva cancelada", exception.Message, StringComparison.Ordinal);
        Assert.True(File.Exists(Path.Combine(root, "tmp", "delete-me.txt")));
        Assert.True(receivedOperation.HasValue);
        Assert.Equal(WorkspaceDestructiveOperationKind.Delete, receivedOperation.Value.Kind);
        Assert.False(receivedOperation.Value.IsRecursive);
    }

    [Fact]
    public void Delete_WhenRecursiveDirectoryConfirmationIsRejected_Throws_AndKeepsDirectory()
    {
        var root = CreateTemporaryDirectory();
        CreateFile(root, "tmp/nested/file.txt", "bye");
        WorkspaceDestructiveOperation? receivedOperation = null;
        var operations = new WorkspaceFileOperations(
            root,
            operation =>
            {
                receivedOperation = operation;
                return false;
            });

        var exception = Assert.Throws<InvalidOperationException>(
            () => operations.Delete("tmp", recursive: true));

        Assert.Contains("Operacao destrutiva cancelada", exception.Message, StringComparison.Ordinal);
        Assert.True(Directory.Exists(Path.Combine(root, "tmp")));
        Assert.True(File.Exists(Path.Combine(root, "tmp", "nested", "file.txt")));
        Assert.True(receivedOperation.HasValue);
        Assert.Equal(WorkspaceDestructiveOperationKind.Delete, receivedOperation.Value.Kind);
        Assert.True(receivedOperation.Value.IsRecursive);
    }

    [Fact]
    public void Delete_WhenDirectoryIsEmpty_RequestsConfirmationWithNonRecursiveOperation()
    {
        var root = CreateTemporaryDirectory();
        Directory.CreateDirectory(Path.Combine(root, "tmp"));
        WorkspaceDestructiveOperation? receivedOperation = null;
        var operations = new WorkspaceFileOperations(
            root,
            operation =>
            {
                receivedOperation = operation;
                return true;
            });

        operations.Delete("tmp", recursive: false);

        var expectedPath = Path.Combine(root, "tmp");
        Assert.False(Directory.Exists(expectedPath));
        Assert.True(receivedOperation.HasValue);
        Assert.Equal(WorkspaceDestructiveOperationKind.Delete, receivedOperation.Value.Kind);
        Assert.False(receivedOperation.Value.IsRecursive);
        Assert.Equal(expectedPath, receivedOperation.Value.TargetPath);
    }

    [Fact]
    public void Delete_WhenDirectoryIsNotEmptyWithoutRecursive_Throws()
    {
        var root = CreateTemporaryDirectory();
        CreateFile(root, "tmp/nested/file.txt", "data");
        var destructiveOperationRequested = false;
        var operations = new WorkspaceFileOperations(
            root,
            _ =>
            {
                destructiveOperationRequested = true;
                return true;
            });

        var exception = Assert.Throws<InvalidOperationException>(
            () => operations.Delete("tmp"));

        Assert.Contains("recursiva", exception.Message, StringComparison.OrdinalIgnoreCase);
        Assert.False(destructiveOperationRequested);
        Assert.True(Directory.Exists(Path.Combine(root, "tmp")));
    }

    [Fact]
    public void Delete_WhenDirectoryIsNotEmptyWithRecursive_DeletesDirectory()
    {
        var root = CreateTemporaryDirectory();
        CreateFile(root, "tmp/nested/file.txt", "data");
        var operations = new WorkspaceFileOperations(root);

        operations.Delete("tmp", recursive: true);

        Assert.False(Directory.Exists(Path.Combine(root, "tmp")));
    }

    [Fact]
    public void Delete_WhenPathTargetsWorkspaceRoot_Throws()
    {
        var root = CreateTemporaryDirectory();
        var operations = new WorkspaceFileOperations(root);

        var exception = Assert.Throws<InvalidOperationException>(
            () => operations.Delete("."));

        Assert.Contains("raiz do workspace", exception.Message, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public void Delete_WhenPermissionPolicyDeniesPath_ThrowsUnauthorizedAccessException()
    {
        var root = CreateTemporaryDirectory();
        CreateFile(root, "protected/secret.txt", "sigilo");
        var policy = new WorkspaceFilePermissionPolicy(
            rules: new Dictionary<WorkspaceFilePermissionOperation, WorkspaceFilePermissionRule>
            {
                [WorkspaceFilePermissionOperation.Delete] = new WorkspaceFilePermissionRule(
                    AllowPatterns: ["**"],
                    DenyPatterns: ["protected/**"])
            });
        var operations = new WorkspaceFileOperations(
            root,
            permissionPolicy: policy);

        var exception = Assert.Throws<UnauthorizedAccessException>(
            () => operations.Delete("protected/secret.txt"));

        Assert.Contains("delete", exception.Message, StringComparison.OrdinalIgnoreCase);
        Assert.True(File.Exists(Path.Combine(root, "protected", "secret.txt")));
    }

    [Fact]
    public void Delete_WhenPathDoesNotExist_DoesNotRequestConfirmation()
    {
        var root = CreateTemporaryDirectory();
        var destructiveOperationRequested = false;
        var operations = new WorkspaceFileOperations(
            root,
            _ =>
            {
                destructiveOperationRequested = true;
                return true;
            });

        var exception = Assert.Throws<FileNotFoundException>(
            () => operations.Delete("tmp/missing.txt"));

        Assert.Contains("nao existe", exception.Message, StringComparison.OrdinalIgnoreCase);
        Assert.False(destructiveOperationRequested);
    }

    private static string CreateTemporaryDirectory()
    {
        var directoryPath = Path.Combine(
            Path.GetTempPath(),
            "asxrun-workspace-file-operations-tests",
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
