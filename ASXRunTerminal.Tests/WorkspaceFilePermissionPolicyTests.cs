using ASXRunTerminal.Core;

namespace ASXRunTerminal.Tests;

public sealed class WorkspaceFilePermissionPolicyTests
{
    [Fact]
    public void EnsureAllowed_WhenNoRulesConfigured_AllowsOperations()
    {
        var root = CreateTemporaryDirectory();
        var targetPath = Path.Combine(root, "src", "program.cs");
        var policy = WorkspaceFilePermissionPolicy.AllowAll;

        policy.EnsureAllowed(WorkspaceFilePermissionOperation.Edit, root, targetPath);
    }

    [Fact]
    public void EnsureAllowed_WhenDefaultModeIsDeny_RequiresAllowPattern()
    {
        var root = CreateTemporaryDirectory();
        var policy = new WorkspaceFilePermissionPolicy(
            defaultMode: WorkspacePermissionDefaultMode.Deny,
            rules: new Dictionary<WorkspaceFilePermissionOperation, WorkspaceFilePermissionRule>
            {
                [WorkspaceFilePermissionOperation.Edit] = new WorkspaceFilePermissionRule(
                    AllowPatterns: ["src/**"],
                    DenyPatterns: [])
            });

        policy.EnsureAllowed(
            WorkspaceFilePermissionOperation.Edit,
            root,
            Path.Combine(root, "src", "Program.cs"));

        var exception = Assert.Throws<UnauthorizedAccessException>(
            () => policy.EnsureAllowed(
                WorkspaceFilePermissionOperation.Edit,
                root,
                Path.Combine(root, "docs", "README.md")));

        Assert.Contains("nao e permitida", exception.Message, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public void EnsureAllowed_WhenDenyPatternMatches_DenyTakesPrecedence()
    {
        var root = CreateTemporaryDirectory();
        var policy = new WorkspaceFilePermissionPolicy(
            rules: new Dictionary<WorkspaceFilePermissionOperation, WorkspaceFilePermissionRule>
            {
                [WorkspaceFilePermissionOperation.Delete] = new WorkspaceFilePermissionRule(
                    AllowPatterns: ["**"],
                    DenyPatterns: ["secrets/**"])
            });

        var exception = Assert.Throws<UnauthorizedAccessException>(
            () => policy.EnsureAllowed(
                WorkspaceFilePermissionOperation.Delete,
                root,
                Path.Combine(root, "secrets", "token.txt")));

        Assert.Contains("delete", exception.Message, StringComparison.OrdinalIgnoreCase);
        Assert.Contains("secrets/token.txt", exception.Message, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public void EnsureAllowed_WhenPatternUsesBackslashes_NormalizesPattern()
    {
        var root = CreateTemporaryDirectory();
        var policy = new WorkspaceFilePermissionPolicy(
            defaultMode: WorkspacePermissionDefaultMode.Deny,
            rules: new Dictionary<WorkspaceFilePermissionOperation, WorkspaceFilePermissionRule>
            {
                [WorkspaceFilePermissionOperation.Create] = new WorkspaceFilePermissionRule(
                    AllowPatterns: ["src\\**"],
                    DenyPatterns: [])
            });

        policy.EnsureAllowed(
            WorkspaceFilePermissionOperation.Create,
            root,
            Path.Combine(root, "src", "new-file.txt"));
    }

    private static string CreateTemporaryDirectory()
    {
        var directoryPath = Path.Combine(
            Path.GetTempPath(),
            "asxrun-workspace-permission-policy-tests",
            Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(directoryPath);
        return directoryPath;
    }
}
