using System.Runtime.InteropServices;
using ASXRunTerminal.Core;
using ASXRunTerminal.Infra;

namespace ASXRunTerminal.Tests;

public sealed class UnixShellToolProviderTests
{
    private static bool IsUnix => RuntimeInformation.IsOSPlatform(OSPlatform.Linux) || RuntimeInformation.IsOSPlatform(OSPlatform.OSX);

    [Fact]
    public void ProviderName_ReturnsShell()
    {
        var provider = new UnixShellToolProvider();
        Assert.Equal("shell", provider.ProviderName);
    }

    [Fact]
    public void ListTools_ReturnsTools_OnlyOnUnix()
    {
        var provider = new UnixShellToolProvider();

        var tools = provider.ListTools();

        if (IsUnix)
        {
            Assert.Equal(2, tools.Count);
            Assert.Contains(tools, t => t.Name == "bash");
            Assert.Contains(tools, t => t.Name == "zsh");
            Assert.Equal("script", tools[0].Parameters[0].Name);
            Assert.True(tools[0].Parameters[0].IsRequired);
        }
        else
        {
            Assert.Empty(tools);
        }
    }

    [Fact]
    public void CanHandle_ReturnsTrue_OnlyForBashAndZshOnUnix()
    {
        var provider = new UnixShellToolProvider();

        if (IsUnix)
        {
            Assert.True(provider.CanHandle("bash"));
            Assert.True(provider.CanHandle("BASH"));
            Assert.True(provider.CanHandle("zsh"));
            Assert.False(provider.CanHandle("powershell"));
        }
        else
        {
            Assert.False(provider.CanHandle("bash"));
            Assert.False(provider.CanHandle("zsh"));
        }
    }

    [Fact]
    public async Task ExecuteAsync_ReturnsFailure_WhenNotOnUnix()
    {
        if (IsUnix)
        {
            return; // Teste irrelevante no Unix
        }

        var provider = new UnixShellToolProvider();
        ToolExecutionRequest request = ("bash", new Dictionary<string, string> { ["script"] = "echo 'test'" });

        var result = await provider.ExecuteAsync(request);

        Assert.False(result.IsSuccess);
        Assert.Equal(1, result.ExitCode);
        Assert.Contains("suportado no Linux e macOS", result.Error);
    }

    [Fact]
    public async Task ExecuteAsync_ReturnsFailure_WhenScriptIsMissing()
    {
        if (!IsUnix) return;

        var provider = new UnixShellToolProvider();
        ToolExecutionRequest request = ("bash", new Dictionary<string, string>());

        var result = await provider.ExecuteAsync(request);

        Assert.False(result.IsSuccess);
        Assert.Equal(1, result.ExitCode);
        Assert.Contains("script", result.Error);
    }

    [Fact]
    public async Task ExecuteAsync_ReturnsFailure_WhenScriptIsBlank()
    {
        if (!IsUnix) return;

        var provider = new UnixShellToolProvider();
        ToolExecutionRequest request = ("bash", new Dictionary<string, string> { ["script"] = "   " });

        var result = await provider.ExecuteAsync(request);

        Assert.False(result.IsSuccess);
        Assert.Equal(1, result.ExitCode);
        Assert.Contains("script", result.Error);
    }

    [Fact]
    public async Task ExecuteAsync_ExecutesScriptSuccessfully()
    {
        if (!IsUnix) return;

        var provider = new UnixShellToolProvider();
        ToolExecutionRequest request = ("bash", new Dictionary<string, string> { ["script"] = "echo 'Hello from test'" });

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
        if (!IsUnix) return;

        var provider = new UnixShellToolProvider();
        ToolExecutionRequest request = ("bash", new Dictionary<string, string> { ["script"] = ">&2 echo 'Custom Error occurred'; exit 1" });

        var result = await provider.ExecuteAsync(request);

        Assert.False(result.IsSuccess);
        Assert.NotEqual(0, result.ExitCode);
        Assert.Contains("Custom Error occurred", result.Error);
    }

    [Fact]
    public async Task ExecuteAsync_CapturesStdoutAndStderr_WhenScriptSucceeds()
    {
        if (!IsUnix) return;

        var provider = new UnixShellToolProvider();
        ToolExecutionRequest request = (
            "bash",
            new Dictionary<string, string>
            {
                ["script"] = "echo 'ok stream'; >&2 echo 'warning stream'"
            });

        var result = await provider.ExecuteAsync(request);

        Assert.True(result.IsSuccess);
        Assert.Equal("ok stream", result.StdOut);
        Assert.Contains("warning stream", result.StdErr);
    }

    [Fact]
    public async Task ExecuteAsync_CapturesStdoutAndStderr_WhenScriptFails()
    {
        if (!IsUnix) return;

        var provider = new UnixShellToolProvider();
        ToolExecutionRequest request = (
            "bash",
            new Dictionary<string, string>
            {
                ["script"] = "echo 'partial out'; >&2 echo 'fatal err'; exit 7"
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
        if (!IsUnix) return;

        var provider = new UnixShellToolProvider();
        var request = new ToolExecutionRequest(
            ToolName: "bash",
            Arguments: new Dictionary<string, string> { ["script"] = "sleep 3; echo done" },
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
        if (!IsUnix) return;

        var provider = new UnixShellToolProvider();
        using var cts = new CancellationTokenSource();
        await cts.CancelAsync();
        ToolExecutionRequest request = ("bash", new Dictionary<string, string> { ["script"] = "sleep 5" });

        var result = await provider.ExecuteAsync(request, cts.Token);

        Assert.False(result.IsSuccess);
        Assert.True(result.IsCancelled);
        Assert.False(result.IsTimedOut);
        Assert.Equal(ToolExecutionResult.CancelledExitCode, result.ExitCode);
    }

    [Fact]
    public async Task ExecuteAsync_ReturnsCancelled_WhenTokenIsCancelledDuringExecution()
    {
        if (!IsUnix) return;

        var provider = new UnixShellToolProvider();
        using var cts = new CancellationTokenSource(TimeSpan.FromMilliseconds(200));
        var request = new ToolExecutionRequest(
            ToolName: "bash",
            Arguments: new Dictionary<string, string> { ["script"] = "sleep 3; echo done" });

        var result = await provider.ExecuteAsync(request, cts.Token);

        Assert.False(result.IsSuccess);
        Assert.True(result.IsCancelled);
        Assert.False(result.IsTimedOut);
        Assert.Equal(ToolExecutionResult.CancelledExitCode, result.ExitCode);
    }
}
