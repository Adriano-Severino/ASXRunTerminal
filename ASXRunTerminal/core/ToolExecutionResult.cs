namespace ASXRunTerminal.Core;

internal readonly record struct ToolExecutionResult(
    bool IsSuccess,
    string Output,
    string? Error,
    int ExitCode,
    TimeSpan Duration)
{
    public static ToolExecutionResult Success(string output, TimeSpan duration)
    {
        return new ToolExecutionResult(
            IsSuccess: true,
            Output: output,
            Error: null,
            ExitCode: 0,
            Duration: duration);
    }

    public static ToolExecutionResult Failure(string error, int exitCode, TimeSpan duration)
    {
        return new ToolExecutionResult(
            IsSuccess: false,
            Output: string.Empty,
            Error: error,
            ExitCode: exitCode,
            Duration: duration);
    }

    public static implicit operator ToolExecutionResult(string output)
    {
        return Success(output, TimeSpan.Zero);
    }
}
