namespace ASXRunTerminal.Infra;

internal sealed class McpStdioConnection : IAsyncDisposable
{
    private readonly Func<string> _standardErrorProvider;
    private readonly Func<ValueTask> _disposeAsync;
    private int _isDisposed;

    public McpStdioConnection(
        Stream input,
        Stream output,
        Func<string>? standardErrorProvider = null,
        Func<ValueTask>? disposeAsync = null)
    {
        Input = input ?? throw new ArgumentNullException(nameof(input));
        Output = output ?? throw new ArgumentNullException(nameof(output));
        _standardErrorProvider = standardErrorProvider ?? (() => string.Empty);
        _disposeAsync = disposeAsync ?? DisposeStreamsAsync;
    }

    public Stream Input { get; }

    public Stream Output { get; }

    public string GetStandardError()
    {
        return _standardErrorProvider();
    }

    public async ValueTask DisposeAsync()
    {
        if (Interlocked.Exchange(ref _isDisposed, 1) != 0)
        {
            return;
        }

        await _disposeAsync();
    }

    private ValueTask DisposeStreamsAsync()
    {
        Input.Dispose();
        Output.Dispose();
        return ValueTask.CompletedTask;
    }
}
