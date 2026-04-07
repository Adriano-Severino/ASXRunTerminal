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
        Assert.Null(result.Error);
        Assert.Equal(0, result.ExitCode);
        Assert.Equal(duration, result.Duration);
    }

    [Fact]
    public void ToolExecutionResult_FailureFactory_SetsExpectedValues()
    {
        var duration = TimeSpan.FromMilliseconds(10);
        var result = ToolExecutionResult.Failure("erro inesperado", exitCode: 1, duration);

        Assert.False(result.IsSuccess);
        Assert.Equal(string.Empty, result.Output);
        Assert.Equal("erro inesperado", result.Error);
        Assert.Equal(1, result.ExitCode);
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
    public async Task EchoToolProvider_ExecuteAsync_ThrowsWhenCancelled()
    {
        var provider = new EchoToolProvider();
        using var cts = new CancellationTokenSource();
        await cts.CancelAsync();
        var request = new ToolExecutionRequest(
            "echo",
            new Dictionary<string, string> { ["text"] = "test" });

        await Assert.ThrowsAnyAsync<OperationCanceledException>(
            () => provider.ExecuteAsync(request, cts.Token));
    }

    // ── ToolRuntime ──

    [Fact]
    public void ToolRuntime_ListTools_AggregatesAllProviders()
    {
        var runtime = new ToolRuntime(new EchoToolProvider(), new StubToolProvider("stub-tool"));

        var tools = runtime.ListTools();

        Assert.Equal(2, tools.Count);
        Assert.Equal("echo", tools[0].Name);
        Assert.Equal("stub-tool", tools[1].Name);
    }

    [Fact]
    public void ToolRuntime_ListTools_ReturnsEmpty_WhenNoProvidersRegistered()
    {
        var runtime = new ToolRuntime(Array.Empty<IToolProvider>());

        var tools = runtime.ListTools();

        Assert.Empty(tools);
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
    public void ToolRuntime_Constructor_ThrowsWhenProvidersIsNull()
    {
        Assert.Throws<ArgumentNullException>(
            () => new ToolRuntime((IReadOnlyList<IToolProvider>)null!));
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
}
