using ASXRunTerminal.Core;

namespace ASXRunTerminal.Tests;

public sealed class ShellCommandPermissionPolicyTests
{
    [Fact]
    public void EnsureAllowed_WhenScriptContainsBlockedCommand_ThrowsUnauthorizedAccessException()
    {
        var policy = ShellCommandPermissionPolicy.Default;

        var exception = Assert.Throws<UnauthorizedAccessException>(
            () => policy.EnsureAllowed("bash", "rm -rf ./tmp"));

        Assert.Contains("alto risco", exception.Message, StringComparison.OrdinalIgnoreCase);
        Assert.Contains("rm", exception.Message, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public void EnsureAllowed_WhenBlockedCommandIsAllowlistedWithoutApproval_ThrowsUnauthorizedAccessException()
    {
        var policy = new ShellCommandPermissionPolicy(allowedCommands: ["rm"]);

        var exception = Assert.Throws<UnauthorizedAccessException>(
            () => policy.EnsureAllowed("bash", "rm -rf ./tmp"));

        Assert.Contains("aprovacao explicita", exception.Message, StringComparison.OrdinalIgnoreCase);
        Assert.Contains(
            ShellCommandPermissionPolicy.DestructiveApprovalArgumentName,
            exception.Message,
            StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public void EnsureAllowed_WhenBlockedCommandIsExplicitlyAllowlistedAndApproved_AllowsExecution()
    {
        var policy = new ShellCommandPermissionPolicy(allowedCommands: ["rm"]);

        policy.EnsureAllowed(
            shellToolName: "bash",
            script: "rm -rf ./tmp",
            isDestructiveCommandApproved: true);
    }

    [Fact]
    public void EnsureAllowed_WhenScriptUsesWrapper_StillDetectsBlockedCommand()
    {
        var policy = ShellCommandPermissionPolicy.Default;

        var exception = Assert.Throws<UnauthorizedAccessException>(
            () => policy.EnsureAllowed("bash", "sudo -n rm -rf ./tmp"));

        Assert.Contains("rm", exception.Message, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public void EnsureAllowed_WhenCommandIsSpecifiedAsAbsolutePath_StillDetectsBlockedCommand()
    {
        var policy = ShellCommandPermissionPolicy.Default;

        var exception = Assert.Throws<UnauthorizedAccessException>(
            () => policy.EnsureAllowed("bash", "/bin/rm -rf ./tmp"));

        Assert.Contains("rm", exception.Message, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public void ExtractCommandNames_IgnoresCommentsAndReturnsCommandsInOrder()
    {
        var commands = ShellCommandPermissionPolicy.ExtractCommandNames(
            """
            # rm -rf /nao-deve-ser-avaliado
            echo 'ok' | grep ok
            sudo -n rm -rf ./tmp
            """);

        Assert.Equal(3, commands.Count);
        Assert.Equal("echo", commands[0]);
        Assert.Equal("grep", commands[1]);
        Assert.Equal("rm", commands[2]);
    }
}
