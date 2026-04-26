using System.Runtime.InteropServices;
using ASXRunTerminal.Core;
using ASXRunTerminal.Infra;

namespace ASXRunTerminal.Tests;

public sealed class CrossPlatformShellIntegrationTests
{
    private static bool IsWindows =>
        RuntimeInformation.IsOSPlatform(OSPlatform.Windows);

    private static bool IsUnix =>
        RuntimeInformation.IsOSPlatform(OSPlatform.Linux)
        || RuntimeInformation.IsOSPlatform(OSPlatform.OSX);

    [Fact]
    [Trait("Category", "Integration")]
    [Trait("Category", "CrossPlatform")]
    [Trait("Shell", "PowerShell")]
    public async Task PowerShellProvider_ExecuteAsync_RunsScriptOnWindows()
    {
        if (!IsWindows)
        {
            return;
        }

        var provider = new PowerShellToolProvider(static () => new ShellCommandPermissionPolicy());
        var request = new ToolExecutionRequest(
            ToolName: "powershell",
            Arguments: new Dictionary<string, string>
            {
                ["script"] = "Write-Output 'powershell-integration-ok'"
            });

        var result = await provider.ExecuteAsync(request);

        Assert.True(result.IsSuccess);
        Assert.Equal(0, result.ExitCode);
        Assert.Equal("powershell-integration-ok", result.StdOut);
        Assert.Equal(string.Empty, result.StdErr);
    }

    [Fact]
    [Trait("Category", "Integration")]
    [Trait("Category", "CrossPlatform")]
    [Trait("Shell", "PowerShell")]
    public async Task ToolRuntime_ShellAlias_UsesPowerShell_OnWindows()
    {
        if (!IsWindows)
        {
            return;
        }

        var runtime = new ToolRuntime(
            new PowerShellToolProvider(static () => new ShellCommandPermissionPolicy()),
            new UnixShellToolProvider(static () => new ShellCommandPermissionPolicy()));
        var request = new ToolExecutionRequest(
            ToolName: "shell",
            Arguments: new Dictionary<string, string>
            {
                ["script"] = "Write-Output 'shell-alias-powershell-ok'"
            });

        var result = await runtime.ExecuteAsync(request);

        Assert.True(result.IsSuccess);
        Assert.Equal(0, result.ExitCode);
        Assert.Equal("shell-alias-powershell-ok", result.StdOut);
        Assert.Equal(string.Empty, result.StdErr);
    }

    [Fact]
    [Trait("Category", "Integration")]
    [Trait("Category", "CrossPlatform")]
    [Trait("Shell", "Bash")]
    public async Task BashProvider_ExecuteAsync_RunsScriptOnUnix()
    {
        if (!IsUnix)
        {
            return;
        }

        var provider = new UnixShellToolProvider(static () => new ShellCommandPermissionPolicy());
        var request = new ToolExecutionRequest(
            ToolName: "bash",
            Arguments: new Dictionary<string, string>
            {
                ["script"] = "echo 'bash-integration-ok'"
            });

        var result = await provider.ExecuteAsync(request);

        Assert.True(result.IsSuccess);
        Assert.Equal(0, result.ExitCode);
        Assert.Equal("bash-integration-ok", result.StdOut);
        Assert.Equal(string.Empty, result.StdErr);
    }

    [Fact]
    [Trait("Category", "Integration")]
    [Trait("Category", "CrossPlatform")]
    [Trait("Shell", "Bash")]
    public async Task ToolRuntime_ShellAlias_UsesBash_OnUnix()
    {
        if (!IsUnix)
        {
            return;
        }

        var runtime = new ToolRuntime(
            new PowerShellToolProvider(static () => new ShellCommandPermissionPolicy()),
            new UnixShellToolProvider(static () => new ShellCommandPermissionPolicy()));
        var request = new ToolExecutionRequest(
            ToolName: "shell",
            Arguments: new Dictionary<string, string>
            {
                ["script"] = "echo 'shell-alias-bash-ok'"
            });

        var result = await runtime.ExecuteAsync(request);

        Assert.True(result.IsSuccess);
        Assert.Equal(0, result.ExitCode);
        Assert.Equal("shell-alias-bash-ok", result.StdOut);
        Assert.Equal(string.Empty, result.StdErr);
    }
}
