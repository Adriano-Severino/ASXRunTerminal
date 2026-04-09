using System.Runtime.InteropServices;
using ASXRunTerminal.Core;

namespace ASXRunTerminal.Infra;

internal sealed class UnixShellToolProvider : IToolProvider
{
    private static readonly ToolDescriptor BashDescriptor = new(
        Name: "bash",
        Description: "Executa scripts Bash localmente no Linux/macOS.",
        Parameters:
        [
            new ToolParameter(
                Name: "script",
                Description: "O script ou comando Bash a ser executado.",
                IsRequired: true)
        ]);

    private static readonly ToolDescriptor ZshDescriptor = new(
        Name: "zsh",
        Description: "Executa scripts Zsh localmente no Linux/macOS.",
        Parameters:
        [
            new ToolParameter(
                Name: "script",
                Description: "O script ou comando Zsh a ser executado.",
                IsRequired: true)
        ]);

    public string ProviderName => "shell";

    public IReadOnlyList<ToolDescriptor> ListTools()
    {
        if (RuntimeInformation.IsOSPlatform(OSPlatform.Linux) || RuntimeInformation.IsOSPlatform(OSPlatform.OSX))
        {
            return [BashDescriptor, ZshDescriptor];
        }
        return [];
    }

    public bool CanHandle(string toolName)
    {
        return (RuntimeInformation.IsOSPlatform(OSPlatform.Linux) || RuntimeInformation.IsOSPlatform(OSPlatform.OSX)) &&
               (string.Equals(toolName, "bash", StringComparison.OrdinalIgnoreCase) ||
                string.Equals(toolName, "zsh", StringComparison.OrdinalIgnoreCase));
    }

    public async Task<ToolExecutionResult> ExecuteAsync(
        ToolExecutionRequest request,
        CancellationToken cancellationToken = default)
    {
        if (cancellationToken.IsCancellationRequested)
        {
            return ToolExecutionResult.Cancelled(TimeSpan.Zero);
        }

        if (!RuntimeInformation.IsOSPlatform(OSPlatform.Linux) && !RuntimeInformation.IsOSPlatform(OSPlatform.OSX))
        {
            return ToolExecutionResult.Failure(
                error: "O UnixShellToolProvider so e suportado no Linux e macOS.",
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

        string shellInterpreter = string.Equals(request.ToolName, "zsh", StringComparison.OrdinalIgnoreCase) ? "zsh" : "bash";
        var executor = new ShellProcessExecutor();

        try
        {
            var result = await executor.ExecuteAsync(
                fileName: shellInterpreter,
                arguments: "-s",
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
                error: $"Erro ao executar {shellInterpreter}: {ex.Message}",
                exitCode: 1,
                duration: TimeSpan.Zero);
        }
    }
}
