namespace ASXRunTerminal.Core;

internal readonly record struct ToolExecutionResult(
    bool IsSuccess,
    string StdOut,
    string StdErr,
    int ExitCode,
    TimeSpan Duration,
    bool IsTimedOut,
    bool IsCancelled)
{
    public const int TimeoutExitCode = 124;
    public const int CancelledExitCode = 130;

    public string Output => StdOut;

    public string? Error => string.IsNullOrWhiteSpace(StdErr) ? null : StdErr;

    public static ToolExecutionResult Success(
        string output,
        TimeSpan duration,
        string? stdErr = null)
    {
        return new ToolExecutionResult(
            IsSuccess: true,
            StdOut: output,
            StdErr: stdErr?.TrimEnd() ?? string.Empty,
            ExitCode: 0,
            Duration: duration,
            IsTimedOut: false,
            IsCancelled: false);
    }

    public static ToolExecutionResult Failure(
        string error,
        int exitCode,
        TimeSpan duration,
        string? stdOut = null)
    {
        return new ToolExecutionResult(
            IsSuccess: false,
            StdOut: stdOut?.TrimEnd() ?? string.Empty,
            StdErr: error.TrimEnd(),
            ExitCode: exitCode,
            Duration: duration,
            IsTimedOut: false,
            IsCancelled: false);
    }

    public static ToolExecutionResult TimedOut(
        TimeSpan duration,
        string? stdOut = null,
        string? stdErr = null)
    {
        var resolvedStdErr = string.IsNullOrWhiteSpace(stdErr)
            ? "Execucao da ferramenta excedeu o tempo limite configurado."
            : stdErr;

        return new ToolExecutionResult(
            IsSuccess: false,
            StdOut: stdOut?.TrimEnd() ?? string.Empty,
            StdErr: resolvedStdErr.TrimEnd(),
            ExitCode: TimeoutExitCode,
            Duration: duration,
            IsTimedOut: true,
            IsCancelled: false);
    }

    public static ToolExecutionResult Cancelled(
        TimeSpan duration,
        string? stdOut = null,
        string? stdErr = null)
    {
        var resolvedStdErr = string.IsNullOrWhiteSpace(stdErr)
            ? "Execucao da ferramenta cancelada."
            : stdErr;

        return new ToolExecutionResult(
            IsSuccess: false,
            StdOut: stdOut?.TrimEnd() ?? string.Empty,
            StdErr: resolvedStdErr.TrimEnd(),
            ExitCode: CancelledExitCode,
            Duration: duration,
            IsTimedOut: false,
            IsCancelled: true);
    }

    public static implicit operator ToolExecutionResult(string output)
    {
        return Success(output, TimeSpan.Zero, stdErr: null);
    }
}
