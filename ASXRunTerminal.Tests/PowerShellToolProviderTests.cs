using System.Runtime.InteropServices;
using ASXRunTerminal.Core;
using ASXRunTerminal.Infra;

namespace ASXRunTerminal.Tests;

public sealed class PowerShellToolProviderTests
{
    private static bool IsWindows => RuntimeInformation.IsOSPlatform(OSPlatform.Windows);

    [Fact]
    public void ProviderName_ReturnsShell()
    {
        var provider = new PowerShellToolProvider();
        Assert.Equal("shell", provider.ProviderName);
    }

    [Fact]
    public void ListTools_ReturnsTool_OnlyOnWindows()
    {
        var provider = new PowerShellToolProvider();

        var tools = provider.ListTools();

        if (IsWindows)
        {
            Assert.Single(tools);
            Assert.Equal("powershell", tools[0].Name);
            Assert.Equal(2, tools[0].Parameters.Count);
            Assert.Equal("script", tools[0].Parameters[0].Name);
            Assert.True(tools[0].Parameters[0].IsRequired);
            Assert.Equal(
                ShellCommandPermissionPolicy.DestructiveApprovalArgumentName,
                tools[0].Parameters[1].Name);
            Assert.False(tools[0].Parameters[1].IsRequired);
        }
        else
        {
            Assert.Empty(tools);
        }
    }

    [Fact]
    public void CanHandle_ReturnsTrue_OnlyForPowershellOnWindows()
    {
        var provider = new PowerShellToolProvider();

        if (IsWindows)
        {
            Assert.True(provider.CanHandle("powershell"));
            Assert.True(provider.CanHandle("POWERSHELL"));
            Assert.False(provider.CanHandle("bash"));
        }
        else
        {
            Assert.False(provider.CanHandle("powershell"));
        }
    }

    [Fact]
    public async Task ExecuteAsync_ReturnsFailure_WhenNotOnWindows()
    {
        if (IsWindows)
        {
            return; // Teste irrelevante no Windows
        }

        var provider = new PowerShellToolProvider();
        ToolExecutionRequest request = ("powershell", new Dictionary<string, string> { ["script"] = "Write-Host 'test'" });

        var result = await provider.ExecuteAsync(request);

        Assert.False(result.IsSuccess);
        Assert.Equal(1, result.ExitCode);
        Assert.Contains("suportado no Windows", result.Error);
    }

    [Fact]
    public async Task ExecuteAsync_ReturnsFailure_WhenScriptIsMissing()
    {
        if (!IsWindows) return;

        var provider = new PowerShellToolProvider();
        ToolExecutionRequest request = ("powershell", new Dictionary<string, string>());

        var result = await provider.ExecuteAsync(request);

        Assert.False(result.IsSuccess);
        Assert.Equal(1, result.ExitCode);
        Assert.Contains("script", result.Error);
    }

    [Fact]
    public async Task ExecuteAsync_ReturnsFailure_WhenScriptIsBlank()
    {
        if (!IsWindows) return;

        var provider = new PowerShellToolProvider();
        ToolExecutionRequest request = ("powershell", new Dictionary<string, string> { ["script"] = "   " });

        var result = await provider.ExecuteAsync(request);

        Assert.False(result.IsSuccess);
        Assert.Equal(1, result.ExitCode);
        Assert.Contains("script", result.Error);
    }

    [Fact]
    public async Task ExecuteAsync_ReturnsFailure_WhenScriptIsBlockedByShellPolicy()
    {
        if (!IsWindows) return;

        var policy = new ShellCommandPermissionPolicy(
            blockedCommands: ["write-output"]);
        var provider = new PowerShellToolProvider(() => policy);
        ToolExecutionRequest request = (
            "powershell",
            new Dictionary<string, string> { ["script"] = "Write-Output 'blocked'" });

        var result = await provider.ExecuteAsync(request);

        Assert.False(result.IsSuccess);
        Assert.Equal(ShellCommandPermissionPolicy.BlockedCommandExitCode, result.ExitCode);
        Assert.Contains("alto risco", result.Error);
        Assert.Contains("write-output", result.Error, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public async Task ExecuteAsync_ReturnsFailure_WhenAllowlistedBlockedCommandHasNoExplicitApproval()
    {
        if (!IsWindows) return;

        var policy = new ShellCommandPermissionPolicy(
            allowedCommands: ["write-output"],
            blockedCommands: ["write-output"]);
        var provider = new PowerShellToolProvider(() => policy);
        ToolExecutionRequest request = (
            "powershell",
            new Dictionary<string, string> { ["script"] = "Write-Output 'Hello from policy'" });

        var result = await provider.ExecuteAsync(request);

        Assert.False(result.IsSuccess);
        Assert.Equal(ShellCommandPermissionPolicy.BlockedCommandExitCode, result.ExitCode);
        Assert.Contains("aprovacao explicita", result.Error, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public async Task ExecuteAsync_AllowlistOverridesBlocklist_WhenConfiguredAndExplicitlyApproved()
    {
        if (!IsWindows) return;

        var policy = new ShellCommandPermissionPolicy(
            allowedCommands: ["write-output"],
            blockedCommands: ["write-output"]);
        var provider = new PowerShellToolProvider(() => policy);
        ToolExecutionRequest request = (
            "powershell",
            new Dictionary<string, string>
            {
                ["script"] = "Write-Output 'Hello from policy'",
                [ShellCommandPermissionPolicy.DestructiveApprovalArgumentName] = "sim"
            });

        var result = await provider.ExecuteAsync(request);

        Assert.True(result.IsSuccess);
        Assert.Equal(0, result.ExitCode);
        Assert.Equal("Hello from policy", result.StdOut);
    }

    [Fact]
    public async Task ExecuteAsync_ExecutesScriptSuccessfully()
    {
        if (!IsWindows) return;

        var provider = new PowerShellToolProvider();
        ToolExecutionRequest request = ("powershell", new Dictionary<string, string> { ["script"] = "Write-Output 'Hello from test'" });

        var result = await provider.ExecuteAsync(request);

        Assert.True(result.IsSuccess);
        Assert.Equal(0, result.ExitCode);
        Assert.Equal("Hello from test", result.Output);
        Assert.Equal("Hello from test", result.StdOut);
        Assert.Equal(string.Empty, result.StdErr);
        Assert.Null(result.Error);
    }

    [Fact]
    public async Task ExecuteAsync_CapturesErrorOutput()
    {
        if (!IsWindows) return;

        var provider = new PowerShellToolProvider();
        // Um script que comprovadamente falha e gera erro
        ToolExecutionRequest request = ("powershell", new Dictionary<string, string> { ["script"] = "Write-Error 'Custom Error occurred'" });

        var result = await provider.ExecuteAsync(request);

        Assert.False(result.IsSuccess);
        Assert.NotEqual(0, result.ExitCode);
        Assert.Contains("Custom Error occurred", result.Error);
    }

    [Fact]
    public async Task ExecuteAsync_CapturesStdoutAndStderr_WhenScriptSucceeds()
    {
        if (!IsWindows) return;

        var provider = new PowerShellToolProvider();
        ToolExecutionRequest request = (
            "powershell",
            new Dictionary<string, string>
            {
                ["script"] = "[Console]::Error.WriteLine('warning stream'); Write-Output 'ok stream'"
            });

        var result = await provider.ExecuteAsync(request);

        Assert.True(result.IsSuccess);
        Assert.Equal("ok stream", result.StdOut);
        Assert.Contains("warning stream", result.StdErr);
        Assert.Contains("warning stream", result.Error);
    }

    [Fact]
    public async Task ExecuteAsync_CapturesStdoutAndStderr_WhenScriptFails()
    {
        if (!IsWindows) return;

        var provider = new PowerShellToolProvider();
        ToolExecutionRequest request = (
            "powershell",
            new Dictionary<string, string>
            {
                ["script"] = "Write-Output 'partial out'; [Console]::Error.WriteLine('fatal err'); exit 7"
            });

        var result = await provider.ExecuteAsync(request);

        Assert.False(result.IsSuccess);
        Assert.Equal(7, result.ExitCode);
        Assert.Equal("partial out", result.StdOut);
        Assert.Contains("fatal err", result.StdErr);
    }

    [Fact]
    public async Task ExecuteAsync_ReturnsTimedOut_WhenExecutionExceedsConfiguredTimeout()
    {
        if (!IsWindows) return;

        var provider = new PowerShellToolProvider();
        var request = new ToolExecutionRequest(
            ToolName: "powershell",
            Arguments: new Dictionary<string, string> { ["script"] = "Start-Sleep -Seconds 3; Write-Output 'done'" },
            Timeout: TimeSpan.FromMilliseconds(200));

        var result = await provider.ExecuteAsync(request);

        Assert.False(result.IsSuccess);
        Assert.True(result.IsTimedOut);
        Assert.False(result.IsCancelled);
        Assert.Equal(ToolExecutionResult.TimeoutExitCode, result.ExitCode);
    }

    [Fact]
    public async Task ExecuteAsync_ReturnsCancelledWhenCancelled()
    {
        if (!IsWindows) return;

        var provider = new PowerShellToolProvider();
        using var cts = new CancellationTokenSource();
        await cts.CancelAsync();
        ToolExecutionRequest request = ("powershell", new Dictionary<string, string> { ["script"] = "Start-Sleep -Seconds 5" });

        var result = await provider.ExecuteAsync(request, cts.Token);

        Assert.False(result.IsSuccess);
        Assert.True(result.IsCancelled);
        Assert.False(result.IsTimedOut);
        Assert.Equal(ToolExecutionResult.CancelledExitCode, result.ExitCode);
    }

    [Fact]
    public async Task ExecuteAsync_ReturnsCancelled_WhenTokenIsCancelledDuringExecution()
    {
        if (!IsWindows) return;

        var provider = new PowerShellToolProvider();
        using var cts = new CancellationTokenSource(TimeSpan.FromMilliseconds(200));
        var request = new ToolExecutionRequest(
            ToolName: "powershell",
            Arguments: new Dictionary<string, string> { ["script"] = "Start-Sleep -Seconds 3; Write-Output 'done'" });

        var result = await provider.ExecuteAsync(request, cts.Token);

        Assert.False(result.IsSuccess);
        Assert.True(result.IsCancelled);
        Assert.False(result.IsTimedOut);
        Assert.Equal(ToolExecutionResult.CancelledExitCode, result.ExitCode);
    }
}
