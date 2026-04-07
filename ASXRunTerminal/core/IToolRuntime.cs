namespace ASXRunTerminal.Core;

internal interface IToolRuntime
{
    IReadOnlyList<ToolDescriptor> ListTools();

    Task<ToolExecutionResult> ExecuteAsync(
        ToolExecutionRequest request,
        CancellationToken cancellationToken = default);
}
