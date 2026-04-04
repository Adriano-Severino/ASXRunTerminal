namespace ASXRunTerminal.Core;

internal enum ExecutionState
{
    Connecting,
    ToolCall,
    Processing,
    Diff,
    Completed,
    Error
}

internal readonly record struct ExecutionStateLabel(string Value)
{
    public static implicit operator string(ExecutionStateLabel label)
    {
        return label.Value;
    }

    public static implicit operator ExecutionStateLabel(ExecutionState state)
    {
        TerminalExecutionStateToken token = state;
        return new ExecutionStateLabel(token.Label);
    }
}
