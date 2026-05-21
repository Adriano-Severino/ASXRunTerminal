using ASXRunTerminal.Config;
using ASXRunTerminal.Core;

namespace ASXRunTerminal.Infra;

internal sealed class AgentGovernedToolRuntime : IToolRuntime
{
    internal const string SensitiveApprovalFlagName = "--approve-sensitive";

    private static readonly IReadOnlySet<string> ShellToolNames = new HashSet<string>(
        [
            "shell",
            "powershell",
            "bash",
            "zsh"
        ],
        StringComparer.OrdinalIgnoreCase);

    private readonly IToolRuntime _innerRuntime;
    private readonly bool _hasExplicitSensitiveOperationApproval;
    private readonly Func<ShellCommandPermissionPolicy> _shellPolicyResolver;

    public AgentGovernedToolRuntime(
        IToolRuntime innerRuntime,
        bool hasExplicitSensitiveOperationApproval,
        Func<ShellCommandPermissionPolicy>? shellPolicyResolver = null)
    {
        _innerRuntime = innerRuntime ?? throw new ArgumentNullException(nameof(innerRuntime));
        _hasExplicitSensitiveOperationApproval = hasExplicitSensitiveOperationApproval;
        _shellPolicyResolver = shellPolicyResolver ?? ResolvePolicyForCurrentWorkspace;
    }

    public IReadOnlyList<ToolDescriptor> ListTools()
    {
        return _innerRuntime.ListTools();
    }

    public Task<ToolExecutionResult> ExecuteAsync(
        ToolExecutionRequest request,
        CancellationToken cancellationToken = default)
    {
        if (!RequiresAgentShellGovernance(request))
        {
            return _innerRuntime.ExecuteAsync(request, cancellationToken);
        }

        var hasToolCallApproval = ShellCommandGuardrailEvaluator
            .HasExplicitDestructiveCommandApproval(request.Arguments);
        if (hasToolCallApproval && !_hasExplicitSensitiveOperationApproval)
        {
            return Task.FromResult(ToolExecutionResult.Failure(
                error:
                "Aprovacao destrutiva nao autorizada no modo agente. " +
                $"O argumento '{ShellCommandPermissionPolicy.DestructiveApprovalArgumentName}=sim' " +
                $"so e aceito quando a sessao foi iniciada com '{SensitiveApprovalFlagName}'.",
                exitCode: ShellCommandPermissionPolicy.BlockedCommandExitCode,
                duration: TimeSpan.Zero));
        }

        var guardrailResult = ShellCommandGuardrailEvaluator.ValidateScript(
            toolName: request.ToolName,
            script: request.Arguments["script"],
            policyResolver: _shellPolicyResolver,
            isDestructiveCommandApproved: hasToolCallApproval && _hasExplicitSensitiveOperationApproval);
        if (guardrailResult is ToolExecutionResult blockedResult)
        {
            return Task.FromResult(blockedResult);
        }

        return _innerRuntime.ExecuteAsync(request, cancellationToken);
    }

    private static bool RequiresAgentShellGovernance(ToolExecutionRequest request)
    {
        return ShellToolNames.Contains(request.ToolName)
            && request.Arguments is not null
            && request.Arguments.TryGetValue("script", out var script)
            && !string.IsNullOrWhiteSpace(script);
    }

    private static ShellCommandPermissionPolicy ResolvePolicyForCurrentWorkspace()
    {
        var workspaceRoot = WorkspaceRootDetector.Resolve();
        return ShellCommandPermissionPolicyFile.Load(workspaceRoot.DirectoryPath);
    }
}
