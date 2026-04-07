using System.Diagnostics;

using ASXRunTerminal.Core;

namespace ASXRunTerminal.Infra;

internal sealed class ToolRuntime : IToolRuntime
{
    private readonly IReadOnlyList<IToolProvider> _providers;

    public ToolRuntime(IReadOnlyList<IToolProvider> providers)
    {
        ArgumentNullException.ThrowIfNull(providers);
        _providers = providers;
    }

    public ToolRuntime(params IToolProvider[] providers)
        : this((IReadOnlyList<IToolProvider>)providers)
    {
    }

    public IReadOnlyList<ToolDescriptor> ListTools()
    {
        var tools = new List<ToolDescriptor>();

        foreach (var provider in _providers)
        {
            tools.AddRange(provider.ListTools());
        }

        return tools;
    }

    public async Task<ToolExecutionResult> ExecuteAsync(
        ToolExecutionRequest request,
        CancellationToken cancellationToken = default)
    {
        var stopwatch = Stopwatch.StartNew();

        foreach (var provider in _providers)
        {
            if (provider.CanHandle(request.ToolName))
            {
                return await provider.ExecuteAsync(request, cancellationToken);
            }
        }

        stopwatch.Stop();
        return ToolExecutionResult.Failure(
            error: $"Nenhum provider registrado consegue executar a ferramenta '{request.ToolName}'.",
            exitCode: 127,
            duration: stopwatch.Elapsed);
    }
}
