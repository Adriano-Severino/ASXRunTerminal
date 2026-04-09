using System.Runtime.InteropServices;
using ASXRunTerminal.Core;
using ASXRunTerminal.Infra;

namespace ASXRunTerminal.Tests;

public sealed class ToolRuntimeTests
{
    // ── ToolParameter ──

    [Fact]
    public void ToolParameter_StoresNameDescriptionAndRequired()
    {
        var param = new ToolParameter(
            Name: "path",
            Description: "Caminho do arquivo.",
            IsRequired: true);

        Assert.Equal("path", param.Name);
        Assert.Equal("Caminho do arquivo.", param.Description);
        Assert.True(param.IsRequired);
    }

    [Fact]
    public void ToolParameter_EqualityByValue()
    {
        var a = new ToolParameter("x", "desc", true);
        var b = new ToolParameter("x", "desc", true);
        var c = new ToolParameter("y", "desc", true);

        Assert.Equal(a, b);
        Assert.NotEqual(a, c);
    }

    // ── ToolDescriptor ──

    [Fact]
    public void ToolDescriptor_StoresNameDescriptionAndParameters()
    {
        var parameters = new[]
        {
            new ToolParameter("a", "param a", true),
            new ToolParameter("b", "param b", false)
        };

        var descriptor = new ToolDescriptor(
            Name: "my-tool",
            Description: "Ferramenta de teste.",
            Parameters: parameters);

        Assert.Equal("my-tool", descriptor.Name);
        Assert.Equal("Ferramenta de teste.", descriptor.Description);
        Assert.Equal(2, descriptor.Parameters.Count);
        Assert.Equal("a", descriptor.Parameters[0].Name);
        Assert.True(descriptor.Parameters[0].IsRequired);
        Assert.False(descriptor.Parameters[1].IsRequired);
    }

    // ── ToolExecutionRequest ──

    [Fact]
    public void ToolExecutionRequest_StoresToolNameAndArguments()
    {
        var args = new Dictionary<string, string> { ["text"] = "hello" };
        var request = new ToolExecutionRequest("echo", args);

        Assert.Equal("echo", request.ToolName);
        Assert.Equal("hello", request.Arguments["text"]);
    }

    [Fact]
    public void ToolExecutionRequest_ImplicitOperatorFromTuple()
    {
        ToolExecutionRequest request = ("echo", new Dictionary<string, string> { ["text"] = "hi" });

        Assert.Equal("echo", request.ToolName);
        Assert.Equal("hi", request.Arguments["text"]);
    }

    // ── ToolExecutionResult ──

    [Fact]
    public void ToolExecutionResult_SuccessFactory_SetsExpectedValues()
    {
        var duration = TimeSpan.FromMilliseconds(42);
        var result = ToolExecutionResult.Success("output text", duration);

        Assert.True(result.IsSuccess);
        Assert.Equal("output text", result.Output);
        Assert.Equal("output text", result.StdOut);
        Assert.Null(result.Error);
        Assert.Equal(string.Empty, result.StdErr);
        Assert.Equal(0, result.ExitCode);
        Assert.Equal(duration, result.Duration);
        Assert.False(result.IsTimedOut);
        Assert.False(result.IsCancelled);
    }

    [Fact]
    public void ToolExecutionResult_FailureFactory_SetsExpectedValues()
    {
        var duration = TimeSpan.FromMilliseconds(10);
        var result = ToolExecutionResult.Failure("erro inesperado", exitCode: 1, duration);

        Assert.False(result.IsSuccess);
        Assert.Equal(string.Empty, result.Output);
        Assert.Equal(string.Empty, result.StdOut);
        Assert.Equal("erro inesperado", result.Error);
        Assert.Equal("erro inesperado", result.StdErr);
        Assert.Equal(1, result.ExitCode);
        Assert.Equal(duration, result.Duration);
        Assert.False(result.IsTimedOut);
        Assert.False(result.IsCancelled);
    }

    [Fact]
    public void ToolExecutionResult_TimedOutFactory_SetsExpectedValues()
    {
        var duration = TimeSpan.FromMilliseconds(100);
        var result = ToolExecutionResult.TimedOut(
            duration: duration,
            stdOut: "partial output",
            stdErr: "timeout");

        Assert.False(result.IsSuccess);
        Assert.True(result.IsTimedOut);
        Assert.False(result.IsCancelled);
        Assert.Equal("partial output", result.StdOut);
        Assert.Equal("timeout", result.StdErr);
        Assert.Equal(ToolExecutionResult.TimeoutExitCode, result.ExitCode);
        Assert.Equal(duration, result.Duration);
    }

    [Fact]
    public void ToolExecutionResult_CancelledFactory_SetsExpectedValues()
    {
        var duration = TimeSpan.FromMilliseconds(80);
        var result = ToolExecutionResult.Cancelled(
            duration: duration,
            stdOut: "partial output",
            stdErr: "cancelled");

        Assert.False(result.IsSuccess);
        Assert.False(result.IsTimedOut);
        Assert.True(result.IsCancelled);
        Assert.Equal("partial output", result.StdOut);
        Assert.Equal("cancelled", result.StdErr);
        Assert.Equal(ToolExecutionResult.CancelledExitCode, result.ExitCode);
        Assert.Equal(duration, result.Duration);
    }

    [Fact]
    public void ToolExecutionResult_ImplicitOperatorFromString_MapsToSuccess()
    {
        ToolExecutionResult result = "resultado simples";

        Assert.True(result.IsSuccess);
        Assert.Equal("resultado simples", result.Output);
        Assert.Null(result.Error);
        Assert.Equal(0, result.ExitCode);
        Assert.Equal(TimeSpan.Zero, result.Duration);
        Assert.False(result.IsTimedOut);
        Assert.False(result.IsCancelled);
    }

    // ── EchoToolProvider ──

    [Fact]
    public void EchoToolProvider_ProviderName_ReturnsBuiltIn()
    {
        var provider = new EchoToolProvider();

        Assert.Equal("built-in", provider.ProviderName);
    }

    [Fact]
    public void EchoToolProvider_ListTools_ReturnsEchoDescriptor()
    {
        var provider = new EchoToolProvider();

        var tools = provider.ListTools();

        Assert.Single(tools);
        Assert.Equal("echo", tools[0].Name);
        Assert.Single(tools[0].Parameters);
        Assert.Equal("text", tools[0].Parameters[0].Name);
        Assert.True(tools[0].Parameters[0].IsRequired);
    }

    [Fact]
    public void EchoToolProvider_CanHandle_ReturnsTrueForEcho()
    {
        var provider = new EchoToolProvider();

        Assert.True(provider.CanHandle("echo"));
        Assert.True(provider.CanHandle("ECHO"));
        Assert.True(provider.CanHandle("Echo"));
    }

    [Fact]
    public void EchoToolProvider_CanHandle_ReturnsFalseForOtherTools()
    {
        var provider = new EchoToolProvider();

        Assert.False(provider.CanHandle("shell"));
        Assert.False(provider.CanHandle(""));
        Assert.False(provider.CanHandle("ech"));
    }

    [Fact]
    public async Task EchoToolProvider_ExecuteAsync_ReturnsInputText()
    {
        var provider = new EchoToolProvider();
        var request = new ToolExecutionRequest(
            "echo",
            new Dictionary<string, string> { ["text"] = "  hello world  " });

        var result = await provider.ExecuteAsync(request);

        Assert.True(result.IsSuccess);
        Assert.Equal("hello world", result.Output);
        Assert.Null(result.Error);
        Assert.Equal(0, result.ExitCode);
    }

    [Fact]
    public async Task EchoToolProvider_ExecuteAsync_ReturnsFailure_WhenTextIsMissing()
    {
        var provider = new EchoToolProvider();
        var request = new ToolExecutionRequest(
            "echo",
            new Dictionary<string, string>());

        var result = await provider.ExecuteAsync(request);

        Assert.False(result.IsSuccess);
        Assert.Contains("text", result.Error);
        Assert.Equal(1, result.ExitCode);
    }

    [Fact]
    public async Task EchoToolProvider_ExecuteAsync_ReturnsFailure_WhenTextIsBlank()
    {
        var provider = new EchoToolProvider();
        var request = new ToolExecutionRequest(
            "echo",
            new Dictionary<string, string> { ["text"] = "   " });

        var result = await provider.ExecuteAsync(request);

        Assert.False(result.IsSuccess);
        Assert.Contains("text", result.Error);
    }

    [Fact]
    public async Task EchoToolProvider_ExecuteAsync_ReturnsCancelledWhenCancelled()
    {
        var provider = new EchoToolProvider();
        using var cts = new CancellationTokenSource();
        await cts.CancelAsync();
        var request = new ToolExecutionRequest(
            "echo",
            new Dictionary<string, string> { ["text"] = "test" });

        var result = await provider.ExecuteAsync(request, cts.Token);

        Assert.False(result.IsSuccess);
        Assert.True(result.IsCancelled);
        Assert.Equal(ToolExecutionResult.CancelledExitCode, result.ExitCode);
    }

    // ── ToolRuntime ──

    [Fact]
    public void ToolRuntime_ListTools_AggregatesAllProviders()
    {
        var runtime = new ToolRuntime(
            [new EchoToolProvider(), new StubToolProvider("stub-tool")],
            defaultShellSelector: () => null);

        var tools = runtime.ListTools();

        Assert.Equal(2, tools.Count);
        Assert.Equal("echo", tools[0].Name);
        Assert.Equal("stub-tool", tools[1].Name);
    }

    [Fact]
    public void ToolRuntime_ListTools_ReturnsEmpty_WhenNoProvidersRegistered()
    {
        var runtime = new ToolRuntime(
            Array.Empty<IToolProvider>(),
            defaultShellSelector: () => null);

        var tools = runtime.ListTools();

        Assert.Empty(tools);
    }

    [Fact]
    public void ToolRuntime_ListTools_IncludesShellAlias_WhenDefaultShellIsAvailable()
    {
        var runtime = new ToolRuntime(
            [new StubToolProvider("bash")],
            defaultShellSelector: () => "bash");

        var tools = runtime.ListTools();

        Assert.Equal(2, tools.Count);
        var shellTool = Assert.Single(
            tools,
            static tool => string.Equals(tool.Name, "shell", StringComparison.OrdinalIgnoreCase));
        Assert.Contains("bash", shellTool.Description);
        Assert.Single(shellTool.Parameters);
        Assert.Equal("script", shellTool.Parameters[0].Name);
        Assert.True(shellTool.Parameters[0].IsRequired);
    }

    [Fact]
    public async Task ToolRuntime_ExecuteAsync_DispatchesToCorrectProvider()
    {
        var runtime = new ToolRuntime(new EchoToolProvider(), new StubToolProvider("other"));
        var request = new ToolExecutionRequest(
            "echo",
            new Dictionary<string, string> { ["text"] = "dispatched" });

        var result = await runtime.ExecuteAsync(request);

        Assert.True(result.IsSuccess);
        Assert.Equal("dispatched", result.Output);
    }

    [Fact]
    public async Task ToolRuntime_ExecuteAsync_DispatchesToStubProvider()
    {
        var runtime = new ToolRuntime(new EchoToolProvider(), new StubToolProvider("stub-tool"));
        var request = new ToolExecutionRequest(
            "stub-tool",
            new Dictionary<string, string> { ["input"] = "value" });

        var result = await runtime.ExecuteAsync(request);

        Assert.True(result.IsSuccess);
        Assert.Equal("stub:value", result.Output);
    }

    [Fact]
    public async Task ToolRuntime_ExecuteAsync_ReturnsFailure_WhenNoProviderHandlesTool()
    {
        var runtime = new ToolRuntime(new EchoToolProvider());
        var request = new ToolExecutionRequest(
            "unknown-tool",
            new Dictionary<string, string>());

        var result = await runtime.ExecuteAsync(request);

        Assert.False(result.IsSuccess);
        Assert.Contains("unknown-tool", result.Error);
        Assert.Equal(127, result.ExitCode);
    }

    [Fact]
    public async Task ToolRuntime_ExecuteAsync_UsesFirstMatchingProvider()
    {
        var firstEcho = new EchoToolProvider();
        var secondEcho = new StubToolProvider("echo");
        var runtime = new ToolRuntime(firstEcho, secondEcho);
        var request = new ToolExecutionRequest(
            "echo",
            new Dictionary<string, string> { ["text"] = "first wins" });

        var result = await runtime.ExecuteAsync(request);

        Assert.True(result.IsSuccess);
        Assert.Equal("first wins", result.Output);
    }

    [Fact]
    public async Task ToolRuntime_ExecuteAsync_ForShellAlias_DispatchesToResolvedShellProvider()
    {
        var runtime = new ToolRuntime(
            [new StubToolProvider("bash")],
            defaultShellSelector: () => "bash");
        var request = new ToolExecutionRequest(
            "shell",
            new Dictionary<string, string> { ["input"] = "resolved" });

        var result = await runtime.ExecuteAsync(request);

        Assert.True(result.IsSuccess);
        Assert.Equal("stub:resolved", result.Output);
    }

    [Fact]
    public async Task ToolRuntime_ExecuteAsync_ForShellAlias_ReturnsFailure_WhenShellCannotBeDetected()
    {
        var runtime = new ToolRuntime(
            [new StubToolProvider("bash")],
            defaultShellSelector: () => null);
        var request = new ToolExecutionRequest(
            "shell",
            new Dictionary<string, string> { ["input"] = "ignored" });

        var result = await runtime.ExecuteAsync(request);

        Assert.False(result.IsSuccess);
        Assert.Equal(127, result.ExitCode);
        Assert.Contains("shell padrao", result.Error);
    }

    [Fact]
    public async Task ToolRuntime_ExecuteAsync_ReturnsFailure_WhenTimeoutIsNonPositive()
    {
        var runtime = new ToolRuntime(new EchoToolProvider());
        var request = new ToolExecutionRequest(
            ToolName: "echo",
            Arguments: new Dictionary<string, string> { ["text"] = "hello" },
            Timeout: TimeSpan.Zero);

        var result = await runtime.ExecuteAsync(request);

        Assert.False(result.IsSuccess);
        Assert.Contains("timeout", result.Error, StringComparison.OrdinalIgnoreCase);
        Assert.Equal(1, result.ExitCode);
    }

    [Fact]
    public async Task ToolRuntime_ExecuteAsync_ReturnsCancelled_WhenTokenIsAlreadyCancelled()
    {
        var runtime = new ToolRuntime(new EchoToolProvider());
        using var cts = new CancellationTokenSource();
        await cts.CancelAsync();
        var request = new ToolExecutionRequest(
            "echo",
            new Dictionary<string, string> { ["text"] = "ignored" });

        var result = await runtime.ExecuteAsync(request, cts.Token);

        Assert.False(result.IsSuccess);
        Assert.True(result.IsCancelled);
        Assert.Equal(ToolExecutionResult.CancelledExitCode, result.ExitCode);
    }

    [Fact]
    public void ToolRuntime_Constructor_ThrowsWhenProvidersIsNull()
    {
        Assert.Throws<ArgumentNullException>(
            () => new ToolRuntime((IReadOnlyList<IToolProvider>)null!));
    }

    [Fact]
    public void ToolRuntime_Constructor_ThrowsWhenProvidersContainsNullItem()
    {
        IReadOnlyList<IToolProvider> providers =
        [
            new EchoToolProvider(),
            null!
        ];

        Assert.Throws<ArgumentException>(
            () => new ToolRuntime(providers));
    }

    // ── ShellEnvironmentDetector ──

    [Theory]
    [InlineData("windows", "powershell")]
    [InlineData("linux", "bash")]
    [InlineData("osx", "zsh")]
    public void ShellEnvironmentDetector_ResolveDefaultShell_ReturnsExpectedByPlatform(
        string platform,
        string expectedShell)
    {
        var actualShell = ShellEnvironmentDetector.ResolveDefaultShell(
            CreatePlatformDetector(platform));

        Assert.Equal(expectedShell, actualShell);
    }

    [Fact]
    public void ShellEnvironmentDetector_ResolveDefaultShell_ReturnsNull_WhenPlatformIsUnknown()
    {
        var actualShell = ShellEnvironmentDetector.ResolveDefaultShell(
            static _ => false);

        Assert.Null(actualShell);
    }

    // ── Stub provider for testing ──

    private sealed class StubToolProvider(string toolName) : IToolProvider
    {
        public string ProviderName => "stub";

        public IReadOnlyList<ToolDescriptor> ListTools()
        {
            return
            [
                new ToolDescriptor(
                    Name: toolName,
                    Description: $"Stub tool: {toolName}",
                    Parameters: [new ToolParameter("input", "Input value", true)])
            ];
        }

        public bool CanHandle(string name)
        {
            return string.Equals(name, toolName, StringComparison.OrdinalIgnoreCase);
        }

        public Task<ToolExecutionResult> ExecuteAsync(
            ToolExecutionRequest request,
            CancellationToken cancellationToken = default)
        {
            cancellationToken.ThrowIfCancellationRequested();

            request.Arguments.TryGetValue("input", out var input);
            return Task.FromResult(
                ToolExecutionResult.Success($"stub:{input}", TimeSpan.Zero));
        }
    }

    private static Func<OSPlatform, bool> CreatePlatformDetector(string platform)
    {
        return osPlatform => platform switch
        {
            "windows" => osPlatform == OSPlatform.Windows,
            "linux" => osPlatform == OSPlatform.Linux,
            "osx" => osPlatform == OSPlatform.OSX,
            _ => false
        };
    }
}
