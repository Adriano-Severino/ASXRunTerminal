using ASXRunTerminal.Core;
using ASXRunTerminal.Infra;

namespace ASXRunTerminal.Tests;

public sealed class AgentGovernedToolRuntimeTests
{
    [Fact]
    public async Task ExecuteAsync_WhenToolCallSelfApprovesWithoutSessionApproval_BlocksBeforeInnerRuntime()
    {
        var innerCalls = 0;
        var innerRuntime = new StubToolRuntime((_, _) =>
        {
            innerCalls++;
            return ToolExecutionResult.Success("nao deveria executar", TimeSpan.Zero);
        });
        var runtime = new AgentGovernedToolRuntime(
            innerRuntime,
            hasExplicitSensitiveOperationApproval: false,
            shellPolicyResolver: static () => new ShellCommandPermissionPolicy(allowedCommands: ["rm"]));
        var request = new ToolExecutionRequest(
            ToolName: "shell",
            Arguments: new Dictionary<string, string>
            {
                ["script"] = "rm -rf ./tmp",
                [ShellCommandPermissionPolicy.DestructiveApprovalArgumentName] = "sim"
            });

        var result = await runtime.ExecuteAsync(request);

        Assert.False(result.IsSuccess);
        Assert.Equal(ShellCommandPermissionPolicy.BlockedCommandExitCode, result.ExitCode);
        Assert.Contains(AgentGovernedToolRuntime.SensitiveApprovalFlagName, result.Error);
        Assert.Equal(0, innerCalls);
    }

    [Fact]
    public async Task ExecuteAsync_WhenSensitiveCommandHasSessionAndToolCallApproval_DelegatesToInnerRuntime()
    {
        var innerCalls = 0;
        var innerRuntime = new StubToolRuntime((_, _) =>
        {
            innerCalls++;
            return ToolExecutionResult.Success("executado", TimeSpan.Zero);
        });
        var runtime = new AgentGovernedToolRuntime(
            innerRuntime,
            hasExplicitSensitiveOperationApproval: true,
            shellPolicyResolver: static () => new ShellCommandPermissionPolicy(allowedCommands: ["rm"]));
        var request = new ToolExecutionRequest(
            ToolName: "shell",
            Arguments: new Dictionary<string, string>
            {
                ["script"] = "rm -rf ./tmp",
                [ShellCommandPermissionPolicy.DestructiveApprovalArgumentName] = "sim"
            });

        var result = await runtime.ExecuteAsync(request);

        Assert.True(result.IsSuccess);
        Assert.Equal("executado", result.Output);
        Assert.Equal(1, innerCalls);
    }

    [Fact]
    public async Task ExecuteAsync_WhenSensitiveCommandHasNoToolCallApproval_BlocksBeforeInnerRuntime()
    {
        var innerCalls = 0;
        var innerRuntime = new StubToolRuntime((_, _) =>
        {
            innerCalls++;
            return ToolExecutionResult.Success("nao deveria executar", TimeSpan.Zero);
        });
        var runtime = new AgentGovernedToolRuntime(
            innerRuntime,
            hasExplicitSensitiveOperationApproval: true,
            shellPolicyResolver: static () => new ShellCommandPermissionPolicy(allowedCommands: ["rm"]));
        var request = new ToolExecutionRequest(
            ToolName: "shell",
            Arguments: new Dictionary<string, string>
            {
                ["script"] = "rm -rf ./tmp"
            });

        var result = await runtime.ExecuteAsync(request);

        Assert.False(result.IsSuccess);
        Assert.Equal(ShellCommandPermissionPolicy.BlockedCommandExitCode, result.ExitCode);
        Assert.Contains("aprovacao explicita", result.Error, StringComparison.OrdinalIgnoreCase);
        Assert.Equal(0, innerCalls);
    }

    [Fact]
    public async Task ExecuteAsync_WhenShellCommandIsNotSensitive_DelegatesWithoutSessionApproval()
    {
        var innerCalls = 0;
        var innerRuntime = new StubToolRuntime((_, _) =>
        {
            innerCalls++;
            return ToolExecutionResult.Success("ok", TimeSpan.Zero);
        });
        var runtime = new AgentGovernedToolRuntime(
            innerRuntime,
            hasExplicitSensitiveOperationApproval: false,
            shellPolicyResolver: static () => ShellCommandPermissionPolicy.Default);
        var request = new ToolExecutionRequest(
            ToolName: "shell",
            Arguments: new Dictionary<string, string>
            {
                ["script"] = "echo ok"
            });

        var result = await runtime.ExecuteAsync(request);

        Assert.True(result.IsSuccess);
        Assert.Equal("ok", result.Output);
        Assert.Equal(1, innerCalls);
    }

    private sealed class StubToolRuntime(
        Func<ToolExecutionRequest, CancellationToken, ToolExecutionResult> execute) : IToolRuntime
    {
        public IReadOnlyList<ToolDescriptor> ListTools()
        {
            return Array.Empty<ToolDescriptor>();
        }

        public Task<ToolExecutionResult> ExecuteAsync(
            ToolExecutionRequest request,
            CancellationToken cancellationToken = default)
        {
            return Task.FromResult(execute(request, cancellationToken));
        }
    }
}
