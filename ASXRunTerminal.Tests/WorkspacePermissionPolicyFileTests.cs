using ASXRunTerminal.Config;
using ASXRunTerminal.Core;

namespace ASXRunTerminal.Tests;

public sealed class WorkspacePermissionPolicyFileTests
{
    [Fact]
    public void Load_WhenPolicyFileIsMissing_ReturnsAllowAllPolicy()
    {
        var workspaceRoot = CreateWorkspaceRoot();
        var policy = WorkspacePermissionPolicyFile.Load(workspaceRoot);

        policy.EnsureAllowed(
            WorkspaceFilePermissionOperation.Delete,
            workspaceRoot,
            Path.Combine(workspaceRoot, "tmp", "file.txt"));
    }

    [Fact]
    public void Load_WhenPolicyFileIsValid_ReturnsConfiguredPolicy()
    {
        var workspaceRoot = CreateWorkspaceRoot();
        var policyDirectory = Path.Combine(workspaceRoot, UserConfigFile.ConfigDirectoryName);
        var policyPath = Path.Combine(policyDirectory, WorkspacePermissionPolicyFile.WorkspacePermissionPolicyFileName);

        Directory.CreateDirectory(policyDirectory);
        File.WriteAllText(
            policyPath,
            """
            {
              "defaultMode": "deny",
              "edit": {
                "allow": ["src/**"]
              }
            }
            """);

        var policy = WorkspacePermissionPolicyFile.Load(workspaceRoot);

        policy.EnsureAllowed(
            WorkspaceFilePermissionOperation.Edit,
            workspaceRoot,
            Path.Combine(workspaceRoot, "src", "Program.cs"));

        var exception = Assert.Throws<UnauthorizedAccessException>(
            () => policy.EnsureAllowed(
                WorkspaceFilePermissionOperation.Edit,
                workspaceRoot,
                Path.Combine(workspaceRoot, "docs", "README.md")));

        Assert.Contains("nao e permitida", exception.Message, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public void Load_WhenDefaultModeIsInvalid_ThrowsInvalidOperationException()
    {
        var workspaceRoot = CreateWorkspaceRoot();
        var policyDirectory = Path.Combine(workspaceRoot, UserConfigFile.ConfigDirectoryName);
        var policyPath = Path.Combine(policyDirectory, WorkspacePermissionPolicyFile.WorkspacePermissionPolicyFileName);

        Directory.CreateDirectory(policyDirectory);
        File.WriteAllText(
            policyPath,
            """
            {
              "defaultMode": "strict"
            }
            """);

        var exception = Assert.Throws<InvalidOperationException>(
            () => WorkspacePermissionPolicyFile.Load(workspaceRoot));

        Assert.Contains("defaultMode", exception.Message, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public void Load_WhenPatternListContainsBlankValue_ThrowsInvalidOperationException()
    {
        var workspaceRoot = CreateWorkspaceRoot();
        var policyDirectory = Path.Combine(workspaceRoot, UserConfigFile.ConfigDirectoryName);
        var policyPath = Path.Combine(policyDirectory, WorkspacePermissionPolicyFile.WorkspacePermissionPolicyFileName);

        Directory.CreateDirectory(policyDirectory);
        File.WriteAllText(
            policyPath,
            """
            {
              "create": {
                "allow": ["src/**", " "]
              }
            }
            """);

        var exception = Assert.Throws<InvalidOperationException>(
            () => WorkspacePermissionPolicyFile.Load(workspaceRoot));

        Assert.Contains("padroes vazios", exception.Message, StringComparison.OrdinalIgnoreCase);
    }

    private static string CreateWorkspaceRoot()
    {
        var workspaceRoot = Path.Combine(
            Path.GetTempPath(),
            "asxrun-workspace-permission-policy-file-tests",
            Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(workspaceRoot);
        return workspaceRoot;
    }
}
