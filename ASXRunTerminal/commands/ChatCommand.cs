using ASXRunTerminal.Core;
using ASXRunTerminal.Infra;

namespace ASXRunTerminal.Commands;

/// <summary>
/// Command for interactive chat mode with the AI model.
/// </summary>
internal sealed class ChatCommand : CommandBase
{
    private const string CliName = "asxrun";
    private const string ModelFlag = "--model";

    public override string Name => "chat";
    public override string Description => "Modo interativo no terminal.";

    private readonly Func<string, string?, CancellationToken, IAsyncEnumerable<string>> _promptExecutor;
    private readonly Func<CancellationToken, Task<IReadOnlyList<OllamaLocalModel>>> _modelsExecutor;
    private readonly IToolRuntime _toolRuntime;
    private readonly Func<CancellationTokenSource, Action, IDisposable> _cancelSignalRegistration;
    private readonly Func<IReadOnlyList<PromptHistoryEntry>> _historyLoader;

    public ChatCommand(
        Func<string, string?, CancellationToken, IAsyncEnumerable<string>> promptExecutor,
        Func<CancellationToken, Task<IReadOnlyList<OllamaLocalModel>>> modelsExecutor,
        IToolRuntime toolRuntime,
        Func<CancellationTokenSource, Action, IDisposable> cancelSignalRegistration,
        Func<IReadOnlyList<PromptHistoryEntry>> historyLoader)
    {
        _promptExecutor = promptExecutor;
        _modelsExecutor = modelsExecutor;
        _toolRuntime = toolRuntime;
        _cancelSignalRegistration = cancelSignalRegistration;
        _historyLoader = historyLoader;
    }

    public override CommandParseResult ParseArguments(string[] args)
    {
        var commandArguments = args.Skip(1).ToArray();
        var optionError = TryExtractModelOption(
            commandArguments,
            out var selectedModel,
            out var remainingArguments);

        if (optionError is not null)
        {
            return Failure(optionError);
        }

        if (remainingArguments.Count > 0)
        {
            return Failure(CliFriendlyError.InvalidArguments(
                detail: "O comando 'chat' nao aceita argumentos adicionais.",
                suggestion: $"Exemplo: {CliName} chat."));
        }

        var parameters = new Dictionary<string, object>
        {
            { "model", selectedModel }
        };

        return Success(parameters);
    }

    public override Task<int> ExecuteAsync(CommandParseResult parseResult, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(parseResult);

        if (parseResult.HasError)
        {
            WriteFriendlyError(parseResult.Error ?? CliFriendlyError.Runtime("Unknown error"));
            return Task.FromResult((int)(parseResult.Error?.ExitCode ?? CliExitCode.RuntimeError));
        }

        var model = GetStringParameter(parseResult.Parameters, "model");

        // For now, delegate to the existing Program.cs ExecuteChat method
        // This will be refactored further in subsequent steps
        return Task.FromResult(ExecuteChat(
            model,
            _promptExecutor,
            _modelsExecutor,
            _toolRuntime,
            _cancelSignalRegistration,
            _historyLoader));
    }

    private static int ExecuteChat(
        string? model,
        Func<string, string?, CancellationToken, IAsyncEnumerable<string>> promptExecutor,
        Func<CancellationToken, Task<IReadOnlyList<OllamaLocalModel>>> modelsExecutor,
        IToolRuntime toolRuntime,
        Func<CancellationTokenSource, Action, IDisposable> cancelSignalRegistration,
        Func<IReadOnlyList<PromptHistoryEntry>> historyLoader)
    {
        // This is a placeholder - the full implementation will be moved here
        // For now, we'll call the existing static method from Program.cs
        // This will be refactored in subsequent steps
        ConsoleLogger.Info("Modo interativo (implementacao parcial - use Program.cs por enquanto)");
        return (int)CliExitCode.Success;
    }

    private CliFriendlyError? TryExtractModelOption(
        string[] arguments,
        out string? selectedModel,
        out List<string> remainingArguments)
    {
        ArgumentNullException.ThrowIfNull(arguments);

        selectedModel = null;
        remainingArguments = [];

        for (var index = 0; index < arguments.Length; index++)
        {
            var argument = arguments[index].Trim();

            if (string.Equals(argument, ModelFlag, StringComparison.OrdinalIgnoreCase))
            {
                if (selectedModel is not null)
                {
                    return CliFriendlyError.InvalidArguments(
                        detail: $"A opcao '{ModelFlag}' foi informada mais de uma vez no comando '{Name}'.",
                        suggestion: $"Exemplo: {CliName} chat --model {OllamaModelDefaults.DefaultModel}.");
                }

                if (index + 1 >= arguments.Length)
                {
                    return CliFriendlyError.InvalidArguments(
                        detail: $"A opcao '{ModelFlag}' exige um valor.",
                        suggestion: $"Exemplo: {CliName} chat --model {OllamaModelDefaults.DefaultModel}.");
                }

                selectedModel = arguments[index + 1].Trim();
                index++; // Skip the next argument as it's the value
            }
            else if (argument.StartsWith($"{ModelFlag}=", StringComparison.OrdinalIgnoreCase))
            {
                if (selectedModel is not null)
                {
                    return CliFriendlyError.InvalidArguments(
                        detail: $"A opcao '{ModelFlag}' foi informada mais de uma vez no comando '{Name}'.",
                        suggestion: $"Exemplo: {CliName} chat --model {OllamaModelDefaults.DefaultModel}.");
                }

                var candidate = argument[(ModelFlag.Length + 1)..].Trim();
                if (string.IsNullOrWhiteSpace(candidate))
                {
                    return CliFriendlyError.InvalidArguments(
                        detail: $"A opcao '{ModelFlag}' exige um valor.",
                        suggestion: $"Exemplo: {CliName} chat --model {OllamaModelDefaults.DefaultModel}.");
                }

                selectedModel = candidate;
            }
            else
            {
                remainingArguments.Add(argument);
            }
        }

        return null;
    }

    private static void WriteFriendlyError(CliFriendlyError error)
    {
        Program.WriteFriendlyError(error);
    }
}
