using ASXRunTerminal.Config;
using ASXRunTerminal.Core;

namespace ASXRunTerminal.Tests;

public sealed class ShellCommandPermissionPolicyFileTests
{
    [Fact]
    public void Load_WhenPolicyFileIsMissing_ReturnsDefaultPolicy()
    {
        var workspaceRoot = CreateWorkspaceRoot();

        var policy = ShellCommandPermissionPolicyFile.Load(workspaceRoot);

        var exception = Assert.Throws<UnauthorizedAccessException>(
            () => policy.EnsureAllowed("bash", "rm -rf ./tmp"));
        Assert.Contains("rm", exception.Message, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public void Load_WhenAllowlistContainsBlockedCommandWithoutExplicitApproval_ThrowsUnauthorizedAccessException()
    {
        var workspaceRoot = CreateWorkspaceRoot();
        var policyDirectory = Path.Combine(workspaceRoot, UserConfigFile.ConfigDirectoryName);
        var policyPath = Path.Combine(
            policyDirectory,
            ShellCommandPermissionPolicyFile.ShellCommandPermissionPolicyFileName);

        Directory.CreateDirectory(policyDirectory);
        File.WriteAllText(
            policyPath,
            """
            {
              "allow": ["rm"]
            }
            """);

        var policy = ShellCommandPermissionPolicyFile.Load(workspaceRoot);

        var exception = Assert.Throws<UnauthorizedAccessException>(
            () => policy.EnsureAllowed("bash", "rm -rf ./tmp"));
        Assert.Contains("aprovacao explicita", exception.Message, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public void Load_WhenAllowlistContainsBlockedCommandWithExplicitApproval_AllowsExecution()
    {
        var workspaceRoot = CreateWorkspaceRoot();
        var policyDirectory = Path.Combine(workspaceRoot, UserConfigFile.ConfigDirectoryName);
        var policyPath = Path.Combine(
            policyDirectory,
            ShellCommandPermissionPolicyFile.ShellCommandPermissionPolicyFileName);

        Directory.CreateDirectory(policyDirectory);
        File.WriteAllText(
            policyPath,
            """
            {
              "allow": ["rm"]
            }
            """);

        var policy = ShellCommandPermissionPolicyFile.Load(workspaceRoot);

        policy.EnsureAllowed(
            shellToolName: "bash",
            script: "rm -rf ./tmp",
            isDestructiveCommandApproved: true);
    }

    [Fact]
    public void Load_WhenDenylistContainsExtraCommand_BlocksExecution()
    {
        var workspaceRoot = CreateWorkspaceRoot();
        var policyDirectory = Path.Combine(workspaceRoot, UserConfigFile.ConfigDirectoryName);
        var policyPath = Path.Combine(
            policyDirectory,
            ShellCommandPermissionPolicyFile.ShellCommandPermissionPolicyFileName);

        Directory.CreateDirectory(policyDirectory);
        File.WriteAllText(
            policyPath,
            """
            {
              "deny": ["echo"]
            }
            """);

        var policy = ShellCommandPermissionPolicyFile.Load(workspaceRoot);

        var exception = Assert.Throws<UnauthorizedAccessException>(
            () => policy.EnsureAllowed("bash", "echo 'bloqueado'"));
        Assert.Contains("echo", exception.Message, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public void Load_WhenPolicyContainsBlankEntry_ThrowsInvalidOperationException()
    {
        var workspaceRoot = CreateWorkspaceRoot();
        var policyDirectory = Path.Combine(workspaceRoot, UserConfigFile.ConfigDirectoryName);
        var policyPath = Path.Combine(
            policyDirectory,
            ShellCommandPermissionPolicyFile.ShellCommandPermissionPolicyFileName);

        Directory.CreateDirectory(policyDirectory);
        File.WriteAllText(
            policyPath,
            """
            {
              "allow": [" ", "rm"]
            }
            """);

        var exception = Assert.Throws<InvalidOperationException>(
            () => ShellCommandPermissionPolicyFile.Load(workspaceRoot));

        Assert.Contains("comandos vazios", exception.Message, StringComparison.OrdinalIgnoreCase);
    }

    private static string CreateWorkspaceRoot()
    {
        var workspaceRoot = Path.Combine(
            Path.GetTempPath(),
            "asxrun-shell-command-policy-file-tests",
            Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(workspaceRoot);
        return workspaceRoot;
    }
}
