using System.Diagnostics;
using System.Text;
using ASXRunTerminal.Core;

namespace ASXRunTerminal.Infra;

internal record ProcessExecutionResult(
    string StdOut,
    string StdErr,
    int ExitCode,
    TimeSpan Duration,
    bool IsTimedOut,
    bool IsCancelled);

internal sealed class ShellProcessExecutor
{
    public async Task<ProcessExecutionResult> ExecuteAsync(
        string fileName,
        string arguments,
        string input,
        TimeSpan? timeout = null,
        CancellationToken cancellationToken = default)
    {
        var stopwatch = Stopwatch.StartNew();
        using var process = new Process();
        process.StartInfo.FileName = fileName;
        process.StartInfo.Arguments = arguments;
        process.StartInfo.UseShellExecute = false;
        process.StartInfo.RedirectStandardInput = true;
        process.StartInfo.RedirectStandardOutput = true;
        process.StartInfo.RedirectStandardError = true;
        process.StartInfo.CreateNoWindow = true;

        var outputBuilder = new StringBuilder();
        var errorBuilder = new StringBuilder();
        var outputCloseEvent = new TaskCompletionSource<bool>(TaskCreationOptions.RunContinuationsAsynchronously);
        var errorCloseEvent = new TaskCompletionSource<bool>(TaskCreationOptions.RunContinuationsAsynchronously);
        var isTimedOut = false;
        var isCancelled = false;

        process.OutputDataReceived += (_, e) =>
        {
            if (e.Data is null) outputCloseEvent.TrySetResult(true);
            else outputBuilder.AppendLine(e.Data);
        };

        process.ErrorDataReceived += (_, e) =>
        {
            if (e.Data is null) errorCloseEvent.TrySetResult(true);
            else errorBuilder.AppendLine(e.Data);
        };

        try
        {
            process.Start();
            process.BeginOutputReadLine();
            process.BeginErrorReadLine();

            if (!string.IsNullOrEmpty(input))
            {
                await process.StandardInput.WriteAsync(input);
            }

            process.StandardInput.Close();

            var waitForExitTask = process.WaitForExitAsync(cancellationToken);

            if (timeout is { } timeoutValue)
            {
                var timeoutTask = Task.Delay(timeoutValue);
                var completedTask = await Task.WhenAny(waitForExitTask, timeoutTask);

                if (ReferenceEquals(completedTask, timeoutTask))
                {
                    isTimedOut = true;
                    TryTerminateProcess(process);
                    await process.WaitForExitAsync(CancellationToken.None);
                }
                else
                {
                    await waitForExitTask;
                }
            }
            else
            {
                await waitForExitTask;
            }

            await Task.WhenAll(outputCloseEvent.Task, errorCloseEvent.Task);
        }
        catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
        {
            isCancelled = true;
            TryTerminateProcess(process);
            await process.WaitForExitAsync(CancellationToken.None);
            await Task.WhenAll(outputCloseEvent.Task, errorCloseEvent.Task);
        }
        finally
        {
            stopwatch.Stop();
        }

        var exitCode = isTimedOut
            ? ToolExecutionResult.TimeoutExitCode
            : isCancelled
                ? ToolExecutionResult.CancelledExitCode
                : process.ExitCode;

        return new ProcessExecutionResult(
            StdOut: outputBuilder.ToString().TrimEnd(),
            StdErr: errorBuilder.ToString().TrimEnd(),
            ExitCode: exitCode,
            Duration: stopwatch.Elapsed,
            IsTimedOut: isTimedOut,
            IsCancelled: isCancelled);
    }

    private static void TryTerminateProcess(Process process)
    {
        try
        {
            if (!process.HasExited)
            {
                process.Kill(entireProcessTree: true);
            }
        }
        catch (InvalidOperationException)
        {
        }
    }
}
