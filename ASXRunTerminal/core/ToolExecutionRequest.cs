namespace ASXRunTerminal.Core;

internal readonly record struct ToolExecutionRequest(
    string ToolName,
    IReadOnlyDictionary<string, string> Arguments)
{
    public static implicit operator ToolExecutionRequest(
        (string ToolName, Dictionary<string, string> Arguments) tuple)
    {
        return new ToolExecutionRequest(tuple.ToolName, tuple.Arguments);
    }
}
