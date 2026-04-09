using System.Diagnostics;

using ASXRunTerminal.Core;

namespace ASXRunTerminal.Infra;

internal sealed class ToolRuntime : IToolRuntime
{
    private static readonly ToolParameter ShellScriptParameter = new(
        Name: "script",
        Description: "Script ou comando a ser executado no shell padrao.",
        IsRequired: true);

    private readonly IReadOnlyList<IToolProvider> _providers;
    private readonly Func<string?> _defaultShellSelector;

    public ToolRuntime(
        IReadOnlyList<IToolProvider> providers,
        Func<string?>? defaultShellSelector = null)
    {
        ArgumentNullException.ThrowIfNull(providers);

        if (providers.Any(static provider => provider is null))
        {
            throw new ArgumentException(
                "A colecao de providers nao pode conter itens nulos.",
                nameof(providers));
        }

        _providers = providers.ToArray();
        _defaultShellSelector = defaultShellSelector ?? (() => ShellEnvironmentDetector.ResolveDefaultShell());
    }

    public ToolRuntime(params IToolProvider[] providers)
        : this((IReadOnlyList<IToolProvider>)providers, defaultShellSelector: null)
    {
    }

    public IReadOnlyList<ToolDescriptor> ListTools()
    {
        var tools = new List<ToolDescriptor>();

        foreach (var provider in _providers)
        {
            tools.AddRange(provider.ListTools());
        }

        var defaultShell = _defaultShellSelector();
        if (!string.IsNullOrWhiteSpace(defaultShell) &&
            tools.All(static tool => !string.Equals(tool.Name, "shell", StringComparison.OrdinalIgnoreCase)))
        {
            tools.Add(new ToolDescriptor(
                Name: "shell",
                Description: $"Executa scripts no shell padrao da plataforma atual ({defaultShell}).",
                Parameters: [ShellScriptParameter]));
        }

        return tools;
    }

    public async Task<ToolExecutionResult> ExecuteAsync(
        ToolExecutionRequest request,
        CancellationToken cancellationToken = default)
    {
        var stopwatch = Stopwatch.StartNew();

        if (cancellationToken.IsCancellationRequested)
        {
            stopwatch.Stop();
            return ToolExecutionResult.Cancelled(stopwatch.Elapsed);
        }

        if (request.Timeout is { } timeout && timeout <= TimeSpan.Zero)
        {
            stopwatch.Stop();
            return ToolExecutionResult.Failure(
                error: "O timeout da tool call deve ser maior que zero.",
                exitCode: 1,
                duration: stopwatch.Elapsed);
        }

        var requestToExecute = request;

        if (string.Equals(request.ToolName, "shell", StringComparison.OrdinalIgnoreCase))
        {
            var defaultShell = _defaultShellSelector();
            if (string.IsNullOrWhiteSpace(defaultShell))
            {
                stopwatch.Stop();
                return ToolExecutionResult.Failure(
                    error: "Nao foi possivel detectar um shell padrao para a plataforma atual.",
                    exitCode: 127,
                    duration: stopwatch.Elapsed);
            }

            requestToExecute = request with { ToolName = defaultShell };
        }

        foreach (var provider in _providers)
        {
            if (provider.CanHandle(requestToExecute.ToolName))
            {
                try
                {
                    return await provider.ExecuteAsync(requestToExecute, cancellationToken);
                }
                catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
                {
                    stopwatch.Stop();
                    return ToolExecutionResult.Cancelled(stopwatch.Elapsed);
                }
                catch (TimeoutException ex)
                {
                    stopwatch.Stop();
                    return ToolExecutionResult.TimedOut(
                        duration: stopwatch.Elapsed,
                        stdErr: ex.Message);
                }
            }
        }

        stopwatch.Stop();
        return ToolExecutionResult.Failure(
            error: $"Nenhum provider registrado consegue executar a ferramenta '{request.ToolName}'.",
            exitCode: 127,
            duration: stopwatch.Elapsed);
    }
}
