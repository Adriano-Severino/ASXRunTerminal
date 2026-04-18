using ASXRunTerminal.Core;

namespace ASXRunTerminal.Tests;

public sealed class WorkspaceRootDetectorTests
{
    [Fact]
    public void Resolve_WhenMonorepoMarkerExists_PrefersMonorepoRoot()
    {
        var root = CreateTemporaryDirectory();
        var solutionDirectory = Path.Combine(root, "apps", "billing");
        var nestedDirectory = Path.Combine(solutionDirectory, "src", "Domain");

        Directory.CreateDirectory(nestedDirectory);
        Directory.CreateDirectory(Path.Combine(root, ".git"));
        File.WriteAllText(Path.Combine(root, "pnpm-workspace.yaml"), "packages:\n  - apps/*\n");
        File.WriteAllText(Path.Combine(solutionDirectory, "billing.sln"), string.Empty);

        var resolution = WorkspaceRootDetector.Resolve(() => nestedDirectory);

        Assert.Equal(Path.GetFullPath(root), resolution.DirectoryPath);
        Assert.Equal(WorkspaceRootKind.Monorepo, resolution.Kind);
    }

    [Fact]
    public void Resolve_WhenSolutionExists_PrefersNearestSolutionOverGitRoot()
    {
        var root = CreateTemporaryDirectory();
        var solutionDirectory = Path.Combine(root, "src", "ASXRunTerminal");
        var nestedDirectory = Path.Combine(solutionDirectory, "core", "tools");

        Directory.CreateDirectory(nestedDirectory);
        Directory.CreateDirectory(Path.Combine(root, ".git"));
        File.WriteAllText(Path.Combine(solutionDirectory, "ASXRunTerminal.slnx"), string.Empty);

        var resolution = WorkspaceRootDetector.Resolve(() => nestedDirectory);

        Assert.Equal(Path.GetFullPath(solutionDirectory), resolution.DirectoryPath);
        Assert.Equal(WorkspaceRootKind.SolutionOrWorkspace, resolution.Kind);
    }

    [Fact]
    public void Resolve_WhenGitMarkerIsAFile_ReturnsGitRoot()
    {
        var root = CreateTemporaryDirectory();
        var nestedDirectory = Path.Combine(root, "src", "feature");

        Directory.CreateDirectory(nestedDirectory);
        File.WriteAllText(Path.Combine(root, ".git"), "gitdir: /tmp/worktree\n");

        var resolution = WorkspaceRootDetector.Resolve(() => nestedDirectory);

        Assert.Equal(Path.GetFullPath(root), resolution.DirectoryPath);
        Assert.Equal(WorkspaceRootKind.Git, resolution.Kind);
    }

    [Fact]
    public void Resolve_WhenPackageJsonDeclaresWorkspaces_ReturnsMonorepoRoot()
    {
        var root = CreateTemporaryDirectory();
        var nestedDirectory = Path.Combine(root, "packages", "api", "src");

        Directory.CreateDirectory(nestedDirectory);
        File.WriteAllText(
            Path.Combine(root, "package.json"),
            """
            {
              "name": "workspace-root",
              "private": true,
              "workspaces": [
                "packages/*"
              ]
            }
            """);

        var resolution = WorkspaceRootDetector.Resolve(() => nestedDirectory);

        Assert.Equal(Path.GetFullPath(root), resolution.DirectoryPath);
        Assert.Equal(WorkspaceRootKind.Monorepo, resolution.Kind);
    }

    [Fact]
    public void Resolve_WhenNoMarkersExist_ReturnsCurrentDirectory()
    {
        var root = CreateTemporaryDirectory();
        var nestedDirectory = Path.Combine(root, "nested");

        Directory.CreateDirectory(nestedDirectory);

        var resolution = WorkspaceRootDetector.Resolve(() => nestedDirectory);

        Assert.Equal(Path.GetFullPath(nestedDirectory), resolution.DirectoryPath);
        Assert.Equal(WorkspaceRootKind.CurrentDirectory, resolution.Kind);
    }

    [Fact]
    public void Resolve_WhenCurrentDirectoryIsMissing_Throws()
    {
        var exception = Assert.Throws<InvalidOperationException>(
            () => WorkspaceRootDetector.Resolve(static () => " "));

        Assert.Equal(
            "Nao foi possivel resolver o diretorio atual para detectar a raiz do workspace.",
            exception.Message);
    }

    private static string CreateTemporaryDirectory()
    {
        var directoryPath = Path.Combine(
            Path.GetTempPath(),
            "asxrun-workspace-root-detector-tests",
            Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(directoryPath);
        return directoryPath;
    }
}
