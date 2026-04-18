using System.Text.Json;

namespace ASXRunTerminal.Core;

internal enum WorkspaceRootKind
{
    CurrentDirectory = 0,
    Git = 1,
    SolutionOrWorkspace = 2,
    Monorepo = 3
}

internal readonly record struct WorkspaceRootResolution(
    string DirectoryPath,
    WorkspaceRootKind Kind);

internal static class WorkspaceRootDetector
{
    private const string GitDirectoryMarkerName = ".git";
    private const string PackageJsonFileName = "package.json";
    private const string WorkspacesPropertyName = "workspaces";
    private static readonly string[] SolutionWorkspaceExtensions =
    [
        ".sln",
        ".slnx",
        ".code-workspace"
    ];

    private static readonly string[] MonorepoMarkerFileNames =
    [
        "pnpm-workspace.yaml",
        "lerna.json",
        "nx.json",
        "rush.json",
        "turbo.json",
        "workspace.json"
    ];

    public static WorkspaceRootResolution Resolve(
        Func<string?>? currentDirectoryResolver = null)
    {
        var currentDirectory = ResolveCurrentDirectory(currentDirectoryResolver);
        var normalizedCurrentDirectory = Path.GetFullPath(currentDirectory);

        string? gitRoot = null;
        string? solutionWorkspaceRoot = null;
        string? monorepoRoot = null;

        foreach (var candidateDirectory in EnumerateCandidateDirectories(normalizedCurrentDirectory))
        {
            if (monorepoRoot is null && ContainsMonorepoMarker(candidateDirectory))
            {
                monorepoRoot = candidateDirectory;
            }

            if (solutionWorkspaceRoot is null && ContainsSolutionWorkspaceMarker(candidateDirectory))
            {
                solutionWorkspaceRoot = candidateDirectory;
            }

            if (gitRoot is null && ContainsGitMarker(candidateDirectory))
            {
                gitRoot = candidateDirectory;
            }

            if (monorepoRoot is not null
                && solutionWorkspaceRoot is not null
                && gitRoot is not null)
            {
                break;
            }
        }

        if (monorepoRoot is not null)
        {
            return new WorkspaceRootResolution(monorepoRoot, WorkspaceRootKind.Monorepo);
        }

        if (solutionWorkspaceRoot is not null)
        {
            return new WorkspaceRootResolution(
                solutionWorkspaceRoot,
                WorkspaceRootKind.SolutionOrWorkspace);
        }

        if (gitRoot is not null)
        {
            return new WorkspaceRootResolution(gitRoot, WorkspaceRootKind.Git);
        }

        return new WorkspaceRootResolution(
            normalizedCurrentDirectory,
            WorkspaceRootKind.CurrentDirectory);
    }

    private static IEnumerable<string> EnumerateCandidateDirectories(string currentDirectory)
    {
        for (var directory = new DirectoryInfo(currentDirectory);
             directory is not null;
             directory = directory.Parent)
        {
            yield return directory.FullName;
        }
    }

    private static bool ContainsGitMarker(string directoryPath)
    {
        var gitMarkerPath = Path.Combine(directoryPath, GitDirectoryMarkerName);
        return Directory.Exists(gitMarkerPath) || File.Exists(gitMarkerPath);
    }

    private static bool ContainsSolutionWorkspaceMarker(string directoryPath)
    {
        foreach (var extension in SolutionWorkspaceExtensions)
        {
            if (HasAnyFileWithExtension(directoryPath, extension))
            {
                return true;
            }
        }

        return false;
    }

    private static bool ContainsMonorepoMarker(string directoryPath)
    {
        foreach (var markerFileName in MonorepoMarkerFileNames)
        {
            var markerPath = Path.Combine(directoryPath, markerFileName);
            if (File.Exists(markerPath))
            {
                return true;
            }
        }

        var packageJsonPath = Path.Combine(directoryPath, PackageJsonFileName);
        return PackageJsonDeclaresWorkspaces(packageJsonPath);
    }

    private static bool HasAnyFileWithExtension(string directoryPath, string extension)
    {
        try
        {
            using var enumerator = Directory.EnumerateFiles(
                directoryPath,
                $"*{extension}",
                SearchOption.TopDirectoryOnly).GetEnumerator();
            return enumerator.MoveNext();
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException)
        {
            return false;
        }
    }

    private static bool PackageJsonDeclaresWorkspaces(string packageJsonPath)
    {
        if (!File.Exists(packageJsonPath))
        {
            return false;
        }

        try
        {
            using var stream = File.OpenRead(packageJsonPath);
            using var document = JsonDocument.Parse(stream);
            if (document.RootElement.ValueKind != JsonValueKind.Object)
            {
                return false;
            }

            if (!document.RootElement.TryGetProperty(
                    WorkspacesPropertyName,
                    out var workspacesElement))
            {
                return false;
            }

            return workspacesElement.ValueKind switch
            {
                JsonValueKind.Array => workspacesElement.GetArrayLength() > 0,
                JsonValueKind.Object => HasAnyProperty(workspacesElement),
                JsonValueKind.String => !string.IsNullOrWhiteSpace(workspacesElement.GetString()),
                JsonValueKind.True => true,
                _ => false
            };
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException or JsonException)
        {
            return false;
        }
    }

    private static bool HasAnyProperty(JsonElement element)
    {
        using var enumerator = element.EnumerateObject();
        return enumerator.MoveNext();
    }

    private static string ResolveCurrentDirectory(Func<string?>? currentDirectoryResolver)
    {
        var resolver = currentDirectoryResolver ?? ResolveCurrentDirectoryFromEnvironment;
        var currentDirectory = resolver();
        if (string.IsNullOrWhiteSpace(currentDirectory))
        {
            throw new InvalidOperationException(
                "Nao foi possivel resolver o diretorio atual para detectar a raiz do workspace.");
        }

        return currentDirectory.Trim();
    }

    private static string? ResolveCurrentDirectoryFromEnvironment()
    {
        return Directory.GetCurrentDirectory();
    }
}
