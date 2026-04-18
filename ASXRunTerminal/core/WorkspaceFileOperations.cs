namespace ASXRunTerminal.Core;

internal sealed class WorkspaceFileOperations
{
    private static readonly StringComparison PathComparison = GetPathComparison();

    private readonly string _workspaceRootDirectory;
    private readonly string _workspaceRootDirectoryWithSeparator;

    public WorkspaceFileOperations(string workspaceRootDirectory)
    {
        _workspaceRootDirectory = ResolveWorkspaceRootDirectory(workspaceRootDirectory);
        _workspaceRootDirectoryWithSeparator = EnsureTrailingDirectorySeparator(_workspaceRootDirectory);
    }

    public string WorkspaceRootDirectoryPath => _workspaceRootDirectory;

    public string Read(string path)
    {
        EnsureFilePathArgument(path, nameof(path));

        var resolvedPath = ResolveWorkspacePath(path, nameof(path));
        if (Directory.Exists(resolvedPath))
        {
            throw new InvalidOperationException(
                $"Nao foi possivel ler '{resolvedPath}'. O caminho aponta para um diretorio.");
        }

        if (!File.Exists(resolvedPath))
        {
            throw new FileNotFoundException(
                $"Nao foi possivel ler o arquivo '{resolvedPath}'. O arquivo nao existe.",
                resolvedPath);
        }

        return File.ReadAllText(resolvedPath);
    }

    public void Create(string path, string content)
    {
        ArgumentNullException.ThrowIfNull(content);
        EnsureFilePathArgument(path, nameof(path));

        var resolvedPath = ResolveWorkspacePath(path, nameof(path));
        if (Directory.Exists(resolvedPath))
        {
            throw new InvalidOperationException(
                $"Nao foi possivel criar '{resolvedPath}'. Ja existe um diretorio com esse caminho.");
        }

        if (File.Exists(resolvedPath))
        {
            throw new InvalidOperationException(
                $"Nao foi possivel criar '{resolvedPath}'. O arquivo ja existe.");
        }

        var directoryPath = Path.GetDirectoryName(resolvedPath)
            ?? throw new InvalidOperationException(
                $"Nao foi possivel resolver o diretorio de destino para '{resolvedPath}'.");

        Directory.CreateDirectory(directoryPath);
        File.WriteAllText(resolvedPath, content);
    }

    public void Edit(string path, string content)
    {
        ArgumentNullException.ThrowIfNull(content);
        EnsureFilePathArgument(path, nameof(path));

        var resolvedPath = ResolveWorkspacePath(path, nameof(path));
        if (Directory.Exists(resolvedPath))
        {
            throw new InvalidOperationException(
                $"Nao foi possivel editar '{resolvedPath}'. O caminho aponta para um diretorio.");
        }

        if (!File.Exists(resolvedPath))
        {
            throw new FileNotFoundException(
                $"Nao foi possivel editar '{resolvedPath}'. O arquivo nao existe.",
                resolvedPath);
        }

        File.WriteAllText(resolvedPath, content);
    }

    public void Copy(string sourcePath, string destinationPath, bool overwrite = false)
    {
        var resolvedSourcePath = ResolveWorkspacePath(sourcePath, nameof(sourcePath));
        var resolvedDestinationPath = ResolveWorkspacePath(destinationPath, nameof(destinationPath));

        ValidateDistinctPaths(
            resolvedSourcePath,
            resolvedDestinationPath,
            "copiar");

        if (File.Exists(resolvedSourcePath))
        {
            CopyFile(
                resolvedSourcePath,
                resolvedDestinationPath,
                overwrite);
            return;
        }

        if (Directory.Exists(resolvedSourcePath))
        {
            ValidateDirectoryPathsDoNotOverlap(
                resolvedSourcePath,
                resolvedDestinationPath,
                "copiar");
            CopyDirectory(
                resolvedSourcePath,
                resolvedDestinationPath,
                overwrite);
            return;
        }

        throw new FileNotFoundException(
            $"Nao foi possivel copiar '{resolvedSourcePath}'. O caminho de origem nao existe.",
            resolvedSourcePath);
    }

    public void Move(string sourcePath, string destinationPath, bool overwrite = false)
    {
        var resolvedSourcePath = ResolveWorkspacePath(sourcePath, nameof(sourcePath));
        var resolvedDestinationPath = ResolveWorkspacePath(destinationPath, nameof(destinationPath));

        ValidateDistinctPaths(
            resolvedSourcePath,
            resolvedDestinationPath,
            "mover");

        if (File.Exists(resolvedSourcePath))
        {
            MoveFile(
                resolvedSourcePath,
                resolvedDestinationPath,
                overwrite);
            return;
        }

        if (Directory.Exists(resolvedSourcePath))
        {
            ValidateDirectoryPathsDoNotOverlap(
                resolvedSourcePath,
                resolvedDestinationPath,
                "mover");
            MoveDirectory(
                resolvedSourcePath,
                resolvedDestinationPath,
                overwrite);
            return;
        }

        throw new FileNotFoundException(
            $"Nao foi possivel mover '{resolvedSourcePath}'. O caminho de origem nao existe.",
            resolvedSourcePath);
    }

    public void Delete(string path, bool recursive = false)
    {
        var resolvedPath = ResolveWorkspacePath(path, nameof(path));

        if (File.Exists(resolvedPath))
        {
            File.Delete(resolvedPath);
            return;
        }

        if (Directory.Exists(resolvedPath))
        {
            if (!recursive && Directory.EnumerateFileSystemEntries(resolvedPath).Any())
            {
                throw new InvalidOperationException(
                    $"Nao foi possivel excluir '{resolvedPath}'. O diretorio nao esta vazio e requer exclusao recursiva.");
            }

            Directory.Delete(resolvedPath, recursive: recursive);
            return;
        }

        throw new FileNotFoundException(
            $"Nao foi possivel excluir '{resolvedPath}'. O caminho informado nao existe.",
            resolvedPath);
    }

    private static void EnsureFilePathArgument(string? path, string argumentName)
    {
        if (string.IsNullOrWhiteSpace(path))
        {
            throw new ArgumentException(
                $"O argumento '{argumentName}' e obrigatorio.",
                argumentName);
        }

        if (Path.EndsInDirectorySeparator(path.Trim()))
        {
            throw new InvalidOperationException(
                $"O argumento '{argumentName}' deve apontar para um arquivo.");
        }
    }

    private void CopyFile(
        string sourcePath,
        string destinationPath,
        bool overwrite)
    {
        if (Directory.Exists(destinationPath))
        {
            throw new InvalidOperationException(
                $"Nao foi possivel copiar o arquivo para '{destinationPath}'. Ja existe um diretorio nesse caminho.");
        }

        if (!overwrite && File.Exists(destinationPath))
        {
            throw new InvalidOperationException(
                $"Nao foi possivel copiar para '{destinationPath}'. O arquivo de destino ja existe.");
        }

        var destinationDirectory = Path.GetDirectoryName(destinationPath)
            ?? throw new InvalidOperationException(
                $"Nao foi possivel resolver o diretorio de destino para '{destinationPath}'.");

        Directory.CreateDirectory(destinationDirectory);
        File.Copy(sourcePath, destinationPath, overwrite: overwrite);
    }

    private void CopyDirectory(
        string sourcePath,
        string destinationPath,
        bool overwrite)
    {
        if (File.Exists(destinationPath))
        {
            throw new InvalidOperationException(
                $"Nao foi possivel copiar o diretorio para '{destinationPath}'. Ja existe um arquivo nesse caminho.");
        }

        if (Directory.Exists(destinationPath))
        {
            if (!overwrite)
            {
                throw new InvalidOperationException(
                    $"Nao foi possivel copiar para '{destinationPath}'. O diretorio de destino ja existe.");
            }

            Directory.Delete(destinationPath, recursive: true);
        }

        CopyDirectoryRecursively(sourcePath, destinationPath);
    }

    private void MoveFile(
        string sourcePath,
        string destinationPath,
        bool overwrite)
    {
        if (Directory.Exists(destinationPath))
        {
            throw new InvalidOperationException(
                $"Nao foi possivel mover o arquivo para '{destinationPath}'. Ja existe um diretorio nesse caminho.");
        }

        if (!overwrite && File.Exists(destinationPath))
        {
            throw new InvalidOperationException(
                $"Nao foi possivel mover para '{destinationPath}'. O arquivo de destino ja existe.");
        }

        var destinationDirectory = Path.GetDirectoryName(destinationPath)
            ?? throw new InvalidOperationException(
                $"Nao foi possivel resolver o diretorio de destino para '{destinationPath}'.");

        Directory.CreateDirectory(destinationDirectory);
        File.Move(sourcePath, destinationPath, overwrite);
    }

    private void MoveDirectory(
        string sourcePath,
        string destinationPath,
        bool overwrite)
    {
        if (File.Exists(destinationPath))
        {
            throw new InvalidOperationException(
                $"Nao foi possivel mover o diretorio para '{destinationPath}'. Ja existe um arquivo nesse caminho.");
        }

        if (Directory.Exists(destinationPath))
        {
            if (!overwrite)
            {
                throw new InvalidOperationException(
                    $"Nao foi possivel mover para '{destinationPath}'. O diretorio de destino ja existe.");
            }

            Directory.Delete(destinationPath, recursive: true);
        }

        var destinationParentDirectory = Path.GetDirectoryName(destinationPath)
            ?? throw new InvalidOperationException(
                $"Nao foi possivel resolver o diretorio de destino para '{destinationPath}'.");

        Directory.CreateDirectory(destinationParentDirectory);
        Directory.Move(sourcePath, destinationPath);
    }

    private static void CopyDirectoryRecursively(
        string sourceDirectoryPath,
        string destinationDirectoryPath)
    {
        Directory.CreateDirectory(destinationDirectoryPath);

        foreach (var sourceDirectory in Directory.EnumerateDirectories(
                     sourceDirectoryPath,
                     "*",
                     SearchOption.AllDirectories))
        {
            var relativeDirectory = Path.GetRelativePath(sourceDirectoryPath, sourceDirectory);
            var destinationDirectory = Path.Combine(destinationDirectoryPath, relativeDirectory);
            Directory.CreateDirectory(destinationDirectory);
        }

        foreach (var sourceFile in Directory.EnumerateFiles(
                     sourceDirectoryPath,
                     "*",
                     SearchOption.AllDirectories))
        {
            var relativeFile = Path.GetRelativePath(sourceDirectoryPath, sourceFile);
            var destinationFile = Path.Combine(destinationDirectoryPath, relativeFile);
            var destinationParent = Path.GetDirectoryName(destinationFile)
                ?? throw new InvalidOperationException(
                    $"Nao foi possivel resolver o diretorio de destino para '{destinationFile}'.");

            Directory.CreateDirectory(destinationParent);
            File.Copy(sourceFile, destinationFile, overwrite: false);
        }
    }

    private static void ValidateDistinctPaths(
        string sourcePath,
        string destinationPath,
        string operationName)
    {
        if (string.Equals(sourcePath, destinationPath, PathComparison))
        {
            throw new InvalidOperationException(
                $"Nao foi possivel {operationName}. Origem e destino precisam ser diferentes.");
        }
    }

    private static void ValidateDirectoryPathsDoNotOverlap(
        string sourcePath,
        string destinationPath,
        string operationName)
    {
        if (IsSamePathOrNestedPath(destinationPath, sourcePath)
            || IsSamePathOrNestedPath(sourcePath, destinationPath))
        {
            throw new InvalidOperationException(
                $"Nao foi possivel {operationName}. Origem e destino de diretorio nao podem se sobrepor.");
        }
    }

    private string ResolveWorkspacePath(
        string? path,
        string argumentName,
        bool allowWorkspaceRoot = false)
    {
        if (string.IsNullOrWhiteSpace(path))
        {
            throw new ArgumentException(
                $"O argumento '{argumentName}' e obrigatorio.",
                argumentName);
        }

        var trimmedPath = path.Trim();
        var candidatePath = Path.IsPathRooted(trimmedPath)
            ? trimmedPath
            : Path.Combine(_workspaceRootDirectory, trimmedPath);
        var resolvedPath = Path.GetFullPath(candidatePath);

        if (!IsPathInsideWorkspaceRoot(resolvedPath))
        {
            throw new UnauthorizedAccessException(
                $"O caminho '{trimmedPath}' esta fora da raiz do workspace '{_workspaceRootDirectory}'.");
        }

        if (!allowWorkspaceRoot && string.Equals(resolvedPath, _workspaceRootDirectory, PathComparison))
        {
            throw new InvalidOperationException(
                "Operacoes de arquivo na raiz do workspace exigem um caminho de arquivo ou diretorio especifico.");
        }

        return resolvedPath;
    }

    private bool IsPathInsideWorkspaceRoot(string resolvedPath)
    {
        if (string.Equals(resolvedPath, _workspaceRootDirectory, PathComparison))
        {
            return true;
        }

        return resolvedPath.StartsWith(_workspaceRootDirectoryWithSeparator, PathComparison);
    }

    private static bool IsSamePathOrNestedPath(string candidatePath, string parentPath)
    {
        if (string.Equals(candidatePath, parentPath, PathComparison))
        {
            return true;
        }

        if (!candidatePath.StartsWith(parentPath, PathComparison))
        {
            return false;
        }

        return candidatePath.Length > parentPath.Length
               && IsDirectorySeparator(candidatePath[parentPath.Length]);
    }

    private static bool IsDirectorySeparator(char value)
    {
        return value == Path.DirectorySeparatorChar
            || value == Path.AltDirectorySeparatorChar;
    }

    private static string ResolveWorkspaceRootDirectory(string workspaceRootDirectory)
    {
        if (string.IsNullOrWhiteSpace(workspaceRootDirectory))
        {
            throw new InvalidOperationException(
                "Nao foi possivel resolver o diretorio raiz para operacoes de arquivo.");
        }

        var resolvedRootDirectory = Path.GetFullPath(workspaceRootDirectory.Trim());
        if (!Directory.Exists(resolvedRootDirectory))
        {
            throw new DirectoryNotFoundException(
                $"Nao foi possivel executar operacoes de arquivo. O diretorio '{resolvedRootDirectory}' nao existe.");
        }

        return resolvedRootDirectory;
    }

    private static string EnsureTrailingDirectorySeparator(string path)
    {
        if (Path.EndsInDirectorySeparator(path))
        {
            return path;
        }

        return $"{path}{Path.DirectorySeparatorChar}";
    }

    private static StringComparison GetPathComparison()
    {
        return OperatingSystem.IsWindows()
            ? StringComparison.OrdinalIgnoreCase
            : StringComparison.Ordinal;
    }
}
