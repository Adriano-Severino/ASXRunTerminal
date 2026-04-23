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
            StdOut: Sanitize(output),
            StdErr: Sanitize(stdErr),
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
            StdOut: Sanitize(stdOut),
            StdErr: Sanitize(error),
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
            StdOut: Sanitize(stdOut),
            StdErr: Sanitize(resolvedStdErr),
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
            StdOut: Sanitize(stdOut),
            StdErr: Sanitize(resolvedStdErr),
            ExitCode: CancelledExitCode,
            Duration: duration,
            IsTimedOut: false,
            IsCancelled: true);
    }

    public static implicit operator ToolExecutionResult(string output)
    {
        return Success(output, TimeSpan.Zero, stdErr: null);
    }

    private static string Sanitize(string? value)
    {
        return SecretMasker.Mask(value).TrimEnd();
    }
}
