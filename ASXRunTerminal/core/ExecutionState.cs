namespace ASXRunTerminal.Core;

internal enum ExecutionState
{
    Connecting,
    Processing,
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
        return new ExecutionStateLabel(state switch
        {
            ExecutionState.Connecting => "conectando",
            ExecutionState.Processing => "processando",
            ExecutionState.Completed => "concluido",
            ExecutionState.Error => "erro",
            _ => throw new ArgumentOutOfRangeException(nameof(state), state, "Estado de execucao invalido.")
        });
    }
}
