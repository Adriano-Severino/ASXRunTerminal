using System.Runtime.InteropServices;
using ASXRunTerminal.Config;
using ASXRunTerminal.Core;

namespace ASXRunTerminal.Infra;

internal sealed class PowerShellToolProvider : IToolProvider
{
    private const string ToolName = "powershell";
    private readonly Func<ShellCommandPermissionPolicy> _policyResolver;

    private static readonly ToolDescriptor Descriptor = new(
        Name: ToolName,
        Description: "Executa scripts PowerShell localmente no Windows.",
        Parameters:
        [
            new ToolParameter(
                Name: "script",
                Description: "O script ou comando PowerShell a ser executado.",
                IsRequired: true),
            new ToolParameter(
                Name: ShellCommandPermissionPolicy.DestructiveApprovalArgumentName,
                Description: "Aprovacao explicita para comandos destrutivos bloqueados por padrao (use 'sim').",
                IsRequired: false)
        ]);

    public string ProviderName => "shell";

    public PowerShellToolProvider()
        : this(ResolvePolicyForCurrentWorkspace)
    {
    }

    internal PowerShellToolProvider(Func<ShellCommandPermissionPolicy> policyResolver)
    {
        _policyResolver = policyResolver ?? throw new ArgumentNullException(nameof(policyResolver));
    }

    public IReadOnlyList<ToolDescriptor> ListTools()
    {
        if (RuntimeInformation.IsOSPlatform(OSPlatform.Windows))
        {
            return [Descriptor];
        }
        return [];
    }

    public bool CanHandle(string toolName)
    {
        return RuntimeInformation.IsOSPlatform(OSPlatform.Windows) &&
               string.Equals(toolName, ToolName, StringComparison.OrdinalIgnoreCase);
    }

    public async Task<ToolExecutionResult> ExecuteAsync(
        ToolExecutionRequest request,
        CancellationToken cancellationToken = default)
    {
        if (cancellationToken.IsCancellationRequested)
        {
            return ToolExecutionResult.Cancelled(TimeSpan.Zero);
        }

        if (!RuntimeInformation.IsOSPlatform(OSPlatform.Windows))
        {
            return ToolExecutionResult.Failure(
                error: "O PowerShellToolProvider so e suportado no Windows.",
                exitCode: 1,
                duration: TimeSpan.Zero);
        }

        if (!request.Arguments.TryGetValue("script", out var script) || string.IsNullOrWhiteSpace(script))
        {
            return ToolExecutionResult.Failure(
                error: "Parametro obrigatorio 'script' nao foi informado.",
                exitCode: 1,
                duration: TimeSpan.Zero);
        }

        var hasExplicitDestructiveApproval =
            ShellCommandGuardrailEvaluator.HasExplicitDestructiveCommandApproval(request.Arguments);
        var guardrailResult = ShellCommandGuardrailEvaluator.ValidateScript(
            toolName: request.ToolName,
            script: script,
            policyResolver: _policyResolver,
            isDestructiveCommandApproved: hasExplicitDestructiveApproval);
        if (guardrailResult is ToolExecutionResult blockedExecutionResult)
        {
            return blockedExecutionResult;
        }

        var executor = new ShellProcessExecutor();

        try
        {
            var result = await executor.ExecuteAsync(
                fileName: "powershell.exe",
                arguments: "-NoProfile -NonInteractive -Command \"-\"",
                input: script,
                timeout: request.Timeout,
                cancellationToken: cancellationToken);

            if (result.IsTimedOut)
            {
                return ToolExecutionResult.TimedOut(
                    duration: result.Duration,
                    stdOut: result.StdOut,
                    stdErr: result.StdErr);
            }

            if (result.IsCancelled)
            {
                return ToolExecutionResult.Cancelled(
                    duration: result.Duration,
                    stdOut: result.StdOut,
                    stdErr: result.StdErr);
            }

            if (result.ExitCode != 0)
            {
                var errorMessage = string.IsNullOrWhiteSpace(result.StdErr)
                    ? "Erro na execucao do script."
                    : result.StdErr;
                return ToolExecutionResult.Failure(
                    error: errorMessage,
                    exitCode: result.ExitCode,
                    duration: result.Duration,
                    stdOut: result.StdOut);
            }

            return ToolExecutionResult.Success(
                output: result.StdOut,
                duration: result.Duration,
                stdErr: result.StdErr);
        }
        catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
        {
            return ToolExecutionResult.Cancelled(TimeSpan.Zero);
        }
        catch (Exception ex)
        {
            return ToolExecutionResult.Failure(
                error: $"Erro ao executar PowerShell: {ex.Message}",
                exitCode: 1,
                duration: TimeSpan.Zero);
        }
    }

    private static ShellCommandPermissionPolicy ResolvePolicyForCurrentWorkspace()
    {
        var workspaceRoot = WorkspaceRootDetector.Resolve();
        return ShellCommandPermissionPolicyFile.Load(workspaceRoot.DirectoryPath);
    }
}
