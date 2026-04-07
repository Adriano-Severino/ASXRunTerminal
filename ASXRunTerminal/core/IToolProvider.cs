namespace ASXRunTerminal.Core;

internal interface IToolProvider
{
    string ProviderName { get; }

    IReadOnlyList<ToolDescriptor> ListTools();

    bool CanHandle(string toolName);

    Task<ToolExecutionResult> ExecuteAsync(
        ToolExecutionRequest request,
        CancellationToken cancellationToken = default);
}
