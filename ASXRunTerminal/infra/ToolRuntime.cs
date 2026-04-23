using System.Diagnostics;

using ASXRunTerminal.Core;

namespace ASXRunTerminal.Infra;

internal sealed class ToolRuntime : IToolRuntime
{
    private static readonly ToolParameter ShellScriptParameter = new(
        Name: "script",
        Description: "Script ou comando a ser executado no shell padrao.",
        IsRequired: true);
    private static readonly string[] KnownShellToolNames =
    [
        "powershell",
        "bash",
        "zsh"
    ];

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

        var toolNamesToTry = new List<string>();
        var knownToolNames = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
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

            AddToolNameCandidate(defaultShell);
            foreach (var fallbackShellName in GetShellFallbackToolNames())
            {
                AddToolNameCandidate(fallbackShellName);
            }
        }
        else
        {
            AddToolNameCandidate(request.ToolName);
        }

        ToolExecutionResult? lastUnavailableResult = null;
        var hasMatchingProvider = false;

        foreach (var toolName in toolNamesToTry)
        {
            var requestToExecute = request with { ToolName = toolName };
            foreach (var provider in _providers)
            {
                if (!provider.CanHandle(toolName))
                {
                    continue;
                }

                hasMatchingProvider = true;

                try
                {
                    var result = await provider.ExecuteAsync(requestToExecute, cancellationToken);
                    if (result.IsSuccess || result.IsTimedOut || result.IsCancelled)
                    {
                        return result;
                    }

                    if (!IsToolUnavailableResult(result))
                    {
                        return result;
                    }

                    lastUnavailableResult = result;
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

        if (lastUnavailableResult is ToolExecutionResult unavailableResult)
        {
            stopwatch.Stop();
            return ToolExecutionResult.Failure(
                error: unavailableResult.Error
                    ?? $"A ferramenta '{request.ToolName}' esta indisponivel no momento.",
                exitCode: unavailableResult.ExitCode == 0 ? 127 : unavailableResult.ExitCode,
                duration: stopwatch.Elapsed,
                stdOut: unavailableResult.StdOut);
        }

        if (hasMatchingProvider)
        {
            stopwatch.Stop();
            return ToolExecutionResult.Failure(
                error: $"A ferramenta '{request.ToolName}' falhou sem fallback disponivel.",
                exitCode: 1,
                duration: stopwatch.Elapsed);
        }

        stopwatch.Stop();
        return ToolExecutionResult.Failure(
            error: $"Nenhum provider registrado consegue executar a ferramenta '{request.ToolName}'.",
            exitCode: 127,
            duration: stopwatch.Elapsed);

        void AddToolNameCandidate(string? toolNameCandidate)
        {
            if (string.IsNullOrWhiteSpace(toolNameCandidate))
            {
                return;
            }

            var normalizedToolName = toolNameCandidate.Trim();
            if (!knownToolNames.Add(normalizedToolName))
            {
                return;
            }

            toolNamesToTry.Add(normalizedToolName);
        }
    }

    private IReadOnlyList<string> GetShellFallbackToolNames()
    {
        var fallbackShellNames = new List<string>();
        var knownShellNames = new HashSet<string>(StringComparer.OrdinalIgnoreCase);

        foreach (var provider in _providers)
        {
            foreach (var descriptor in provider.ListTools())
            {
                if (!IsShellToolName(descriptor.Name))
                {
                    continue;
                }

                var normalizedName = descriptor.Name.Trim();
                if (!knownShellNames.Add(normalizedName))
                {
                    continue;
                }

                fallbackShellNames.Add(normalizedName);
            }
        }

        return fallbackShellNames;
    }

    private static bool IsShellToolName(string? toolName)
    {
        if (string.IsNullOrWhiteSpace(toolName))
        {
            return false;
        }

        foreach (var knownShellToolName in KnownShellToolNames)
        {
            if (string.Equals(toolName, knownShellToolName, StringComparison.OrdinalIgnoreCase))
            {
                return true;
            }
        }

        return false;
    }

    private static bool IsToolUnavailableResult(ToolExecutionResult result)
    {
        if (result.ExitCode == 127)
        {
            return true;
        }

        return false;
    }
}
