namespace ASXRunTerminal.Core;

internal readonly record struct ToolExecutionRequest(
    string ToolName,
    IReadOnlyDictionary<string, string> Arguments,
    TimeSpan? Timeout = null)
{
    public static implicit operator ToolExecutionRequest(
        (string ToolName, Dictionary<string, string> Arguments) tuple)
    {
        return new ToolExecutionRequest(
            ToolName: tuple.ToolName,
            Arguments: tuple.Arguments,
            Timeout: null);
    }
}
